param([string]$OutputDirectory = (Split-Path $PSScriptRoot -Parent), [switch]$ReplaceGenerated)
$ErrorActionPreference = 'Stop'

# Native editable PowerPoint. No external assets, credentials, or network requests.
$navy = 0x302017
$blue = 0xB87C00
$gold = 0x39BFE8
$white = 0xFFFFFF
$muted = 0xD9CDBE
$panel = 0x493325
$green = 0xAFD372
$slideNumber = 0
$timings = @(25,45,60,45,90,70,65,65,85,50)
$app = New-Object -ComObject PowerPoint.Application
$app.DisplayAlerts = 1
$deck = $null
function Box($slide, $x, $y, $w, $h, $color) {
    $shape = $slide.Shapes.AddShape(1,$x,$y,$w,$h)
    $shape.Fill.ForeColor.RGB = $color
    $shape.Line.Visible = 0
    return $shape
}
function Text($slide, $x, $y, $w, $h, [string]$value, $size=20, $color=$white, $bold=$false) {
    $shape = $slide.Shapes.AddTextbox(1,$x,$y,$w,$h)
    $shape.TextFrame.MarginLeft=0; $shape.TextFrame.MarginRight=0
    $shape.TextFrame.MarginTop=0; $shape.TextFrame.MarginBottom=0
    $shape.TextFrame.WordWrap=-1
    $shape.TextFrame.AutoSize=0
    $range = $shape.TextFrame.TextRange
    $range.Text=$value; $range.Font.Name='Aptos'; $range.Font.Size=$size
    $range.Font.Color.RGB=$color; $range.Font.Bold=[int]$bold * -1
    $range.ParagraphFormat.SpaceAfter=8
    return $shape
}
function Arrow($slide,$x1,$y1,$x2,$y2) {
    $line=$slide.Shapes.AddLine($x1,$y1,$x2,$y2)
    $line.Line.ForeColor.RGB=$gold; $line.Line.Weight=2
    $line.Line.EndArrowheadStyle=3
}
function Card($slide,$x,$y,$w,$h,$label,$body,$accent=$gold) {
    $null=Box $slide $x $y $w $h $panel
    $null=Box $slide $x $y 4 $h $accent
    $heading=Text $slide ($x+18) ($y+16) ($w-36) 70 $label 20 $accent $true
    $heading.Height=$heading.TextFrame.TextRange.BoundHeight+2
    $bodyOffset=[Math]::Max(58,16+$heading.Height+12)
    $bodyHeight=$h-$bodyOffset-12
    $copy=Text $slide ($x+18) ($y+$bodyOffset) ($w-36) $bodyHeight $body 18
    $fontSize=18
    while($copy.TextFrame.TextRange.BoundHeight -gt $bodyHeight -and $fontSize -gt 14) {
        $fontSize--
        $copy.Delete()
        $copy=Text $slide ($x+18) ($y+$bodyOffset) ($w-36) $bodyHeight $body $fontSize
    }
}
function New-Slide($title,$subtitle,$notes) {
    $script:slideNumber++
    $s=$deck.Slides.Add($script:slideNumber,12)
    $s.FollowMasterBackground=0; $s.Background.Fill.ForeColor.RGB=$navy
    $null=Box $s 40 32 38 4 $gold
    $null=Text $s 40 51 880 54 $title 32 $white $true
    $null=Text $s 40 111 880 38 $subtitle 16 $muted
    $null=Text $s 40 509 790 18 'ANDREW OSABUEDE GABRIEL  |  FIRSTBANK .NET ASSESSMENT' 10 $muted
    $null=Text $s 870 505 55 22 ('{0:00}' -f $script:slideNumber) 13 $gold $true
    $s.NotesPage.Shapes.Placeholders.Item(2).TextFrame.TextRange.Text=$notes
    if ($script:slideNumber -le 10) {
        $s.SlideShowTransition.AdvanceOnTime=0
        $s.SlideShowTransition.AdvanceOnClick=-1
        $s.SlideShowTransition.AdvanceTime=$timings[$script:slideNumber-1]
    } else { $s.SlideShowTransition.Hidden=-1 }
    return $s
}
try {
    $deck=$app.Presentations.Add(0)
    $deck.PageSetup.SlideWidth=960; $deck.PageSetup.SlideHeight=540

    $s=New-Slide 'Reliable wallet APIs' 'NovaWallet Ledger Service | Backend / API Developer (.NET) case study' @'
25 seconds. Good day, my name is Andrew Osabuede Gabriel. My solution is NovaWallet, a wallet API designed around financial correctness: a transfer must not double-spend, an HTTP retry must not move money twice, and every committed movement must leave durable evidence. I will explain the architecture, the critical transaction flow, and the results verified on 16 September 2026. This is an assessment implementation, not a claim of production banking readiness.
'@
    $null=Text $s 40 182 790 90 'Move money once.`nKeep the evidence.' 43 $white $true
    # Use actual newline rather than a literal PowerShell escape in single-quoted text.
    $s.Shapes.Item($s.Shapes.Count).TextFrame.TextRange.Text="Move money once.`nKeep the evidence."
    Card $s 40 332 270 117 'CORRECTNESS' 'Atomic balances and limits'
    Card $s 330 332 270 117 'RELIABILITY' 'Durable idempotent outcomes'
    Card $s 620 332 300 117 'ACCOUNTABILITY' 'Separate append-only audit'
    $null=Text $s 40 467 850 25 'Andrew Osabuede Gabriel  |  17 September 2026' 17 $muted

    $s=New-Slide 'The brief becomes five invariants' 'Business rules drive the design, not the framework.' @'
45 seconds. A new wallet starts at zero and uses NGN. All amounts are signed 64-bit integer kobo, never floating-point money. A transfer debits and credits atomically and never permits a negative balance. Repeating an operation with the same key and payload returns its stored outcome, while a changed payload is rejected. Outbound transfers are capped at NGN 500,000 per wallet per WAT day, resetting at midnight. Each committed movement must have financial history and a separate audit entry. JWT, paginated statements and one-command Docker startup complete the mandatory scope. Inbound credit simulates NIP; it is not a real payment-network integration.
'@
    $rules=@(
        @('01','Exact money','NGN only; long / bigint kobo; zero opening balance'),
        @('02','Safe spending','Atomic debit + credit; no negative balances'),
        @('03','Safe retries','Same key + payload = stored response; changed payload = 409'),
        @('04','Daily control','Outbound cap: NGN 500,000; reset at midnight WAT'),
        @('05','Durable evidence','Financial history and audit commit with the movement')
    )
    $y=167
    foreach($r in $rules) {
        $null=Text $s 42 $y 48 34 $r[0] 22 $gold $true
        $null=Text $s 105 $y 205 36 $r[1] 21 $white $true
        $null=Text $s 325 ($y+2) 595 41 $r[2] 18 $muted
        $y+=61
    }

    $s=New-Slide 'One application. Clear feature boundaries.' 'Controller-based Vertical Slice Architecture | .NET 9 + EF Core 9 + SQL Server 2022' @'
60 seconds. I selected a single deployable application with controller-based vertical slices. Each use case owns its request, response and handler, such as CreateWallet or TransferFunds. Controllers bind HTTP and apply authorization policies. Handlers orchestrate the use case, while domain methods enforce money arithmetic. Shared infrastructure owns the database transaction and idempotency protocol, so credits and transfers do not duplicate financial safeguards. EF Core maps the data, and SQL Server provides the transaction, locks and constraints. I deliberately avoided a mediator, a message broker and premature microservices. Interfaces are introduced where replacement or testing justifies them, not for each class. The shared database also coordinates independent API hosts.
'@
    Card $s 40 174 190 136 'HTTP + JWT' "Controllers`nPolicies + validation"
    Card $s 280 174 240 136 'FEATURE HANDLERS' "Wallets / Transfers`nStatements / Audit"
    Card $s 570 174 160 136 'DOMAIN' "Money rules`nWallet state"
    Arrow $s 231 242 277 242; Arrow $s 522 242 567 242
    Card $s 280 357 450 109 'SHARED FINANCIAL INFRASTRUCTURE' 'Transaction + idempotency + evidence + WAT clock'
    Arrow $s 400 312 400 352
    Card $s 775 357 145 109 'SQL' 'Locks + checks'
    Arrow $s 733 412 770 412
    $null=Text $s 43 366 203 81 "One deployable unit`nNo mediator or broker" 17 $muted

    $s=New-Slide 'A small model with strong database rules' 'Operational balance + immutable movement evidence; not a full general ledger.' @'
45 seconds. Wallet holds the current spendable balance and one unique customer identity. Transfer records a completed source-to-destination movement. WalletTransactions provide statement entries with before and after balances, and AuditEntries separately record the actor, operation and trace. IdempotencyRecords bind a scoped key and request fingerprint to a terminal HTTP outcome. A successful transfer creates one transfer, two statement entries and two audit entries. SQL checks protect non-negative balances and valid arithmetic; unique indexes protect customer wallets, scoped keys and wallet-operation evidence. Audit immutability is reinforced by runtime permissions, SQL triggers and the EF save guard. Administrators remain a separate threat boundary.
'@
    Card $s 40 169 273 142 'Wallet' "CustomerId UNIQUE`nBalanceKobo bigint >= 0`nCurrency = NGN"
    Card $s 344 169 273 142 'Transfer' "Source + destination`nAmountKobo > 0`nCompletedAtUtc"
    Card $s 648 169 273 142 'IdempotencyRecord' "Scope + key UNIQUE`nRequest hash`nStored HTTP outcome"
    Card $s 190 360 273 117 'WalletTransaction' "Operation + before/after`nNewest-first statement"
    Card $s 510 360 273 117 'AuditEntry' "Actor + trace + operation`nAppend-only evidence"
    $null=Text $s 40 324 880 23 'One successful transfer: 1 transfer record + 2 history entries + 2 audit entries' 16 $gold

    $s=New-Slide 'The transfer is one indivisible SQL transaction' 'READ COMMITTED + explicit transaction-owned locks; never a process-local mutex.' @'
90 seconds. The controller authenticates the customer and validates integer inputs. Inside the financial transaction, the first lock is a SQL application lock for the scoped idempotency key. If a committed outcome exists, we replay it or reject a changed fingerprint. For a new operation, source and destination wallets are locked sequentially using UPDLOCK and HOLDLOCK in deterministic ordinal GUID-string order. The source ownership is rechecked under lock. We then check funds, recipient overflow and daily outbound usage while the source remains locked. WAT time is captured after wallet locks. We stage the debit, credit, transfer, two history entries, two audit entries and stored response, then commit them together. If evidence insertion fails, balances and the key roll back too. Lock timeout returns a retryable 503; transient SQL faults use bounded whole-transaction retries with a fresh context. The source lock prevents concurrent requests from both passing a stale balance or limit check. Different keys do not bypass that protection.
'@
    $steps=@(
        @('1','Lock scoped key','SQL sp_getapplock'),
        @('2','Lock both wallets','Sequential, stable order'),
        @('3','Check all rules','Owner / funds / overflow / cap'),
        @('4','Stage all evidence','Balances + history + audit'),
        @('5','Commit outcome','Persist response with money')
    )
    $x=40
    foreach($st in $steps) {
        Card $s $x 198 164 172 ($st[0]+'  '+$st[1]) $st[2]
        if($x -lt 740) { Arrow $s ($x+167) 282 ($x+177) 282 }
        $x+=179
    }
    $null=Box $s 40 407 880 65 $panel
    $null=Text $s 60 426 840 35 'ALL COMMIT TOGETHER  /  ALL ROLL BACK TOGETHER' 23 $gold $true

    $s=New-Slide 'Idempotency survives HTTP retries and API replicas' 'The database stores the outcome; the key is not an in-memory cache.' @'
70 seconds. Transfer keys are scoped to the customer and compared exactly. A canonical SHA-256 fingerprint binds source, destination and amount to the key. The transaction-owned SQL key lock serializes duplicate submissions even when they reach different API hosts. The same fingerprint returns the original stored status and JSON with Idempotency-Replayed true. A changed payload gets HTTP 409 without modifying the original outcome. Financial 422 rejections are stored too: adding funds later does not change that key's previous rejection, so a new intentional attempt requires a new key. Syntax, authorization and existence failures do not reserve the key. Funding uses a global simulator reference scope to deduplicate across funding identities. For transient SQL failures, a fresh attempt reacquires the key and checks committed state before doing money work. Deliberate lost-commit-response injection has not been executed, so I describe that recovery protocol without claiming fault-test evidence or end-to-end exactly-once delivery.
'@
    Card $s 40 173 260 132 'SCOPED KEY + HASH' "Customer-scoped transfer key`nCanonical request fingerprint"
    Card $s 359 173 240 132 'SQL KEY LOCK' "Serializes duplicates`nAcross API hosts"
    Arrow $s 303 240 354 240
    Arrow $s 603 240 652 240
    Card $s 657 163 263 87 'MATCH' 'Stored status + exact JSON' $green
    Card $s 657 266 263 87 'MISMATCH' '409; original outcome unchanged'
    $null=Text $s 40 380 880 40 'A key identifies one intent, including its financial rejection.' 25 $white $true
    $null=Text $s 40 439 880 36 'Retry the same intent with the same key. Use a new key for a deliberate new attempt.' 18 $muted

    $s=New-Slide 'Daily limits and audit are inside the safety boundary' 'Business controls are enforced by the backend, not just the user interface.' @'
65 seconds. WAT is UTC plus one hour with no daylight saving. Midnight WAT corresponds to 23:00 UTC on the preceding date. While the source wallet is locked, the handler sums completed outbound transfers in a half-open UTC window and checks that prior usage plus this amount is no more than 50 million kobo. The wide SQL integer aggregate avoids bigint sum overflow. Incoming funds do not consume or restore outbound allowance. Audit entries are inserted in the same transaction as the money movement, so failed audit insertion prevents the transfer from committing. The runtime identity can select and insert audit, history and transfer records but cannot update or delete them. Triggers add protection against privileged accidental edits; they are not a defense against a malicious database administrator who can disable them. Operational JSON logs remain separate from financial evidence.
'@
    Card $s 40 172 422 238 'WAT OUTBOUND CAP' "NGN 500,000 = 50,000,000 kobo`n`nMidnight WAT = 23:00 UTC`nWindow: start inclusive, end exclusive`nCheck completed outbound while locked"
    Card $s 498 172 422 238 'APPEND-ONLY EVIDENCE' "Audit inserted with the movement`n`nRestricted runtime SQL identity`nDENY UPDATE / DELETE + SQL triggers`nEF guard rejects history edits"
    $null=Text $s 40 439 880 37 'An audit failure rolls back the money. A malicious administrator needs stronger controls.' 18 $gold

    $s=New-Slide 'Secure API boundaries. Reproducible deployment.' 'JWT roles + ownership | consistent errors | one-command assessment startup' @'
65 seconds. JWT validation checks signature, algorithm, issuer, audience and expiry. Customers can create their own wallet, view their own balance and statement, and transfer only from their own source wallet. The funding role controls simulated inbound credit, and auditors or administrators inspect audit trails. Missing authentication is 401, forbidden access is 403, and errors use consistent Problem Details with trace IDs. Strict JSON rejects fractional and string amounts. Rate limiting is per instance; it is an operational control, not the financial integrity mechanism. Docker Compose starts SQL Server, then a privileged one-shot migration/bootstrap process, and finally the API using a restricted runtime login. The API runs as non-root and SQL is not published to the host. Readiness probes the database while liveness only confirms the process. Demo credentials and CLI tokens are assessment-only: production requires managed identity, secret storage and a real identity provider.
'@
    Card $s 40 170 270 219 'ACCESS' "Customer: own wallet / source`nFundingSystem: inbound credit`nAuditor / Admin: audit trail`n`nJWT signature + claims validated"
    Card $s 335 170 270 219 'API OPERATIONS' "Problem Details + trace IDs`nStrict integer money inputs`nPaginated newest-first history`n`nJSON logs + per-instance limits"
    Card $s 630 170 290 219 'DOCKER COMPOSE' "SQL Server`n    -> migrations / login bootstrap`n    -> restricted, non-root API`n`nLiveness + database readiness"
    $null=Text $s 40 429 880 44 'docker compose up' 30 $gold $true
    $null=Text $s 40 474 880 23 'Disposable demo credentials only; no claim of production identity or real NIP integration.' 15 $muted

    $s=New-Slide 'Correctness demonstrated against real SQL Server' 'Recorded 16 September 2026 | 23 unit + 15 integration tests passed; zero failed/skipped.' @'
85 seconds. The unit suite has 23 passing tests and the SQL integration suite has 15. The key spending test sends 100 simultaneous NGN 10,000 transfers against NGN 100,000: exactly 10 succeed and 90 reject for insufficient funds, leaving source zero and destination NGN 100,000. It also reconciles ten transfer records, twenty history entries and twenty audit entries. Fifty identical requests across two hosts produce one movement and 49 replays. Opposing transfers preserve total balances. Other tests cover daily-limit contention, actual WAT midnight reset, credit overflow, changed-payload conflict, authorization and integer inputs. Injected audit-insert failure proves whole-transaction rollback, and direct SQL attempts prove the append-only protections. The deployed Compose smoke separately reconciled source NGN 90,000 and destination NGN 10,000, exact replay and HTTP 409. These are correctness tests, not a throughput benchmark. Clean-checkout restart-persistence acceptance and deliberate deadlock or lost-commit-response injection remain unexecuted.
'@
    $rows=@(
        @('100 competing spends','10 succeed / 90 reject; balances and evidence reconcile'),
        @('50 duplicates / 2 hosts','One money movement + 49 response replays'),
        @('Limits + boundary time','Contention respects cap; midnight WAT resets allowance'),
        @('Audit fault + SQL writes','Whole transfer rolls back; historical edits are blocked'),
        @('Deployed Docker smoke','Balances 9,000,000 / 1,000,000 kobo; replay + 409 verified')
    )
    $y=171
    foreach($r in $rows) {
        $null=Box $s 40 $y 880 52 $panel
        $null=Text $s 55 ($y+13) 267 31 $r[0] 18 $gold $true
        $null=Text $s 330 ($y+13) 575 34 $r[1] 17 $white
        $y+=59
    }
    $null=Text $s 40 478 880 24 'Correctness evidence, not a performance benchmark or production-readiness certification.' 14 $muted

    $s=New-Slide 'Simple scope. Explicit trade-offs. A credible next step.' 'Deliver the mandatory guarantees first; extend without weakening the transaction boundary.' @'
50 seconds. The assessment delivers a single application, local SQL transactions, durable idempotency, audited movements and runnable evidence. The trade-off is intentional serialization on a hot wallet and reliance on one database's availability; throughput and lock behavior must be measured before scaling. A paired wallet transfer is not a complete bank general ledger. Real settlement, KYC, reversals and external payment rails are outside scope. No message broker or transactional outbox is included because the mandatory workflow does not publish events. If external side effects are introduced, an outbox becomes a deliberate extension. Before production I would integrate managed authentication and secrets, plan the runtime support upgrade, add administrator tamper evidence, and validate disaster recovery, load and fault scenarios. AI accelerated drafting, but unsafe application-only audit advice was corrected and real HTTP SQL tests caught a DTO validation bug. My key message is that financial integrity is enforced where concurrent writers meet: inside the database transaction. Thank you; I welcome your questions.
'@
    Card $s 40 172 422 218 'DELIVERED NOW' "One application; one financial transaction`nDurable idempotency + immutable evidence`n38 passing automated tests`nCompose + Swagger + deployed smoke"
    Card $s 498 172 422 218 'BEFORE PRODUCTION' "Managed identity, secrets + runtime upgrade`nReal settlement / full accounting design`nAdmin tamper evidence + disaster recovery`nLoad, lost-commit and deadlock fault tests"
    $null=Text $s 40 423 880 39 'No broker or outbox in this submission. Add an outbox when external side effects exist.' 18 $gold
    $null=Text $s 40 473 880 26 'Financial integrity belongs inside the transaction boundary.  |  Questions' 19 $white $true

    $s=New-Slide 'Backup | API contracts and permissions' 'Hidden from the timed slideshow; available for technical questions.' @'
Backup only. All routes use /api/v1. The transfer Idempotency-Key header is required; credit reference supplies the simulator deduplication identity. Source customer ownership is checked again under locks. Duplicate wallet creation returns 409 with the existing walletId and a Location link. Statement reads use snapshot isolation for consistent count and page items within one response; page requests are not a permanent cross-page snapshot. Demo token issuance is development-only CLI, not an anonymous HTTP token endpoint.
'@
    $endpoints=@(
        @('POST /wallets','Customer; own customerId'),
        @('GET /wallets/{id}/balance','Customer; owner'),
        @('POST /wallets/{id}/credits','FundingSystem / Admin'),
        @('POST /transfers','Customer; source owner; Idempotency-Key'),
        @('GET /wallets/{id}/statement','Customer; owner; page/pageSize'),
        @('GET /audit/wallets/{id}','Auditor / Admin; page/pageSize')
    )
    $y=167
    foreach($r in $endpoints) {
        $null=Text $s 40 $y 455 40 $r[0] 18 $gold $true
        $null=Text $s 510 $y 410 40 $r[1] 18 $white
        $y+=49
    }

    $s=New-Slide 'Backup | Failure outcomes and recovery' 'A new key means a new intent. A technical retry keeps the original key.' @'
Backup only. Ordinary financial rejections are returned as structured decisions, not exceptions. Stored 422 outcomes include insufficient funds, daily cap and overflow. 400, 403 and 404 do not consume a new key. Technical faults dispose the transaction; when commit acknowledgement is uncertain, clients must not assume rollback. Retry using the same key so the next attempt checks durable outcome before mutation. No terminal Processing row is committed. Bounded whole-transaction retries use a new DbContext; they are not retries of only the final SQL command. A terminal rejection remains authoritative for its key even after funds or the day change. The advanced lost-commit response and deliberate deadlock injection tests are not part of the executed evidence.
'@
    Card $s 40 172 422 137 'BUSINESS OUTCOMES' "422 financial rejection: stored and replayed`n409 changed payload: original unchanged`n400 / 403 / 404: no new key reserved"
    Card $s 498 172 422 137 'TECHNICAL OUTCOMES' "Lock timeout: 503; retry same key`nTransient SQL: bounded fresh-context retry`nUncertain outcome: verify via same-key retry"
    $null=Text $s 40 365 880 43 'Do not turn a network timeout into a second payment.' 29 $gold $true
    $null=Text $s 40 425 880 46 'Database state is authoritative; transport success alone is not the financial truth.' 21 $white

    $s=New-Slide 'Backup | AI assistance and evidence discipline' 'AI output was reviewed and corrected; passing tests, not generated code, support the claims.' @'
Backup only. ChatGPT supported requirements and architecture discussions. Codex drafted the TD and implementation, migrations and tests, then executed verification. The earlier application-only audit advice was too weak, so the implementation added restricted SQL permissions, immutability triggers and EF guards. A genuine implementation mistake targeted validation attributes at generated properties on positional request records; SQL HTTP tests failed before wallet creation, exposing it. Attributes were moved to constructor parameters, and the suite was rerun successfully. AI_USAGE.md records actual prompts and corrections. The candidate should review and own the reasoning; neither generated code nor this deck is a regulatory assurance. Test counts are a 16 September snapshot and should be rerun if money-path code changes.
'@
    Card $s 40 174 422 218 'REVIEWED AND CORRECTED' "Application-only audit advice was too weak`nAdded permissions + triggers + EF guard`n`nHTTP tests exposed DTO validation metadata`nCorrected targets; reran SQL suite"
    Card $s 498 174 422 218 'TRACEABLE HANDOFF' "TECHNICAL_DESIGN.md: decisions + boundaries`nREADME.md: run, demo and tests`nAI_USAGE.md: actual prompts + corrections`n`nEvidence snapshot: 16 September 2026"
    $null=Text $s 40 437 880 39 'Code review owns the solution. AI assistance does not replace verification.' 23 $gold $true

    $pptx=Join-Path $OutputDirectory 'Andrew_Osabuede_Gabriel.pptx'
    $pdf=Join-Path $OutputDirectory 'Andrew_Osabuede_Gabriel.pdf'
    if((Test-Path -LiteralPath $pptx) -and -not $ReplaceGenerated) { throw 'Presentation already exists; preserve candidate edits instead of overwriting.' }
    $deck.SaveAs($pptx,24)
    $deck.SaveAs($pdf,32)
    $preview=Join-Path $OutputDirectory 'presentation-preview'
    $null=New-Item -ItemType Directory -Path $preview -Force
    foreach($slide in $deck.Slides) {
        $slide.Export((Join-Path $preview ('slide-{0:00}.png' -f $slide.SlideIndex)),'PNG',1600,900)
    }
    $overflow=@()
    foreach($slide in $deck.Slides) {
        foreach($shape in $slide.Shapes) {
            if($shape.HasTextFrame -and $shape.TextFrame.HasText) {
                if($shape.TextFrame.TextRange.BoundHeight -gt ($shape.Height+3)) {
                    $overflow+=('Slide '+$slide.SlideIndex+': '+$shape.TextFrame.TextRange.Text)
                }
            }
        }
    }
    [PSCustomObject]@{Slides=$deck.Slides.Count; MainSlides=10; BackupSlides=3; PlannedSeconds=($timings|Measure-Object -Sum).Sum; OverflowCount=$overflow.Count; PowerPoint=$pptx; PDF=$pdf}
    $overflow | ForEach-Object { Write-Warning $_ }
} finally {
    if($deck) { $deck.Close() }
    $app.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($app)
}
