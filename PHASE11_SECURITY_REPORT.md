# Phase 11 security and delivery report

Date: 2026-11-02. Scope: source review, package audit and automated tests without connecting to a SQL Server. See also `PHASE10_TEST_REPORT.md`.

## Baseline and classification

Initial restore/build succeeded with vulnerability warnings; tests: 21 passed, 5 skipped. The five SQL-dependent tests were unavailable because no suitable SQL Server connection was configured.

| Classification | Finding at baseline |
| --- | --- |
| Confirmed vulnerable dependency | Transitive Pkcs 9.0.4 had NuGet advisory findings; application-specific exploitability was not established. |
| Security misconfiguration | A development JWT signing key was tracked in configuration and Git history. |
| Confirmed functional/security defects | Unexpected `InvalidOperationException` became 409; exception messages could reach clients and unexpected exceptions lacked sufficient error logging. |
| Existing limitation | There is no reliable authenticated-user-to-expert mapping or public closure route. |
| Unverified risks | Real SQL Server concurrency, rollback, multi-instance operation, crash/restart and queue performance. |
| Previously addressed in source | Worker uses fresh scopes for queue pages and assignment attempts; retries are bounded, cancellation is propagated, and cycle processing is sequential. Actual database behavior still requires SQL tests. |

## Dependency audit

`dotnet list OrganizationRequests.sln package --vulnerable --include-transitive --no-restore` identified `System.Security.Cryptography.Pkcs` 9.0.4 through `Microsoft.EntityFrameworkCore.SqlServer 10.0.7 -> Microsoft.Data.SqlClient 6.1.1 -> System.Security.Cryptography.Pkcs 9.0.4` (confirmed through `project.assets.json`). Advisories: [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) (high) and [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) (moderate). Neither finding alone proves an exploitable application path.

A direct Infrastructure reference to Pkcs **9.0.21**, a patched release in the same major version, overrides the transitive dependency without broad upgrades. After restore the resolved version is 9.0.21, and the same NuGet audit reports no vulnerable packages for all six projects. The audit reflects available NuGet advisory data at execution time and does not audit the operating system or installed runtime.

## Authentication and configuration

The development key was removed from tracked settings. API supports development User Secrets and normal configuration providers; issuer and audience sample values remain development-only. Production must supply a strong, randomly generated signing key, issuer and audience securely. The application rejects missing values and keys shorter than 32 UTF-8 bytes, but length alone does not prove entropy. Because the old value remains in Git history, rotate it in every environment that used it; no history rewrite or external rotation was performed.

JWT validation requires a signed, unexpired token, validates signing key, issuer, audience and lifetime with a 30-second skew, and permits HMAC SHA-256/384/512. Identity comes from the numeric `user_id` claim. Token issuance is outside this solution. Configured secrets and tokens should not appear in logs.

## Exceptions, authorization and logging

Typed not-found, access-denied, request-key conflict, invalid argument and EF concurrency cases return generic 404, 403, 409, 400 and 409 responses respectively. Other exceptions, including unexpected `InvalidOperationException`, return generic 500 and a trace identifier; the server logs the exception, request method, path and trace identifier. Request bodies, query strings and authorization headers are not included by this middleware. Exception objects themselves can contain sensitive information, so log storage requires access controls.

Registration and retrieval require authentication. Registration uses the current identity; replay requires the same owner and matching input. Retrieval returns 404 for another user's record. No public closure endpoint was added: internal closure must remain internal until a trusted identity-to-expert mapping and ownership authorization exist. Workers use a system current-user context independently of HTTP/JWT; audit value 0 is a system convention, not an identified person.

Worker cycle logs now include a cycle identifier. Worker is registered through `AddHostedService`; it captures the scope factory and options rather than scoped application services. Each assignment attempt creates a scope; only concurrency outcomes are retried, with bounded cancellation-aware delays. Database/timeout failures delay the next cycle; unexpected failures are logged and propagated. The Worker configuration registration was extracted without changing its rules so startup validation can be tested. Actual fresh DbContext behavior with SQL and crash recovery remain unverified.

## Tests and verification

