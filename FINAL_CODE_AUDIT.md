# Final Code Audit — Code Improvement 9

## Initial implementation inventory (before code changes)

Baseline: .NET SDK 10.0.403, solution build succeeded with 0 warnings and 0 errors; 89 Category=Unit tests passed, 0 failed, 0 skipped. Cached packages were used with --no-restore. No database was contacted.

| Feature | Interface / implementation | DI / model / repository | Endpoint / tests | State |
|---|---|---|---|---|
| Registration and retrieval | IRequestRegistrationService / RequestRegistrationService | scoped; ServiceRequest; IServiceRequestRepository | POST and GET /api/requests; RegistrationTests, ApiSecurityTests | implemented; SQL verification pending |
| Selection | IExpertSelectionService / ExpertSelectionService | scoped; Expert and ExpertSpecialty; IExpertRepository | internal; ExpertSelectionTests | implemented; SQL eligibility verification pending |
| Aged queue | IPendingRequestQueueService / PendingRequestQueueService | scoped; ServiceRequest; IServiceRequestRepository.GetPendingPageAsync | worker; AssignmentWorkerTests | implemented; SQL translation/performance pending |
| Assignment | IRequestAssignmentService / RequestAssignmentService | scoped; ServiceRequest, Expert; conditional request and tracked expert repositories | worker; AssignmentWorkerTests and SQL-only tests | implemented; transaction/concurrency unverified |
| Closure | IRequestClosureService / RequestClosureService | scoped; ServiceRequest, Expert, request history repositories | internal only, no HTTP closure endpoint; RequestClosureServiceTests | partially exposed; SQL verification pending |
| Request history | IRequestHistoryService / RequestHistoryService | scoped; RequestActivityHistory; IRequestActivityHistoryRepository | GET /api/requests/{id}/history; RequestHistoryTests, ApiSecurityTests | implemented; SQL verification pending |
| Expert activity history | IRequestHistoryService.GetExpertHistoryAsync | scoped; RequestActivityHistory; GetByExpertIdAsync | internal only, no /api/experts/{id}/history; RequestHistoryTests | partially exposed; authorization required before exposure |
| Expert management | IExpertManagementService / ExpertManagementService | scoped; Expert, ExpertSpecialty; IExpertRepository | five admin routes; ExpertManagementTests, ApiSecurityTests | implemented; issuer trust and SQL verification pending |
| Expert configuration history | IExpertChangeHistoryService / ExpertChangeHistoryService | scoped; ExpertChangeHistory; IExpertChangeHistoryRepository | GET /api/experts/{id}/changes; ExpertManagementTests, ApiSecurityTests | implemented; issuer trust and SQL verification pending |
| Worker | AssignmentWorker / BackgroundService | singleton hosted service with async scopes; scoped queue and assignment services | internal; AssignmentWorkerTests, WorkerConfigurationTests | implemented; multi-instance SQL verification pending |
| Authentication / authorization | HttpCurrentUserContext, JWT bearer and ExpertsManage policy | scoped current user; trusted claim settings | controllers; HttpCurrentUserContextTests, ApiSecurityTests | implemented; expert and admin claim trust disabled pending issuer contract |

The original inventory identified no HTTP closure route and no expert activity-history route; neither is added in this audit because the phase prohibits new features. The independent expert-configuration changes endpoint exists. Initial middleware review identified untyped ArgumentException mapping and cancellation handling as candidates for focused regression tests.
## Executive summary

The simplified layered design is coherent and the offline suite passes, but this is not a production-readiness certification. The baseline inventory above is grounded in the code, not earlier reports. The critical 20-requests/capacity-five scenario and transaction, migration and real multi-instance behavior remain unverified. Two expected HTTP endpoints are absent. No critical defect was established without SQL Server.

## Findings

