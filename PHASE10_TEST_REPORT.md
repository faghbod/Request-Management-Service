# Phase 10 — Test and concurrency report

## Status

**Phase 10 is not fully verified.** The solution builds and its database-free tests pass, but the mandatory SQL Server concurrency, transaction and performance tests could not be executed. Prepared tests are not evidence of a successful database run. No SQL Server, application host or Worker process was started for this report.

## Environment and baseline

| Item | Observed |
| --- | --- |
| OS / SDK | Windows; temporary .NET SDK 10.0.403 |
| EF Core SQL Server | 10.0.7 |
| Test framework | xUnit 2.9.3; Microsoft.NET.Test.Sdk 17.14.1 |
| Docker / SQL tools | Docker and sqlcmd commands unavailable; no MSSQL Windows service found |
| SQL Server version / database | Unknown; no explicitly configured disposable test connection was found |
| Baseline before changes | Restore and build succeeded; 13 tests passed, 2 SQL Server tests skipped |
| Final | Build succeeded: 0 errors, 8 warnings; 21 tests passed, 5 skipped, 0 failed |
| Build warnings | NU1902 and NU1903 for transitive System.Security.Cryptography.Pkcs 9.0.4, repeated per project |

The migration and snapshot exist. An executed offline test generates the migration SQL and checks for the lifecycle constraint, unique index and rowversion, and verifies no model changes are pending. This does **not** prove that the migration applies to an actual SQL Server. SQL tests check migration history and require an already migrated, empty dedicated database; they do not create, migrate, reset, or drop one.

## Scenario results

`Pass` below means the named test actually ran. SQL Server rows marked `Not Executed` describe prepared checks, not observed results. `Unit` uses fakes and is not evidence of SQL transaction or concurrency behavior.

| Scenario / test | Category | Expected | Actual / status |
| --- | --- | --- | --- |
| New registration, same-key replay, changed subject, replay after closure; `RegistrationTests` | Unit | One in-memory record; replay preserves status and ID; conflict throws | Pass with test doubles; no HTTP or SQL verification |
| Same key by another user; `RegistrationTests` | Unit | Conflict, no new record | Pass with test doubles |
| Twenty identical registrations, 20 distinct keys, replay after assignment and closure; `RegistrationQueueSqlTests` | SQL / concurrency | One record per key, all 20 identical requests replay consistently | Not Executed; deterministic initial-read gate prepared |
| Conflicting key HTTP response | HTTP / SQL | 409, unchanged record | Not Executed; no API integration client test was added |
| Ratio, count and ID tie breaks; `ExpertSelectionTests` | Unit | Lowest ratio, then count, then ID | Pass with supplied candidate list |
| Inactive, deleted, full and wrong-specialty exclusion; shared capacity | SQL | Filter ineligible experts and share capacity across specialties | Not Executed; checks prepared in `RegistrationQueueSqlTests` |
| Fresh scope on each Worker attempt and retry | Unit | Distinct scopes disposed between attempts | Pass using scope marker doubles; actual SQL DbContext freshness is unverified |
| 20 requests / capacity 5 and duplicate assignment; `SqlServerConcurrencyTests` | SQL / concurrency | 5 assigned, 15 pending, counter 5 | Not Executed |
| Same request with different winning candidates; last slot with capacity 5 and count 4; repeated stress | SQL / concurrency | One effective assignment, capacity never exceeded | Not Executed; dedicated deterministic cases are not yet implemented |
| Assignment exception after counter write, conditional update returns zero; `AssignmentRollbackSqlTests` | SQL / fault injection | Counter and request rolled back; later fresh attempt succeeds | Not Executed |
| Ambiguous commit / connection loss | SQL / recovery | A later fresh attempt reads persisted state before changes | Not Executed; deterministic connection-loss injection not implemented. Existing code does not blindly retry arbitrary exceptions |
| Successful and duplicate closure, invalid and missing states, inactive/full expert, zero counter, optimistic conflict, injected failure and lost conditional update; `RequestClosureServiceTests` | Unit | Correct result and simulated rollback | Pass with fakes; not SQL transaction proof |
| Concurrent same/different closures, closure vs assignment, SQL rowversion, closure rollback, Worker capacity reuse; `SqlServerClosureTests` | SQL / concurrency | Correct persisted lifecycle and counter | Not Executed |
| Aging at 30 minutes, urgent, FIFO / ID and 120 requests in pages of 50/50/20; `RegistrationQueueSqlTests` | SQL / queue | Stable reference time and exact expected order, no duplicates | Not Executed |
| First 50 requests have no expert; next page has assignable item | Worker unit | Continue to later page | Pass with fake pages and fake assignment service; SQL end-to-end unverified |
| New registrations during scan; starvation with sustained urgent arrivals | SQL / queue | Exclude records later than reference time; aged older items retain ordering | Not Executed; concurrent-arrival and sustained-arrival scenarios are not separately implemented. Prepared queue test checks exclusion of records created after its fixed reference time |
| Two independent Worker containers | SQL / concurrency | Consistent persisted assignment and capacity | Not Executed; test uses independent service providers, not separate OS processes |
| Worker process shutdown/restart during active database transaction | Process / SQL | Committed work survives; pending work resumes | Not Executed; process-level test not implemented |
| Bounded retries, unavailable expert, cancellation, no overlapping cycle | Worker unit | Bounded attempts, disposed scopes, no new work after cancellation | Pass with test doubles |
| Worker recoverable database error and unexpected programming error | Worker unit | A later cycle is possible after database error; programming error propagates | Pass with test doubles. Backoff duration itself was not independently measured |
| Offline migration SQL and model snapshot | Unit / migration | Generate expected schema SQL without pending model changes | Pass without a database; application of migration unverified |
| Queue benchmark 100 / 10,000 requests | SQL / performance | Measure paging commands/time and a small cycle | Not Executed; opt-in test prepared |

