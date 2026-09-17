param([string]$BaseUrl = 'http://127.0.0.1:8080')

$ErrorActionPreference = 'Stop'
$targetUri = [Uri]$BaseUrl
if ($targetUri.Host -notin @('localhost', '127.0.0.1') -or $targetUri.Scheme -ne 'http') {
    throw 'The assessment smoke test targets only the local demo API.'
}

function New-DemoToken([Guid]$Subject, [string]$Role) {
    $tokenOutput = & docker compose exec -T api dotnet NovaWallet.Api.dll --token $Subject.ToString('D') $Role
    if ($LASTEXITCODE -ne 0) { throw 'Demo token generation failed.' }
    return ($tokenOutput | Select-Object -Last 1).Trim()
}

function Invoke-DemoPost([string]$Path, [string]$Token, [object]$Payload, [string]$Key = '') {
    $requestHeaders = @{ Authorization = 'Bearer ' + $Token }
    if ($Key) { $requestHeaders['Idempotency-Key'] = $Key }
    $parameters = @{
        Uri = $BaseUrl + $Path
        UseBasicParsing = $true
        Method = 'Post'
        Headers = $requestHeaders
        ContentType = 'application/json'
        Body = $Payload | ConvertTo-Json -Depth 5
    }
    return Invoke-WebRequest @parameters
}

$customerA = [Guid]::NewGuid()
$customerB = [Guid]::NewGuid()
$tokenA = New-DemoToken $customerA 'Customer'
$tokenB = New-DemoToken $customerB 'Customer'
$fundingToken = New-DemoToken ([Guid]::NewGuid()) 'FundingSystem'
$auditorToken = New-DemoToken ([Guid]::NewGuid()) 'Auditor'

foreach ($path in @('/health/live', '/swagger/v1/swagger.json')) {
    $check = Invoke-WebRequest -Uri ($BaseUrl + $path) -UseBasicParsing
    if ($check.StatusCode -ne 200) { throw ('Deployment check failed: ' + $path) }
}

$ready = $false
for ($attempt = 1; $attempt -le 10; $attempt++) {
    try {
        $probe = Invoke-WebRequest -Uri ($BaseUrl + '/health/ready') -UseBasicParsing
        if ($probe.StatusCode -eq 200) { $ready = $true; break }
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 503) { throw }
    }
    Start-Sleep -Seconds 2
}
if (-not $ready) { throw 'The API did not become ready within the bounded startup wait.' }

$createdA = Invoke-DemoPost '/api/v1/wallets' $tokenA @{ customerId = $customerA.ToString('D') }
$createdB = Invoke-DemoPost '/api/v1/wallets' $tokenB @{ customerId = $customerB.ToString('D') }
if ($createdA.StatusCode -ne 201 -or $createdB.StatusCode -ne 201) { throw 'Wallet creation failed.' }
$walletA = ($createdA.Content | ConvertFrom-Json).walletId
$walletB = ($createdB.Content | ConvertFrom-Json).walletId
$credit = Invoke-DemoPost ("/api/v1/wallets/$walletA/credits") $fundingToken @{
    amount = [long]100000; reference = 'smoke-credit-' + [Guid]::NewGuid().ToString('N')
}
if ($credit.StatusCode -ne 200) { throw 'Funding failed.' }
$creditResponse = $credit.Content | ConvertFrom-Json
if ($creditResponse.totalbalanceInNaira -ne 100000 -or $creditResponse.amountKobo -ne 10000000 -or
    $creditResponse.PSObject.Properties.Name -contains 'balanceAfterKobo') { throw 'Credit response contract failed.' }

$key = 'smoke-transfer-' + [Guid]::NewGuid().ToString('N')
$payload = @{ sourceWalletId = $walletA; destinationWalletId = $walletB; amount = [long]10000 }
$transfer = Invoke-DemoPost '/api/v1/transfers' $tokenA $payload $key
$transferResponse = $transfer.Content | ConvertFrom-Json
if ($transferResponse.amountKobo -ne 1000000 -or $transferResponse.sourceBalanceInNaira -ne 90000 -or
    $transferResponse.PSObject.Properties.Name -contains 'sourceBalanceAfterKobo') { throw 'Transfer response contract failed.' }
$replay = Invoke-DemoPost '/api/v1/transfers' $tokenA $payload $key
if ($transfer.Content -ne $replay.Content -or $replay.Headers['Idempotency-Replayed'] -ne 'true') {
    throw 'Idempotency replay failed.'
}
$payload.amount = [long]20000
$conflictStatus = 0
try { $null = Invoke-DemoPost '/api/v1/transfers' $tokenA $payload $key }
catch {
    if ($_.Exception.Response) { $conflictStatus = [int]$_.Exception.Response.StatusCode }
    else { throw }
}
if ($conflictStatus -ne 409) { throw 'Changed-payload conflict was not rejected.' }

$balanceA = Invoke-RestMethod -Uri ("$BaseUrl/api/v1/wallets/$walletA/balance") -Headers @{Authorization = 'Bearer ' + $tokenA}
$balanceB = Invoke-RestMethod -Uri ("$BaseUrl/api/v1/wallets/$walletB/balance") -Headers @{Authorization = 'Bearer ' + $tokenB}
$statement = Invoke-RestMethod -Uri ("$BaseUrl/api/v1/wallets/$walletA/statement") -Headers @{Authorization = 'Bearer ' + $tokenA}
$audit = Invoke-RestMethod -Uri ("$BaseUrl/api/v1/audit/wallets/$walletA") -Headers @{Authorization = 'Bearer ' + $auditorToken}
if ($balanceA.balance -ne 90000 -or $balanceB.balance -ne 10000) { throw 'Naira balances do not reconcile.' }
if ($balanceA.balanceInKobo -ne 9000000 -or $balanceB.balanceInKobo -ne 1000000) { throw 'Kobo balances do not reconcile.' }
if ($statement.totalCount -ne 2 -or $audit.totalCount -ne 2) { throw 'History/audit counts do not reconcile.' }

[PSCustomObject]@{
    Result = 'PASS'
    SourceWallet = $walletA
    DestinationWallet = $walletB
    TransferId = ($transfer.Content | ConvertFrom-Json).transferId
    SourceBalanceNaira = $balanceA.balance
    DestinationBalanceNaira = $balanceB.balance
    IdempotentReplay = $true
    ConflictingPayloadStatus = $conflictStatus
    SourceHistoryEntries = $statement.totalCount
    SourceAuditEntries = $audit.totalCount
}

# This creates fresh fictional demo wallets. It does not delete or reset any data.