| ID / severity | Project and location | Expected; actual before audit; cause | Action and status |
|---|---|---|---|
| F01 / Medium | Api, Security/ApiExceptionMiddleware.cs, InvokeAsync | Unexpected ArgumentException should be logged and return safe 500; middleware mapped every ArgumentException to 400, masking server defects. | Fixed with explicit RequestValidationException for registration and 500 for unexpected exceptions. Regression demonstrated 400 before fix and 500 after; valid input errors remain 400. |
| F02 / Medium | Application, Services/RequestClosureService.cs, CloseAsync | An unrelated expert should not distinguish an existing request from a missing request. Previously mismatch threw AccessDeniedException while missing returned RequestNotFound. The same leak existed after a lost conditional update. | Fixed both paths to return RequestNotFound with no expert data. Missing or invalid caller identity still fails closed. A regression failed before the fix and passes after it. The service is currently internal, not exposed by an HTTP route. |
| F03 / Low | Api, Security/ApiExceptionMiddleware.cs, InvokeAsync | Client cancellation should not trigger response writing. Previously canceled operations were treated as unexpected errors whenever the response had not started. | Fixed: suppress error response only when RequestAborted is canceled; unrelated OperationCanceledException remains a logged 500. Focused tests added. |
| F04 / High | Api, Controllers/RequestsController.cs | Expected closure endpoint; none exists, so clients cannot invoke the implemented IRequestClosureService through HTTP. | Open. New functionality is outside this audit's restrictions; define HTTP mapping and authorized access in a later phase. |
| F05 / Medium | Api, Controllers/ExpertsController.cs; Application, RequestHistoryService.GetExpertHistoryAsync | Expected expert request-activity history API; a paginated internal read exists but no public /history endpoint or authorized entry point exists. | Open. Establish and test a trusted access policy before exposing it. /changes is a separate administrative configuration history. |
| F06 / High | Infrastructure migrations and assignment/closure repositories; Worker | Actual multi-instance safety and schema compatibility require SQL Server. Code review and fake repositories cannot prove atomicity, locks or generated-key/RowVersion behavior. | Open; follow DATABASE_VERIFICATION_BACKLOG.md, especially the 20/5 test. Do not release until verified. |
| F07 / Medium | Api, Program.cs and Security/HttpCurrentUserContext.cs | Expert/administrative claims must be controlled by a trusted issuer. Both trust switches are false by default; the production issuer contract is not established here. | Open external dependency: validate issuer/claim ownership before enabling either switch. Existing JWT signature, issuer, audience and lifetime checks remain enabled. |
| F08 / Medium | Repository history / security operations | A JWT signing secret was previously committed in Git history. Current checked-in appsettings contain no active signing key or database password. | Open operational action: rotate every previously used key, revoke affected tokens and review exposure; do not rewrite history automatically. Never reproduce the key. |
| F09 / Low | Api, Security/HttpCurrentUserContext.cs UserId; Application, RequestRegistrationService.GetUserId and RequestClosureService.CloseAsync | User IDs currently accept nonzero negative numbers and the first authenticated user_id claim; expert IDs use stricter positive/unique parsing. Issuer semantics for user IDs are not documented as strictly positive/unique. | Open contract decision: verify issuer guarantees, then normalize user_id validation consistently with migrations and existing data. Expert-management event creation already requires positive actor IDs. |
| F10 / Low | Integration/ApiSecurityTests.cs, ApiFactory.CreateHost | Tests temporarily modify process-wide environment variables and restore them in finally. Parallel execution with other environment-sensitive tests could interfere. | Open test-isolation improvement; current tests passed, but the risk is not evidence of an actual failure. |
| F11 / Medium | README / Infrastructure package references | Vulnerability audit requires current package-advisory feeds. Packages and current configuration were inspected, but no online dependency vulnerability scan was performed. | Open; run a current package audit with trusted feeds before release. |
| F12 / Low | ExpertManagementService.UpdateExpertAsync and SetExpertActiveStatusAsync | No-op checks the loaded RowVersion and returns without a write/lock; concurrent changes after the read can make the returned view stale. | Documented expected consequence of no-write behavior; SQL concurrency tests remain required. |

## Fixes and tests

- Registration uses a specific application validation exception and the middleware no longer treats arbitrary ArgumentException as a client mistake. API test verifies 409 conflict, redacted logged 500 for injected ArgumentException, and 400 for invalid registration.
- Middleware avoids writing for a canceled request. Tests cover canceled request and an unrelated cancellation.
- Closure returns a nondisclosing not-found outcome for unrelated experts both initially and after re-reading a concurrent update. Tests cover missing, assigned and closed requests and the post-rollback access check.
- Added offline DI graph validation with scope validation and no database connection, and a fake-repository cross-feature test for specialty removal, preserved assignment and deactivation.
- Existing metadata tests generate migration SQL and check model snapshot without connecting. This verifies metadata generation only, not migration execution.

## Architecture, DI and source review

