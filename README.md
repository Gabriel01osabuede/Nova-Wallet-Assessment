# NovaWallet Ledger Service

FirstBank Nigeria .NET assessment — Andrew Osabuede Gabriel.

ASP.NET Core 9 controllers, vertical slices, EF Core 9, and SQL Server 2022.
See [TECHNICAL_DESIGN.md](TECHNICAL_DESIGN.md) for the implementation blueprint.

Verified on 16 September 2026: 23 unit tests and 15 real-SQL integration tests passed.
Docker build, Compose startup/migrations, and the deployed API smoke test also passed.
The smoke test reconciled balances, statement/audit entries, idempotent replay, and HTTP 409 conflicts.

## Run

Requires Docker Desktop running Linux containers. From this directory:

    docker compose up

Compose starts SQL Server, applies checked-in migrations through a one-shot privileged
bootstrap process, provisions a restricted runtime login, then starts the API.
No manual database setup is required. After code changes, use:

    docker compose up --build

- Swagger: http://localhost:8080/swagger
- OpenAPI: http://localhost:8080/swagger/v1/swagger.json
- Liveness: http://localhost:8080/health/live
- Readiness: http://localhost:8080/health/ready

**Demo credentials and signing key are intentionally disposable and publicly visible.**
They exist solely for single-command assessment startup. Never use them in production.
Override them through the variable names in .env.example; do not commit a real .env.
SQL Server is not published to the host. SQL data persists in the sql-data volume.
Do not delete volumes to restart the service.

## Obtain demo tokens

Token minting is a development-only local CLI command, not an anonymous HTTP endpoint.
Each token lasts one hour. Paste the token into Swagger's Authorize field.

    docker compose exec api dotnet NovaWallet.Api.dll --token 7dbecbac-45b8-48a6-a81e-43209e7aa119 Customer
    docker compose exec api dotnet NovaWallet.Api.dll --token 8dbecbac-45b8-48a6-a81e-43209e7aa119 Customer
    docker compose exec api dotnet NovaWallet.Api.dll --token 9dbecbac-45b8-48a6-a81e-43209e7aa119 FundingSystem
    docker compose exec api dotnet NovaWallet.Api.dll --token adbecbac-45b8-48a6-a81e-43209e7aa119 Auditor

## Demonstration sequence

1. Authorize as customer A; POST /api/v1/wallets with A's customer GUID.
2. Authorize as customer B; create B's wallet. Retain both returned wallet IDs.
3. Authorize as funding system; credit A with amount 100000 (naira) and reference demo-credit-1.
4. Authorize as A; transfer amount 10000 (naira) to B with Idempotency-Key: demo-transfer-1.
5. Repeat the same transfer/key: the original response returns with Idempotency-Replayed: true.
6. Change the amount under that key: 409 idempotency-conflict.
7. Inspect balances and A's statement; authorize as auditor to inspect the separate audit trail.

Credit and transfer requests accept numeric amount in naira: 10000 means ₦10,000.
The API converts it exactly to 1000000 integer kobo before financial processing.
The balance endpoint returns balance in naira and balanceInKobo in integer kobo.
Credits return amountKobo and cumulative totalbalanceInNaira. Transfers return amountKobo
and sourceBalanceInNaira. Financial history, audit, limits and persistence remain in kobo.
Customers cannot credit themselves
or access another customer's balance, statement, or source wallet.
Duplicate wallet creation returns the existing wallet ID and a Location link.

## Architecture and financial safety

- One application; use-case-specific requests, responses, and handlers live in feature folders.
- Controllers bind HTTP and apply policies. Handlers orchestrate; domain methods own money arithmetic.
- Credits/transfers use one SQL transaction for balances, history, audit, and terminal idempotency responses.
- SQL transaction-owned resource locks serialize identical keys before financial work.
- Sequential UPDLOCK/HOLDLOCK wallet lookups follow ordinal GUID-string order.
- Daily outbound usage is checked while the source is locked using a midnight-WAT window.
- SQL constraints protect non-negative balances, valid arithmetic, unique wallets, and unique history.
- Audit, financial history, and transfers reject updates/deletes through permissions and triggers.
- Retryable SQL faults replay the whole transaction with a fresh context and the same idempotency protocol.
- Transfer keys are customer-scoped; funding references are global within the simulated NIP source.
- Expected financial rejections remain stored. A deliberate new attempt requires a new key.
- JSON operational logs and trace IDs are separate from durable financial audit evidence.

The database protects concurrent application writers, not a malicious administrator.
This is paired wallet debit/credit evidence, not a complete accounting ledger with clearing accounts.
Real NIP/KYC, production identity, administrator tamper evidence, and the outbox are excluded.
Rate limiting is per instance; wallet consistency remains database-enforced across instances.
.NET 9 matches the brief but needs a production upgrade plan before its support ends.

## Tests

Install .NET SDK 9, then:

    dotnet restore
    dotnet build
    dotnet test tests/NovaWallet.UnitTests
    dotnet test tests/NovaWallet.IntegrationTests

Integration tests require Docker. They provision a separate SQL Server container and isolated
databases, exercising the API through HTTP and a restricted runtime login.
They never reset the demonstration datastore.

