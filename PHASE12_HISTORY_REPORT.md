# Phase 12 — Request and expert activity history

## Implemented

- A single append-only-in-normal-workflows `RequestActivityHistories` table, entity `RequestActivityHistory`, and enums `RequestActivityType` and `HistoryActorType` store registration, assignment and closure. No old `RequestStatusHistory` implementation or table was found in the source/migrations.
- Migration `20261102123752_AddRequestActivityHistory` creates the table without altering existing requests/experts or backfilling events. Existing requests can therefore have partial or empty histories. Migration `Down` drops the history table and its records; review backups and rollback plans before use. It was generated, not applied to a database here.
- Foreign keys to request and optional expert use NoAction. Indexes are `(ServiceRequestId, OccurredAtUtc, Id)` and `(ExpertId, OccurredAtUtc, Id)`. A unique `(ServiceRequestId, EventType)` enforces at most one event of each type in the current lifecycle; reassignment or reopening requires revisiting it. CHECK constraints enforce per-row event/status/expert combinations and actor representation, not cross-row chronology. The table is not protected against privileged direct SQL modifications.
- `IRequestActivityHistoryRepository` provides add and no-tracking request/expert reads, plus detaching unsaved history after failure. Request history sorts ascending by time and ID; expert history uses descending keyset pagination with max page size 100. Expert subject is read from its request and no expert active/specialty filter removes past activity.
- Scoped `IRequestHistoryService` prepares registration/assignment/closure records and maps request and expert history DTOs. Actor and associated expert are independent. Registration uses the authenticated user and a tracked request navigation, with the DbContext synchronizing the event to the exact creation audit timestamp before a single SaveChanges. An idempotent replay creates no new event.
- Assignment uses the system actor, null user and the exact assignment timestamp; the history insert is saved inside the existing transaction after the conditional request update. Closure uses the actual nonzero current user ID, the assigned expert and the exact closure timestamp; it saves in the same existing transaction as status and counter changes. Pending history is detached on failure, without altering committed history. The internal closure caller remains responsible for authorization.
- `GET /api/requests/{requestId}/history` is authenticated and reuses request ownership checks, returning 404 to nonowners. It omits actor user IDs; enum serialization follows current numeric API conventions. Internal expert-history retrieval exists; no public expert endpoint was exposed because the current model has no reliable user-to-expert mapping or existing admin policy. No public closure endpoint was added.

## Verification

Restore and build succeeded with **0 warnings and 0 errors**. Full automated suite: **42 passed, 6 skipped, 0 failed**. Tests use .NET SDK 10.0.403; no SQL Server or independent application process was started. HTTP authorization tests use an in-memory host and fake repositories; EF save-interceptor tests verify timestamp synchronization and tracking without a database. Unit tests cover event preparation, user/expert distinction, idempotent replay, closure actor validation, mock request ordering, cursor tie handling and validation, as well as generated migration SQL/model consistency.

The new SQL test `RequestHistorySqlTests.Committed_events_are_atomic_idempotent_and_queryable_across_concurrent_operations` is implemented but **not executed**. It checks duplicate registration/assignment/closure, assignment/closure rollback on injected history failure, event counts and timestamp alignment, inactive expert history and pagination. Existing SQL concurrency tests were extended to check committed history counts, but they also did not run. All six SQL-dependent tests remain unverified: SQL history, assignment concurrency, closure concurrency, registration/queue races, assignment rollback and performance. Real SQL Server transaction, rowversion and multi-worker behavior must not be inferred from fake/in-memory tests. SQL insert failure during SaveChanges and equal-timestamp SQL pagination should receive additional database integration coverage.

To run the history SQL test, supply `ORGANIZATIONREQUESTS_HISTORY_TEST_SQL` pointing to its **own empty, migrated, disposable** database named `OrganizationRequests_Test_*`, then run `dotnet test --filter "FullyQualifiedName~RequestHistorySqlTests"`. It writes data and does not delete it. Existing SQL suites require their own empty databases and environment variables as documented in their test attributes. Do not point any test at production.

## Files created

- Domain: `Entities/RequestActivityHistory/RequestActivityHistory.cs`, `Entities/Enums/RequestActivityType.cs`, `Entities/Enums/HistoryActorType.cs`.
- Application: `DTOs/ExpertHistoryPage.cs`, `DTOs/RequestHistoryItemDto.cs`, `Repositories/IRequestActivityHistoryRepository.cs`, `IServiceContracts/IRequestHistoryService.cs`, `Services/RequestHistoryService.cs`.
- Infrastructure: `Persistence/Configurations/RequestActivityHistoryConfiguration.cs`, `Persistence/Repositories/RequestActivityHistoryRepository.cs`, `Persistence/Migrations/20261102123752_AddRequestActivityHistory.cs` and its `.Designer.cs`.
- Tests: `TestHistoryRepository.cs`, `Unit/RegistrationAuditTests.cs`, `Unit/RequestHistoryTests.cs`, `Integration/RequestHistorySqlTests.cs`.
- `PHASE12_HISTORY_REPORT.md`.

## Files modified

- API: `Controllers/RequestsController.cs`.
- Application: `DependencyInjection.cs`, `Services/RequestRegistrationService.cs`, `Services/RequestAssignmentService.cs`, `Services/RequestClosureService.cs`.
- Infrastructure: `DependencyInjection.cs`, `Persistence/ApplicationDbContext.cs`, `Persistence/Repositories/ServiceRequestRepository.cs`, `Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`.
- Tests: `Integration/ApiSecurityTests.cs`, `Integration/AssignmentRollbackSqlTests.cs`, `Integration/RegistrationQueueSqlTests.cs`, `RequestClosureServiceTests.cs`, `SqlServerClosureTests.cs`, `SqlServerConcurrencyTests.cs`, `Unit/MigrationMetadataTests.cs`, `Unit/RegistrationTests.cs`.
- `README.md`.

## Remaining limitations

Production expert-history authorization and an authorized public closure workflow need a trusted identity-to-expert relationship or explicit administrative policy. Until available, neither endpoint is exposed. Migration rollout requires a coordinated deployment of compatible API/Worker versions: old binaries do not write events and will create gaps while running. SQL concurrency and database migration application remain unverified in this environment. No production readiness claim is made.
