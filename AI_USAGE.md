# AI usage — Andrew Osabuede Gabriel

## Tools and purpose

ChatGPT supported assessment analysis and architecture discussion. Codex inspected the brief,
developed the TD, wrote code/migrations/tests, and ran verification.
AI output is reviewed against requirements and SQL Server behavior, not accepted as proof.

## Actual prompts and outcomes

1. **“I'm thinking of using wolverine to handle this, any benefits over using interfaces and dependency injections?”**
   ChatGPT explained that Wolverine provides command/messaging infrastructure, not a replacement for DI.
   The candidate ultimately selected standard DI with vertical slices without Wolverine.

2. **“Look at this format if we can make something like this, which is normally clearer.”**
   The candidate provided a feature-led document example. Codex adapted the TD to business context,
   tables, endpoints, payloads, critical flows, and explicit limitations.

3. **“Okay base on this we can begin.”**
   Codex began the controllers/vertical-slices/SQL Server implementation, including constraints,
   financial idempotency, audit protections, and unit/integration tests.

## Request history and follow-up work

This log covers the candidate requests available in this project's conversation history
and the supplied pasted ChatGPT conversation. It is a request-and-outcome summary, not a
verbatim export of every message. Quoted excerpts retain the original wording; longer
requests and acknowledgements are summarized. Where an earlier action is not independently
confirmed, the log records the request without inventing a completion claim.

### Assessment intake and supplied ChatGPT discussion

1. **Assessment preparation:** The candidate provided the FirstBank recruitment email,
   asked "How do we begin," and directed the assistant to the attachment in the directory.
   The work focused on the NovaWallet brief, its implementation deliverables and the
   separate ten-minute PowerPoint presentation.
2. **Candidate name:** "Andrew Osabuede Gabriel" was supplied for attribution and
   presentation filenames.
3. **Shared conversation:** The candidate asked whether the assistant could access a
   ChatGPT share link. The request is recorded here; successful retrieval of that link
   is not claimed. The candidate subsequently supplied the conversation as a local
   pasted-text attachment and asked the assistant to read the earlier thinking.
4. **Brief supplied as text:** In the pasted conversation, the candidate explained that
   mobile PDF upload was unavailable and supplied the assessment text. ChatGPT discussed
   requirements, integer money, concurrency, idempotency, limits, audit and testing.
   Its initial .NET 8/PostgreSQL suggestions were discussion proposals, not the final stack.
5. **Local implementation workflow:** "We will work on this using codex on my laptop."
   The subsequent implementation and verification took place in the local workspace.
6. **Wolverine versus DI:** The candidate asked about using Wolverine instead of interfaces
   and dependency injection. ChatGPT explained their different responsibilities and
   suggested a vertical-slice option. The final implementation uses standard DI,
   controllers and feature handlers without Wolverine or MediatR.

### Technical design and architectural clarification

7. **Design before slides:** The candidate proposed preparing a technical document first,
   implementing the application, and using that work for PowerPoint, while asking whether
   slides could precede implementation. The eventual deck used the design and recorded
   implementation results rather than presenting planned tests as executed evidence.
8. **Project role instructions:** The candidate requested reading Agent.md before starting
   the TD. Its technical-lead guidance was read and used to keep the solution simple,
   explicit about risks, and focused on financial correctness.
9. **Prepare the TD:** "Okay let's prepare a TD now." The design document was developed
   as an implementation reference and later updated with verified results.
10. **Feature-led document format:** "Look at this format if we can make something like
    this, which is normally clearer." The candidate supplied the Customer Review Flag
    PBI example, including context, tables, enums, endpoints, restrictions and limitations.
    Its structure informed the wallet TD; its CRM business rules were not added to NovaWallet.
11. **Purpose of the TD:** The candidate asked whether the document was for building the
    application, PowerPoint, or both. It serves as the implementation reference and a
    source for presentation content; the slides are a shorter interview narrative.
12. **Complete the design document:** The candidate asked what remained to complete
    the technical design, then authorized proceeding. The local document is named
    TECHNICAL_DESIGN.md, with requirements, contracts, transaction strategy, controls,
    testing, deployment and explicit exclusions.
