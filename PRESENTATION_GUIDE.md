# Presentation guide - Andrew Osabuede Gabriel

## Files

- Andrew_Osabuede_Gabriel.pptx: editable 16:9 PowerPoint with speaker notes.
- Andrew_Osabuede_Gabriel.pdf: reading/review copy; submit the PowerPoint as requested.
- presentation-preview/: rendered PNGs for visual checking.

There are 10 main slides and three hidden backup slides. Backups do not appear in the
normal slideshow; use them for questions about API contracts, failure recovery, and AI usage.
Notes are in PowerPoint's Notes pane and Presenter View. Slide changes remain manual.

## Ten-minute rehearsal

| Slide | Topic | Seconds | Cumulative |
|---|---|---:|---:|
| 1 | Introduction and financial integrity | 25 | 0:25 |
| 2 | Requirements and invariants | 45 | 1:10 |
| 3 | Vertical Slice Architecture | 60 | 2:10 |
| 4 | Data model and constraints | 45 | 2:55 |
| 5 | Atomic transfer and wallet locks | 90 | 4:25 |
| 6 | Durable idempotency | 70 | 5:35 |
| 7 | WAT daily cap and audit | 65 | 6:40 |
| 8 | Security and Compose deployment | 65 | 7:45 |
| 9 | Verified test evidence | 85 | 9:10 |
| 10 | Trade-offs and production next steps | 50 | 10:00 |

These are target times, not a guarantee of delivery speed. Rehearse aloud once with
PowerPoint's Rehearse Timings. Aim to finish slightly early rather than read every note.
The notes provide explanations to adapt into your own words, not a script you must memorize.
No live coding or live demonstration is needed inside this ten-minute narrative.

## Review priorities

Be comfortable explaining these distinctions:

- The idempotency lock serializes duplicate keys. Wallet locks serialize competing
  spending even when requests have different keys.
- A successful transfer commits balances, transfer evidence, statement entries, audit,
  and stored outcome together. An audit insertion failure rolls the transfer back.
- Financial 422 rejections are stored. The same key replays its rejection even after
  funds arrive; a deliberate new attempt uses a new key.
- WAT midnight is 23:00 UTC on the previous date. The handler captures the time after
  acquiring wallet locks and checks a start-inclusive/end-exclusive UTC window.
- Two wallet entries are not a complete accounting general ledger.
- Permissions and triggers do not make records immune to a malicious database administrator.
- No transactional outbox or message broker is implemented. An outbox becomes relevant
  when committed money movements must reliably drive external side effects.
- Per-instance rate limiting is not the cross-instance financial safety mechanism.

## Evidence boundary

The subsequent naira-input change passed 44 unit and 16 SQL integration tests on
17 September. Credit and transfer request bodies now use amount in naira; the API converts
it to integer kobo before financial work. Response fields remain in kobo. Existing slides
have not been overwritten; adjust any spoken claim that request JSON must be an integer
to explain exact fixed-point naira conversion. The dated 16 September results remain
a historical snapshot, not the latest counts.

The results are the verified 16 September 2026 snapshot: 23 unit tests and 15 real-SQL
integration tests passed, along with Docker build/startup and deployed smoke checks.
The concurrency scenarios are correctness evidence, not a measured throughput benchmark.
Do not claim executed deadlock/lost-commit-response injection or clean-checkout
restart-persistence acceptance; those checks remain outstanding.

Code refactoring that preserves the design does not require changing the architecture
slides. If you change locks, transaction boundaries, daily-limit semantics, authorization,
or idempotency behavior, review the affected slide and notes. Rerun tests before claiming
that this dated evidence applies to a changed implementation.

## Teams presentation

Test screen sharing and slide readability before the interview. Share the slideshow
window, not the notes window, to keep speaker notes private. If using PowerPoint Live,
confirm what the audience sees in advance. Keep tokens, terminals containing credentials,
and unrelated applications out of the shared view. Phones are not permitted by the invitation.

The presentation has been prepared locally only. It has not been emailed, uploaded,
or submitted to FirstBank.

## Rebuilding

scripts/Build-Presentation.ps1 is the generation source, using installed Microsoft PowerPoint.
It refuses to overwrite an existing deck by default. Do not rebuild after manually editing
the PowerPoint unless you intentionally want to replace those edits; preserve a copy first.
The ReplaceGenerated switch exists for deliberate regeneration, not routine code review.
