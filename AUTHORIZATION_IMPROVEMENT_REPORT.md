# Code Improvement 1 — Identity and request-level authorization

## Behavior

`ICurrentUserContext` exposes separate `UserId` and `ExpertId`. The Worker returns null for both and assignment remains independent of HTTP/JWT. API reads `expert_id` only from a single authenticated identity when `Jwt:TrustExpertIdClaim` is explicitly true. Exactly one claim must contain a positive 32-bit integer; missing, malformed, nonpositive, duplicate, or ambiguous identities fail closed. An unsigned or invalid JWT cannot authenticate. `user_id` is also read only from an authenticated identity.

A request's registering user or its assigned expert can read the request and its request-history endpoint. Other authenticated users receive 404. Idempotent request-key replay remains restricted to the original registering user. `RequestClosureService` requires a valid user ID and matching positive expert ID before disclosing request state, including AlreadyClosed or InvalidState; it rechecks after a lost conditional update. The original transactions, concurrency handling and history actor are retained. No new public endpoint, role, schema, migration, assignment rule or token issuer was added.

## Issuer prerequisite

The real identity provider and its claim issuance policy are not available in this repository. Expert claim trust defaults to false. Only after verifying the existing trusted issuer controls the `expert_id` claim and binds it to the authenticated user may the deployer set `Jwt__TrustExpertIdClaim=true`. This configuration is a deployment attestation, not verification of the identity-provider contract. Signing keys and token issuance must be secured; if the contract cannot be guaranteed, leave the setting disabled. The request owner can still read their own requests. There is no public closure or expert-history endpoint in this phase.

## Verification

.NET SDK 10.0.403: solution build succeeded with 0 warnings and 0 errors; `dotnet test OrganizationRequests.sln --no-build --no-restore --filter Category=Unit` passed **60/60** tests. These include claim parsing, missing/invalid/ambiguous/unauthenticated identities, opt-in trust, owner and expert reads, inaccessible history, assigned-expert closure, wrong/missing expert and closed-state protection, reauthorization after rollback, existing worker tests, and in-memory HTTP tests with valid and invalid signed JWTs. No SQL Server connection, migration or database operation was performed. SQL-specific test fixtures were adapted to provide the actual seeded expert identity but were not executed.

## Files modified

- `src/OrganizationRequests.Application/Abstractions/ICurrentUserContext.cs`
- `src/OrganizationRequests.Api/Security/HttpCurrentUserContext.cs`
- `src/OrganizationRequests.Application/Services/RequestRegistrationService.cs`, `RequestClosureService.cs`, `IRequestClosureService.cs`
- `src/OrganizationRequests.Worker/SystemCurrentUserContext.cs`
- `tests/OrganizationRequests.Tests/Unit/HttpCurrentUserContextTests.cs` (new), `Unit/RegistrationTests.cs`, `Unit/RegistrationAuditTests.cs`, `Unit/MigrationMetadataTests.cs`
- `tests/OrganizationRequests.Tests/RequestClosureServiceTests.cs`, `Integration/ApiSecurityTests.cs`, `Integration/AssignmentRollbackSqlTests.cs`, `Integration/RegistrationQueueSqlTests.cs`, `Integration/RequestHistorySqlTests.cs`, `Performance/QueuePerformanceSqlTests.cs`, `SqlServerClosureTests.cs`, `SqlServerConcurrencyTests.cs`
- `README.md`, `AUTHORIZATION_IMPROVEMENT_REPORT.md` (new).