13. **Outbox scope:** The candidate challenged the wording that the transactional outbox
    was deferred and asked whether it would actually be implemented and why.
    The final submission scope excludes an outbox and broker. They are future extensions
    if committed movements must reliably produce external side effects.
14. **Handler and module responsibilities:** The candidate asked what the feature-folder
    plan meant and whether each module owned its interfaces and other components.
    Requests, responses and handlers are grouped by use case; financial infrastructure
    is shared. Interfaces are not created mechanically for every handler.
15. **Pattern name:** "So what pattern is this called?" The agreed structure is
    controller-based Vertical Slice Architecture in one deployable application.
16. **Readiness to implement:** The candidate stated that the TD had been reviewed,
    asked whether anything was missed, then authorized implementation with
    "Okay base on this we can begin." The application, migrations, tests, Docker setup,
    README and AI usage documentation were implemented and verified as recorded below.

The final agreed stack is .NET 9, EF Core 9, SQL Server 2022 and xUnit/Testcontainers.
The earlier PostgreSQL/Wolverine proposals in the supplied conversation are not described
as implemented components. Acknowledgements such as "Okay make sense," "Okay thanks,"
and "Okay let's proceed" are treated as confirmations of the corresponding discussion,
not as additional features or independent test evidence.

### Presentation requests and explanations

17. **Presentation timing during review:** "Currently reviewing. How about the powerpoint
    slides when do we get that done?" The candidate asked to prepare the slides alongside
    code review. The assistant proposed a ten-minute deck using the implemented architecture
    and recorded evidence. "Noted" acknowledged that proposal.
18. **Create the PowerPoint:** "Proceed to do the power point now." The candidate explained
    that subsequent code changes should not materially affect the architecture or standing
    rules. Codex created Andrew_Osabuede_Gabriel.pptx, a PDF review copy, editable diagrams,
    speaker notes, rendered previews and PRESENTATION_GUIDE.md. Ten main slides have a
    ten-minute target, with three hidden backup slides. The deck's results are explicitly
    the 16 September 2026 verification snapshot.
19. **Crossed-out slide numbers:** "The power point canceled out slide 11 to 13 can you
    explain why." The assistant explained that slides 11-13 are hidden backups, not
    deleted: API contracts, failure recovery and AI assistance. Instructions were provided
    to toggle Hide Slide; no subsequent unhide operation was requested or performed.

### Code review refactoring and documentation

20. **Split LedgerRecords.cs:** The candidate requested that database entities and enums
    no longer share a single file, specifying Domain/LedgerRecords/{class file name} and
    Domain/Enums/{Enum file name}. Codex replaced the combined file with:
    - LedgerRecords/Transfer.cs
    - LedgerRecords/WalletTransaction.cs
    - LedgerRecords/AuditEntry.cs
    - LedgerRecords/IdempotencyRecord.cs
    - Enums/TransferStatus.cs
    - Enums/WalletTransactionType.cs
    - Enums/AuditOperation.cs
    - Enums/IdempotencyStatus.cs

    Existing NovaWallet.Api.Domain namespaces, properties, accessors, defaults and enum
    values were preserved. This was file organization only, not a database schema change.
    The solution built with zero warnings/errors and all 23 unit tests passed after the
    split. The SQL integration suite was not rerun for this file-only refactor.
21. **Keep the AI usage history complete:** "Kindly ensure to add this recent requests
    along with all previous requests in the AI_Usage.md file." Codex expanded this document
    with the supplied conversation, earlier design requests, presentation work, hidden-slide
    explanation, domain refactoring and this documentation request. Existing unsafe-advice
    corrections and verification evidence were retained.

22. **Align namespaces after the split:** The candidate requested correcting the new
    class namespaces and LedgerDbContext references. Entities now use
    NovaWallet.Api.Domain.LedgerRecords and enums use NovaWallet.Api.Domain.Enums.
    Codex updated the consuming handlers, shared persistence helpers and EF model snapshot.
    Wallet remains in NovaWallet.Api.Domain; historical migration designers were preserved.
    The solution built with zero warnings/errors and all 23 unit tests passed.