Final restore: **passed**. Build: **passed, 0 warnings and 0 errors**. `dotnet test OrganizationRequests.sln --no-build --no-restore`: **35 passed, 5 skipped, 0 failed**. SDK 10.0.403 was used from a temporary local installation; no application process or production database was run.

| Test group | Expected and actual | Result |
| --- | --- | --- |
| `ApiSecurityTests.Authentication_rejects_missing_invalid_expired_issuer_and_audience_tokens` | Missing/invalid JWT rejected with 401. | Passed |
| `ApiSecurityTests.Valid_token_can_read_own_request_but_not_another_users_or_close_it` | Owner reads; other user and absent closure route return 404. | Passed |
| `ApiSecurityTests.Unexpected_exceptions_are_safe_and_logged_with_trace_context` | Unexpected exceptions yield generic 500, trace and error log. | Passed |
| `ApiSecurityTests.Explicit_conflict_stays_409_and_input_errors_do_not_echo_messages` | Explicit conflict is 409; argument error is generic 400. | Passed |
| `ApiSecurityTests.Missing_or_invalid_security_settings_fail_startup` | Missing/short key, issuer or audience fail startup. | Passed, four cases |
| `WorkerConfigurationTests` | Invalid polling, retry, batch and aging settings fail host startup; valid defaults start. | Passed, six cases |
| Existing registration, expert selection, closure, migration metadata and Worker tests | Fake/in-memory regressions including retries, scope disposal, cancellation, non-overlap and failure handling. | Passed |
| `RegistrationQueueSqlTests` | Registration races and queue page consistency. | Not executed: SQL unavailable |
| `AssignmentRollbackSqlTests` | Database transaction rollback. | Not executed: SQL unavailable |
| `SqlServerConcurrencyTests` | Assignment concurrency and capacity. | Not executed: SQL unavailable |
| `SqlServerClosureTests` | Concurrent closure and capacity release. | Not executed: SQL unavailable |
| `QueuePerformanceSqlTests` | Queue timing at 100 and 10,000 pending records. | Not executed: SQL unavailable |

API tests run an in-memory HTTP server with a fake request repository and synthetic credentials; they do not prove SQL behavior. Existing Worker tests prove separate scopes with test doubles; they do not exercise a real SQL DbContext on each retry. The test for closure verifies that no HTTP endpoint exists, rather than testing an authorization policy on an endpoint. Run the five SQL tests only against the dedicated migrated test database described in README.

## Remaining risks and delivery status

- **Fixed and verified:** NuGet advisory findings listed above, tracked current development key, unsafe unexpected-error responses, insufficient unexpected-error logging, JWT security validation and Worker configuration validation.
- **Confirmed unresolved:** historical signing key exposure requires rotation; user-to-expert authorization mapping is absent, so no closure endpoint is exposed.
- **Implemented but not verified in real SQL:** preservation of transactional assignment/closure and multi-instance behavior; no business algorithms were changed in this phase.
- **Unverified:** SQL performance, real concurrent integrity and crash/restart. No SQL Server or test connection was available.
- **Not implemented:** identity-to-expert mapping, public closure endpoint, production secret rotation and external monitoring; they require deployment or business context. Production readiness is not asserted.

## Files changed

- `src/OrganizationRequests.Infrastructure/OrganizationRequests.Infrastructure.csproj`: patched dependency override.
- `src/OrganizationRequests.Api/OrganizationRequests.Api.csproj`, `Program.cs`, `Security/ApiExceptionMiddleware.cs`, `appsettings.Development.json`: development secrets, JWT validation, safe error handling and logging.
- `src/OrganizationRequests.Worker/Program.cs`, `WorkerConfiguration.cs`, `Workers/AssignmentWorker.cs`: testable unchanged startup validation and cycle correlation.
- `tests/OrganizationRequests.Tests/OrganizationRequests.Tests.csproj`, `Integration/ApiSecurityTests.cs`, `WorkerConfigurationTests.cs`: HTTP security and Worker configuration tests.
- `README.md`, `PHASE11_SECURITY_REPORT.md`: setup and delivery documentation.
