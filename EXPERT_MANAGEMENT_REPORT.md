# Code Improvement 7 — Expert management

## Implemented

`IExpertManagementService` and `ExpertManagementService` provide `CreateExpertAsync`, `GetExpertByIdAsync`, `GetExpertsAsync`, `UpdateExpertAsync` and `SetExpertActiveStatusAsync`. DTOs do not accept ID, audit fields, rowversion on creation or active-request count; unknown JSON fields are rejected. Names are trimmed and must be 1–150 characters, capacity must be positive and at least the current active count, specialties must be valid and unique, and an active expert must have at least one specialty. Status is explicitly supplied. Deactivation/specialty changes do not alter existing requests or the shared active count. No deletion, expert configuration history, assignment algorithm change, domain entity modification or migration was introduced.

`IExpertRepository`/`ExpertRepository` add tracked management retrieval with specialties, no-tracking detail and keyset list (`afterId`, deterministic ID order, optional active and subject filters), add, update preparation and cleanup. Listing takes at most 100 records plus one to determine the next cursor. Update/status requests require an eight-byte Base64 SQL Server rowversion. Stale loaded versions and EF optimistic-concurrency failures become `ExpertConcurrencyException` and HTTP 409. The tracked parent receives the client's original RowVersion and is explicitly marked for an UPDATE even when only specialty rows change. Parent and child changes are saved inside the same UnitOfWork transaction; no client DTO is attached as an expert. Child rows retain their composite key.

`ExpertsController` exposes POST and GET `/api/experts`, GET and PUT `/api/experts/{id}`, and PATCH `/api/experts/{id}/status`. All endpoints, including reads, require the dedicated `ExpertsManage` policy. Anonymous callers receive 401, other authenticated callers receive 403, invalid inputs 400, unknown ID 404 and version conflicts 409. Unknown JSON properties are rejected. No public expert-history endpoint existed or was changed.

## Authorization blocker

The existing system has no confirmed administrator permission source. The policy accepts only a validated JWT identity containing the exact claim `permission=experts.manage`, and only when `Jwt:TrustExpertManagementPermission` is explicitly enabled. The flag defaults to false. Before enabling, operators must verify the trusted issuer controls the claim and binds it to authorized administrators; granting a claim or setting this flag does not prove that contract. Expert identity and authentication alone do not grant management permission. No permission-issuing endpoint was added.

## Offline verification and limitations

Build succeeded with 0 warnings and 0 errors. Unit category: **78 passed, 0 failed**. Tests use fakes, EF change tracking without a database, and an in-memory HTTP host. They cover validation, capacity boundaries, active count and request preservation, activation, specialties, malformed/stale versions, fake EF conflict rollback, paging, authorized and unauthorized HTTP calls, unknown fields, and existing assignment/closure/worker regression tests. The EF tracking test verifies the parent is marked modified, original RowVersion is supplied, and child changes are tracked; it does not execute SQL.

No SQL Server connection, SQL integration test, migration or database operation was run. Correct SQL Server command ordering, atomic rollback, true RowVersion races with Worker assignment/closure, deadlock behavior, and query performance require verification against a disposable SQL Server database before production use. The existing expert-claim trust blocker and migration rollout caveats remain applicable.

## Files created

- `src/OrganizationRequests.Application/DTOs/ExpertManagementDtos.cs`
- `src/OrganizationRequests.Application/Exceptions/ExpertManagementExceptions.cs`
- `src/OrganizationRequests.Application/Services/IExpertManagementService.cs`, `ExpertManagementService.cs`
- `src/OrganizationRequests.Api/Controllers/ExpertsController.cs`
- `tests/OrganizationRequests.Tests/Unit/ExpertManagementTests.cs`, `ExpertManagementTrackingTests.cs`
- `EXPERT_MANAGEMENT_REPORT.md`

## Files modified

- `src/OrganizationRequests.Application/Repositories/IExpertRepository.cs`, `DependencyInjection.cs`
- `src/OrganizationRequests.Infrastructure/Persistence/Repositories/ExpertRepository.cs`
- `src/OrganizationRequests.Api/Program.cs`, `Security/ApiExceptionMiddleware.cs`
- `tests/OrganizationRequests.Tests/Unit/ExpertSelectionTests.cs`, `RequestClosureServiceTests.cs`, `Integration/ApiSecurityTests.cs`
- `README.md`
