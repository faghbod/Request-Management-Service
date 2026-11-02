# Offline test results

## Environment

- SDK: .NET 10.0.403 (temporary local SDK).
- Test project: OrganizationRequests.Tests, xUnit 2.9.3, xunit.runner.visualstudio 3.1.1, Microsoft.NET.Test.Sdk 17.14.1, Microsoft.AspNetCore.Mvc.Testing 10.0.7.
- NuGet packages came from the existing temporary local cache. Restore was not run; no online package advisory audit was run.
- Test hosts use in-memory HTTP and fake dependencies; a dummy SQL connection string is configured where needed but no connection is opened.

## Commands and observed results

Commands below use `dotnet` from the temporary SDK; DOTNET_ROOT, DOTNET_CLI_HOME and NUGET_PACKAGES were set to the existing local SDK/home/cache.

| Stage | Command | Outcome |
|---|---|---|
| Baseline SDK | `dotnet --version` | 10.0.403 |
| Baseline build | `dotnet build --no-restore` | success, 0 warnings, 0 errors |
| Baseline regression | `dotnet test --no-build --no-restore --filter "Category=Unit"` | 89 passed, 0 failed, 0 skipped |
| Red test: error classification | `dotnet test --no-restore --filter "FullyQualifiedName~Explicit_conflict_stays_409"` | 1 failed as expected before fix: expected 500, got 400 |
| Intermediate regression | `dotnet test --no-restore --filter "Category=Unit"` | 92 passed, 0 failed, 0 skipped |
| Red test: closure disclosure | `dotnet test --no-restore --filter "FullyQualifiedName~Unrelated_expert_cannot_distinguish"` | 1 failed as expected before fix: unrelated expert raised AccessDeniedException |
| Additional regression before final test | `dotnet build --no-restore` then `dotnet test --no-build --no-restore --filter "Category=Unit"` | build: 0 warnings, 0 errors; 93 passed, 0 failed, 0 skipped |
| Final build | `dotnet build --no-restore` | success, 0 warnings, 0 errors |
| Final regression | `dotnet test --no-build --no-restore --filter "Category=Unit"` | 94 passed, 0 failed, 0 skipped |
| SQL test discovery only | `dotnet test --no-build --no-restore --filter "Category=SqlServer" --list-tests` | 6 test cases listed; none executed |

The final 94 include data-driven test cases. The two deliberately failing runs are pre-fix regressions, not final-suite failures. Existing tests cover registration/idempotency, selection, priority queue orchestration, worker scopes/retries/cancellation, identities and API authorization, request history, management and change-history snapshots, EF metadata and mapping. New tests cover exception/cancellation handling, container scopes, nondisclosing closure and management/selection interaction using fakes.

The category filter intentionally excluded all 6 discovered SQL Server test cases, including the performance test; they were **not executed**, rather than skipped by the runner. They require a dedicated configured SQL Server. There was no SQL connection, migration application, database creation/reset, live application deployment, real issuer validation or package-feed vulnerability scan. A fake repository cannot establish SQL transaction correctness, SQL query behavior or RowVersion behavior. The offline model snapshot and generated migration SQL checks do not establish applied database schema correctness.