## Concurrency and invariants

The 20-request scenario has **no measured final Assigned count, Pending count or ActiveRequestCount**. Duplicate assignment, two-provider execution, SQL rowversion handling, rollback and capacity consistency remain unverified against SQL Server. The existing test uses two DI providers in one process, which cannot establish separate-process behavior.

Reusable `DatabaseInvariants.AssertAsync` is attached to the prepared assignment, closure, registration and fault-injection SQL tests. It checks capacity bounds, assigned-count equality, lifecycle fields and RequestKey uniqueness using no-tracking queries. It has not run against SQL Server. Shared capacity has a prepared integration scenario; sustained contention and process restart require additional tests.

The assignment service updates the expert before the request; closure takes an expert update lock before its conditional request update. This aims to reduce lock-order inversion but does not prove the absence of SQL Server deadlocks. Worker retries expected concurrency outcomes in fresh scopes. Unexpected SQL errors are not automatically retried. The unique-key classifier depends on the SQL error number and the index name appearing in its message; real concurrent registration and message behavior are unverified.

## Performance

No dataset was loaded and no measurements, execution plan, scan/sort findings, assignment throughput or multi-Worker throughput were obtained. The opt-in benchmark prepares 100 and 10,000 mixed-priority/subject requests, records page-reader command counts and elapsed paging time, and runs one 100-request cycle without eligible experts. It does not capture an execution plan, a 10,000-request Worker cycle, available-expert throughput or concurrent Workers. The calculated priority and existing index may cause sorting or scanning, but this is an **unverified risk**, not a measured bottleneck. No performance optimization was made.

## Confirmed issues and unresolved risks

- **Security:** dependency audit reports vulnerable `System.Security.Cryptography.Pkcs` 9.0.4 (NU1902 and NU1903). Actual exploitability through this application was not established.
- **API error handling:** source inspection confirms unexpected exceptions are not logged by `ApiExceptionMiddleware`, and generic `InvalidOperationException` is mapped to 409 with its message. This phase did not change that middleware.
- **JWT:** a development signing key is in development configuration. Reuse in production or deployment with Development settings is a security risk.
- **Closure authorization:** there is no trustworthy user-to-expert mapping or privileged role in the inspected model. The public close endpoint remains unavailable; no fake authorization was introduced.
- **Audit:** the existing unauthenticated/system convention records actor `0`; the meaning must be considered when designing production identity and audit requirements.
- **Infrastructure:** no disposable SQL Server was available. The SQL fixtures require separate empty databases per test, leave their test data in place, and require a fresh empty migrated database for another run. They do not automatically provision or clean databases. Multi-process testing and deterministic ambiguous-commit injection remain to be implemented.
- **Functional defects:** no new functional defect was reproduced by the executed tests, so no production-code fix was made. Passing fake-based tests does not establish database correctness.

## Reproduction

Commands below assume a .NET 10 SDK on PATH. On this machine the available temporary SDK was `%TEMP%\\organizationrequests-dotnet\\sdk\\dotnet.exe`, with temporary CLI home and NuGet cache under `%TEMP%\\organizationrequests-dotnet`.

```powershell
dotnet restore OrganizationRequests.sln
dotnet build OrganizationRequests.sln --no-restore
dotnet test OrganizationRequests.sln --no-build --no-restore --filter "Category=Unit"
dotnet test OrganizationRequests.sln --no-build --no-restore --filter "Category=SqlServer&Category!=Performance"
dotnet test OrganizationRequests.sln --no-build --no-restore --filter "Category=Concurrency"
dotnet test OrganizationRequests.sln --no-build --no-restore --filter "Category=Performance"
```

SQL tests require distinct, explicitly configured, empty databases named `OrganizationRequests_Test_*`, with the application's migration already applied. Each variable must point to its own database: `ORGANIZATIONREQUESTS_TEST_SQL`, `ORGANIZATIONREQUESTS_CLOSURE_TEST_SQL`, `ORGANIZATIONREQUESTS_PHASE10_SQL`, and `ORGANIZATIONREQUESTS_ASSIGNMENT_FAULT_SQL`. The benchmark additionally requires its own `ORGANIZATIONREQUESTS_PERF_TEST_SQL` and `ORGANIZATIONREQUESTS_RUN_PERFORMANCE=1`. Do not use a database containing important data; these tests insert data and do not clean it. Keep connection strings and secrets out of source control. After benchmarking unset the opt-in variable.

Actually executed in this environment: restore, solution build, all tests, and the Unit, SQL Server, Concurrency and Performance filters. Unit: 21 passed. SQL Server: 4 skipped. Concurrency: 4 skipped. Performance: 1 skipped. The complete run: 21 passed, 5 skipped, 0 failed.

## Changed files

- Added `tests/OrganizationRequests.Tests/Unit/ExpertSelectionTests.cs`, `RegistrationTests.cs`, and `MigrationMetadataTests.cs`.
- Added `tests/OrganizationRequests.Tests/Integration/RegistrationQueueSqlTests.cs`, `AssignmentRollbackSqlTests.cs`, and `DatabaseInvariants.cs`.
- Added `tests/OrganizationRequests.Tests/Performance/QueuePerformanceSqlTests.cs`.
- Modified `AssignmentWorkerTests.cs`, `RequestClosureServiceTests.cs`, `SqlServerConcurrencyTests.cs`, and `SqlServerClosureTests.cs` under `tests/OrganizationRequests.Tests` for categorization, Worker failure checks, migration-history checks and reusable invariants.
- Added this report. No production files or package versions were changed.