The concurrency test sends 100 simultaneous ₦10,000 requests against ₦100,000:
exactly 10 must succeed, 90 must fail for insufficient funds, and balances/history/audit must reconcile.
Another sends 50 identical requests through two API hosts connected to the same database.

Other scenarios cover idempotency conflicts/rejection replay, daily-limit contention,
credit deduplication/overflow, JWT roles/ownership, exact naira input conversion,
audit-insertion rollback, append-only permissions/triggers, statements, Swagger, health, and throttling.
The 15 SQL integration tests passed, including opposing transfers, concurrent changed-payload
conflicts, WAT midnight reset, and database-unavailable readiness. Deliberate lost-commit-response
and SQL deadlock fault injection have not been executed; they are not claimed as passing tests.

## Local Git handoff

Target repository: [Nova-Wallet-Assessment](https://github.com/Gabriel01osabuede/Nova-Wallet-Assessment).

For subsequent updates, review the files before committing and pushing:

    git add .
    git diff --cached --stat
    git commit -m "Implement NovaWallet ledger assessment"
    git push -u origin main

The assessment PDF and local agent guidance are excluded. Do not stage real credentials.
For a private remote, grant the panel access through their supplied addresses.
The separate presentation is Andrew_Osabuede_Gabriel.pptx, with an accompanying PDF.
See PRESENTATION_GUIDE.md for timing, speaker-note access, and review guidance.

## Latest verification

Dual-unit balance and credit-total responses passed 48 unit tests and 17 SQL integration
tests. Docker was rebuilt; deployed smoke passed with balance, balanceInKobo and credit
totalbalanceInNaira assertions. Existing data and historical replay responses were preserved.

The balance-response revision subsequently passed 48 unit tests and 16 SQL integration
tests on 17 September. Docker was rebuilt and the deployed smoke passed with balance
values 90000/10000 naira. The response is now balance, not balanceKobo.

The naira-input revision passed 44 unit tests and 16 real-SQL integration tests on
17 September 2026. This includes all original financial-safety scenarios and new
exact conversion and normalized-idempotency checks. The deck still shows the earlier
explicitly dated 16 September snapshot; it has not been regenerated over candidate edits.

## Credit and transfer request amounts

### Balance response

GET /api/v1/wallets/{walletId}/balance returns balance in naira and balanceInKobo in kobo:

```json
{
  "walletId": "11111111-1111-1111-1111-111111111111",
  "currency": "NGN",
  "balance": 1500.50,
  "balanceInKobo": 150050
}
```

MoneyConversion.KoboToNaira uses exact decimal division only for presentation. Stored
balances and financial operations remain integer kobo; no float/double is introduced.
The kobo field also satisfies the brief's balance-unit requirement. For an already
released API, use explicit versioning rather than silently changing units or field names.

### Credit response

The credit response uses totalbalanceInNaira instead of balanceAfterKobo:

```json
{
  "creditId": "33333333-3333-3333-3333-333333333333",
  "walletId": "11111111-1111-1111-1111-111111111111",
  "amountKobo": 10000000,
  "currency": "NGN",
  "totalbalanceInNaira": 100000,
  "completedAtUtc": "2026-09-17T12:04:42.4198384+00:00"
}
```

This is the cumulative balance immediately after the credit, not just the deposited amount.
Its exact spelling matches the requested JSON. Credit replays return the original stored
response, not the later balance. References committed before this contract change retain
their original response shape: use a fresh reference to test the new response.

### Credit and transfer requests

Transfer requests accept amount in naira, not kobo: amount 1000 is NGN 1,000 and
becomes amountKobo 100000 in the response. Newly committed transfer responses return
sourceBalanceInNaira instead of sourceBalanceAfterKobo, representing the actual source
balance after debit. For example, 9900000 stored kobo is 99000 naira, not 900000.
Stored idempotency responses retain their original shape; test the updated contract
using a fresh Idempotency-Key rather than replaying a previously completed transfer.

This is a breaking request-contract change: use amount (naira), not amountKobo.
Legacy amountKobo request fields are rejected rather than guessed or reinterpreted.
Numeric fixed-point amounts with at most two decimal places are accepted; strings,
scientific notation, zero, negatives, excess precision and out-of-range values return 400.
There is no rounding. The maximum is 92233720368547758.07 naira (long.MaxValue kobo).
Idempotency fingerprints use the converted kobo: 10000 and 10000.00 are the same amount.

Credit request:

```json
{
  "amount": 10000.50,
  "reference": "demo-naira-credit-1"
}
```

Transfer request (substitute the returned wallet IDs):

```json
{
  "sourceWalletId": "11111111-1111-1111-1111-111111111111",
  "destinationWalletId": "22222222-2222-2222-2222-222222222222",
  "amount": 10000
}
```

MoneyConversion.TryNairaToKobo converts decimal text using only integer digit parsing
and checked integer arithmetic. NairaAmount's JSON converter invokes it before handlers
execute; it does not use floating-point or decimal arithmetic to represent money.

## Automated deployed demonstration

With Compose running, use PowerShell:

    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Smoke-Test.ps1

This waits for readiness, creates fresh fictional wallets, funds one, transfers,
verifies matching replay and conflicting payload rejection, and reconciles balances,
statement entries, and audit entries. It does not delete or reset data.