23. **Accept naira at the API boundary:** During manual testing, the candidate requested
    "Refactor these API's to accepts the amount in naira rather than requiring the client
    to provide an amount already converted to kobo," and explicitly asked to document it.
    The request examples use amount 10000 to mean ten thousand naira. Codex changed credit
    and transfer requests to amount, introduced MoneyConversion.TryNairaToKobo and the
    NairaAmount JSON converter, and kept financial processing/storage in integer kobo.
    Fixed-point JSON numbers with up to two decimal places convert exactly using integer
    arithmetic. Excess precision, non-positive amounts, strings, scientific notation and
    kobo overflow are rejected without rounding. Legacy amountKobo fields are rejected.
    Responses retain their kobo field names. Fingerprints still use integer kobo, so
    equivalent naira inputs replay the same intent. Tests, Swagger, README, TD and the
    smoke script were updated. On 17 September 2026, 44 unit tests and 16 real-SQL
    integration tests passed after the change. The earlier architecture/authentication
    questions marked by the candidate as excluded are not added to this log.

24. **Present balance in naira:** The candidate requested changing
    /api/v1/wallets/{walletId}/balance to return "balance" instead of "balancekobo",
    with the converted naira value. Codex changed only BalanceResponse and its handler,
    added MoneyConversion.KoboToNaira for exact decimal presentation, and retained long
    kobo storage and financial arithmetic. No float/double or rounding is used. Tests,
    smoke assertions, README and TD were updated. The documentation explicitly notes
    the breaking response contract and deviation from the brief's original kobo balance
    requirement. Other endpoint response money fields are unchanged. Verification for
    this revision is recorded separately once executed.

25. **Dual-unit balance and naira funding total:** After reviewing the brief again, the
    candidate requested balanceInKobo alongside balance on the balance endpoint, and
    replacing balanceAfterKobo with totalbalanceInNaira in the credit response while
    retaining amountKobo. Codex added the stored integer kobo balance to BalanceResponse
    and exact decimal cumulative naira total to CreditWalletResponse, explicitly preserving
    the JSON spelling totalbalanceInNaira. Financial storage, arithmetic, transaction
    evidence and transfer responses remain unchanged. Tests cover unit consistency,
    fractional naira, cumulative credits and stored-response replay. README, TD and the
    deployed smoke checks were updated. Previously committed idempotency responses retain
    their exact original shape; fresh references exercise the new credit response.

26. **Naira source balance after transfer:** The candidate requested replacing
    sourceBalanceAfterKobo with sourceBalanceInNaira in transfer responses and confirming
    that transfer inputs accept naira. Codex changed the response to exact decimal naira
    using MoneyConversion.KoboToNaira; amountKobo remains the transfer amount in kobo.
    The request remains numeric amount in naira, converted exactly at the API boundary.
    The candidate's illustrative 9,900,000 kobo balance was clarified as 99,000 naira,
    not 900,000. SQL HTTP assertions verify whole and fractional naira inputs, the actual
    post-transfer source balance and removal of the old field. TD, README and smoke checks
    were updated. Previously completed keys replay their original stored response.

27. **Publish the assessment repository:** The candidate requested a push to
    https://github.com/Gabriel01osabuede/Nova-Wallet-Assessment.git. Codex inspected the
    Git state, target remote and intended files for unintended credentials, then prepared
    a commit of the application, tests, deployment files, documentation and presentation.
    Demo-only credentials remain explicitly documented. Local agent guidance, assessment
    PDF, generated previews and private working notes are excluded from the staged handoff.
    Actual publication is evidenced by the remote Git history, not by a planned push.

Repository publication is separately authorized above. No entry implies emailing the
deck or submitting the assessment to FirstBank; those actions have not been performed.

## Presentation assistance

On 17 September 2026 the candidate requested, "Proceed to do the power point now,"
preserving the agreed architecture and rules during code review. Codex generated a local
editable PowerPoint with speaker notes, PDF and rehearsal guide from the TD and recorded
results. All 13 rendered slides were checked and layout adjustments applied. The results
slide is explicitly dated 16 September; presentation creation does not imply a new test
run or submission to FirstBank.

## Unsafe advice identified and corrected

The earlier ChatGPT conversation described application-only audit restrictions as “probably sufficient”
for the assessment if documented. This is weaker than the append-only, immutable-audit requirement:
another code path or direct SQL using an overly privileged runtime identity could edit records.

