# Verification record

Audit: 14 November 2026, Windows, .NET SDK 10.0.401.

## Executed checks

- 67 xUnit cases passed, zero failed or skipped. Machine results: verification/tests.trx.
- 70.32% total line coverage (1,844/2,622), 56.39% branch coverage. See verification/COVERAGE.md and coverage.cobertura.xml. The denominator includes Identity template pages and migrations.
- Pure calculations, decimal/minor-unit handling, CSV quoting and formula protection.
- Real SQLite CRUD, validation, monthly boundaries, owner isolation, foreign keys, duplicate categories/budgets and atomic sample creation.
- Blazor component events: category/transaction/budget CRUD, overspending, 45-row pagination, composed search/filter, month navigation, dashboard/analytics, read-error retry, successful-write/failed-refresh without duplicate writes.
- HTTP: real registration, weak/duplicate credentials, missing antiforgery rejection, login/logout, password/profile change, five-password lockout, private routes, CSS/Blazor scripts, owner/month-filtered CSV and invalid months.
- Two-factor setup, invalid/valid authenticator code, recovery login, rejected reused recovery code, regeneration and authenticator reset.
- A 10,000-row correctness test and an eight-worker file-backed SQLite workload: 25 writes and reads per owner, 200 transactions total, with exact counts, amounts and owner boundaries. Completed in the passing test run; this is a local concurrency check, not a production capacity claim.
- The packaged tests/e2e.cjs passed in installed Edge and Chrome, Playwright Firefox and WebKit on Windows: registration, sample loading, search, validation, transaction CRUD, CSV download, logout/login. All five tabs checked at widths 375, 768 and 1280 with no horizontal overflow. JSON results include versions; desktop/mobile screenshots are in verification.
- tests/HttpSmoke.ps1 passed during the earlier audit. Release publish with a fresh database returned HTTP 200 for landing page and all four CSS assets. Database files are excluded from publish.

## Reproduction

From the solution folder: dotnet test FinanceTracker.sln --collect:"XPlat Code Coverage". Tests isolate their SQLite databases and test users from the demonstration account. HTTP tests use temporary file databases and test-only ephemeral cookie protection.

Start the app before running browser tests; follow tests/README.md. Browser tests create their own sample accounts and delete them at the end of the successful scenario.

## Limits

Native macOS/Linux, actual Safari, physical phones, screen-reader testing and CPU/network profiling remain unverified. Playwright WebKit on Windows is not a Safari/macOS test. Email delivery, external providers and real passkey devices need configuration/testing. Some Identity template recovery/account paths remain uncovered. SQLite uses last-write-wins editing; no production SLA or large-scale capacity is claimed.

These limits do not prevent the local academic CRUD/budget/dashboard demonstration. The examiner decides the grade and the student must complete the mandatory defense.


## Account deletion verification

The account settings deletion form requires the current password and an antiforgery token. It removes all transactions, budgets and categories across all months, plus the Identity account and cascading authentication records. On success the browser signs out and returns to the landing page.

Five new integration tests verify all-month removal and preservation of another owner, invalid-password preservation, an injected failure during Identity deletion with full finance rollback and successful retry, empty-account deletion, and stale-version rejection. They also verify claims/logins/tokens are removed, the old login is rejected, the same email can register a new empty account, and a stale tab cannot recreate categories for the deleted owner.

Deletion uses one SQLite ATTACH transaction with DELETE journals in both files; runtime failures roll back the operation. This follows the [SQLite attached-database transaction requirements](https://www.sqlite.org/lang_attach.html). The service rejects an incompatible journal configuration. A minimal DeletedAccounts marker retains only the opaque account ID to prevent stale circuits recreating records; it contains no email, credentials or financial data. Deletion removes live records, not previously exported CSV files, backups or forensic disk remnants.