Domain has no project references. Application references Domain, not Infrastructure or API. Infrastructure references Application and Domain and implements the repository/UoW abstractions. API and Worker are composition roots. Controllers delegate to services and contain no DbContext access. There is one DbContext, no CQRS, MediatR, event sourcing, or additional broker. No circular project references were found. Application and persistence services, repositories, UoW and DbContext are scoped. Worker is a hosted singleton and creates asynchronous scopes for each queue page and assignment attempt, including retries; the added container test enables ValidateOnBuild and ValidateScopes. It does not verify connectivity. The common DI graph was resolved; API-specific authentication/host wiring is additionally exercised through the in-memory API host.

Registration uses a unique RequestKey mapping, validates replay ownership and payload, creates PendingAssignment and prepares history in the same SaveChanges. Selection uses eligible repository candidates, integer cross-multiplication for load ratios and deterministic ties. Queue ordering uses a fixed scan time, aging cutoff, ranked keyset and continued pages. Worker has bounded retries, fresh scopes, cancellation and sequential cycles; no automatic parallelization was added. Static review found conditional request transitions, concurrency-token expert updates, transaction boundaries and history insertion before commit in assignment and closure; actual lock order, query translation and rollback require SQL Server.

Management validates name, positive capacity, current active-count floor, specialties, active status, actor and RowVersion; it uses tracked experts and forces a parent update for specialty changes. Configuration events use complete snapshots, sorted numeric specialty JSON, positive actor and shared UoW. The model snapshot reports no pending model changes in an offline metadata test; AddExpertChangeHistory exists but has not been applied. No-op version checking is read-time only. Request history hides ActorUserId in its DTO; internal expert activity includes actor identifiers and requires a future authorization policy. Application-level history is append-only, not tamper-proof.

## HTTP endpoint inventory

All routes use JWT authentication. Expert routes additionally require trusted experts.manage; this fails closed by default. MVC model binding can return 400; unexpected failures return safe 500.

| Method and route | Service / response | Authorization and expected statuses |
|---|---|---|
| POST /api/requests | IRequestRegistrationService / RequestResponseDto | authenticated; 201 created, 200 replay, 400 invalid, 403 missing identity, 409 conflict, 401 anonymous |
| GET /api/requests/{id} | IRequestRegistrationService / RequestResponseDto | owner or trusted assigned expert; 200, 404 for unrelated/missing, 401 |
| GET /api/requests/{id}/history | registration authorization then IRequestHistoryService / RequestHistoryItemDto[] | owner or trusted assigned expert; 200, 404, 401 |
| POST /api/requests/{id}/close | no controller route | missing, even though internal closure service exists |
| POST /api/experts | IExpertManagementService / ExpertResponseDto | ExpertsManage; 201, 400, 401, 403 |
| GET /api/experts/{id} | IExpertManagementService / ExpertResponseDto | ExpertsManage; 200, 400, 404, 401, 403 |
| GET /api/experts | IExpertManagementService / ExpertManagementPage | ExpertsManage; 200, 400, 401, 403 |
| PUT /api/experts/{id} | IExpertManagementService / ExpertResponseDto | ExpertsManage; 200, 400, 404, 409 stale, 401, 403 |
| PATCH /api/experts/{id}/status | IExpertManagementService / ExpertResponseDto | ExpertsManage; 200, 400, 404, 409 stale, 401, 403 |
| GET /api/experts/{id}/history | internal service only | missing; distinct from configuration changes |
| GET /api/experts/{id}/changes | IExpertChangeHistoryService / ExpertChangeHistoryPage | ExpertsManage; 200, 400, 401, 403; empty page for no events |

There are no duplicate route declarations or anonymous controller actions. [ApiController] performs binding and validation; DTOs, not EF entities, are returned. Authorization of request history is enforced by the controller's preceding request access check, not by RequestHistoryService itself, so any new caller must enforce authorization before use. Closure authorization is in the application service.

## Final status

Offline code/build/tests and generated EF metadata were verified as described in OFFLINE_TEST_RESULTS.md. No SQL Server was contacted, no migration applied, and no app or worker was run against a real database. No current dependency-advisory result is claimed. External claim contracts, prior-secret rotation, missing HTTP endpoints, actual SQL transactions/concurrency and operational rollout remain open.
## Prior improvement prompt coverage

The available attachments in this workspace identify Code Improvement 1 (identity and request-level authorization), 7 (expert management), and 8 (expert configuration history); their implemented behavior was reviewed above. The initial development attachments also cover phases 1–12. Separate Code Improvement 2–6 prompt texts were not found among the supplied attachments, so their individual acceptance criteria cannot be verified or represented as audited. The current source features were reviewed by feature instead. This limits any claim that all eight numbered improvement prompts were individually checked.