The implementation instead provisions a restricted runtime login, explicitly denies update/delete
on audit/history/transfer tables, adds immutability triggers, and rejects EF history updates/deletes.
SQL integration tests attempt runtime writes and privileged accidental deletes.
Their execution evidence must be recorded only after actual passes.

The prior read/check/write concurrency example was explicitly a warning, not an AI recommendation.
It is not presented as a reproduced AI-generated regression.

## Implementation review corrections

- Numeric range attributes initially used a constructor accepting floating-point bounds.
  They were changed to explicit long ranges before HTTP money-path tests.
- Trigger-bearing EF mappings use UseSqlOutputClause(false), avoiding SQL Server OUTPUT incompatibility.
- Wrapped transient SQL exceptions are unwrapped so the entire idempotent transaction enters bounded retry.
- The first real-SQL HTTP run returned 500 during wallet creation because Codex targeted
  validation attributes at generated properties of positional request records.
  ASP.NET Core expects that metadata on constructor parameters. The attributes were corrected
  on wallet creation, credit, and transfer DTOs, then the integration suite was rerun.
  This failure demonstrates why compilation and domain-only tests cannot prove HTTP behavior.

## Verification evidence

- Unit suite: 23 passed on 16 September 2026.
- Initial application build: zero warnings/errors.
- SQL integration suite: 15 passed, zero failed/skipped, on 16 September 2026.
- Combined solution test run: 23 unit tests and 15 SQL integration tests passed.
- Docker image build, Compose startup/migrations, and deployed API smoke test passed.
- Deployed smoke reconciled source/destination balances of 9,000,000/1,000,000 kobo,
  exact response replay, changed-payload HTTP 409, and two source history/audit entries.
- Solution formatting completed successfully.

### Naira-input revision verification - 17 September 2026

- Solution build: zero warnings/errors; 44 unit tests and 16 real-SQL integration tests passed.
- EF model consistency check: no pending changes; no migration was added.
- Docker image rebuild and Compose startup/migration bootstrap completed successfully.
- Deployed smoke passed with naira request bodies, exact replay, HTTP 409 conflict,
  balances 9,000,000/1,000,000 kobo, and two source history/audit entries.
- Deployed Swagger confirms numeric amount in both request schemas, not amountKobo.
- Readiness returned HTTP 200; the existing SQL data volume was preserved.
- The PowerPoint was not overwritten or regenerated; the rehearsal guide notes the
  updated request contract and the deck's historical verification snapshot.

### Naira balance-response verification - 17 September 2026

- All 48 unit tests and 16 real-SQL integration tests passed after the response change.
- Conversion tests cover zero, one kobo, fractional naira and long.MaxValue exactly.
- HTTP tests assert numeric balance, NGN currency and absence of balanceKobo.
- Docker rebuild/startup succeeded; the deployed smoke passed with naira balances
  90,000/10,000, matching idempotent replay, HTTP 409 conflict and reconciled evidence.
- Existing SQL data was preserved. No schema or other endpoint response change was made.

### Dual-unit balance and credit-total verification - 17 September 2026

- All 48 unit tests and 17 real-SQL integration tests passed.
- Balance HTTP assertions verify naira balance equals balanceInKobo / 100 exactly.
- Credit HTTP assertions verify totalbalanceInNaira, retained amountKobo and removal
  of balanceAfterKobo. A cumulative-credit test proves replay preserves the original total.
- Docker rebuild/startup and deployed smoke passed with both balance units and the
  new credit response. Existing SQL data and historical idempotency responses were preserved.

### Transfer source-balance verification - 17 September 2026

- All 48 unit tests and 17 real-SQL integration tests passed.
- HTTP tests prove amount 10 naira becomes amountKobo 1000 and amount 10.01 becomes
  amountKobo 1001; sourceBalanceInNaira reflects the exact resulting balance.
- Tests verify removal of sourceBalanceAfterKobo and matching stored-response replay.
- Docker rebuild/startup and deployed smoke passed with the new response assertions.
- Stored balances/evidence and prior idempotency outcomes were not rewritten.

No production-readiness, regulatory-compliance, or unexecuted-test claims are made.
