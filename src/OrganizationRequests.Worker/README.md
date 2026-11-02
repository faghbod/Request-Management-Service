# Assignment worker

This independently hosted process uses the existing application and infrastructure registrations. Set `ConnectionStrings__DefaultConnection` through the environment or another standard .NET configuration provider. Apply migrations as a separate deployment step. The worker does not run migrations or create a database.

`AssignmentWorker` settings are `PollingIntervalSeconds` (30), `MaxAssignmentAttempts` (3, including the first attempt), `InitialRetryDelayMilliseconds` (100), and `MaxRetryDelayMilliseconds` (1000). The existing `QueueProcessing` section configures `BatchSize` (50) and `AgingThresholdMinutes` (30). Configuration is validated when the host starts.

Each cycle starts immediately after startup or after the delay following the previous cycle. It captures one UTC scan time and reads pages in short-lived scopes. Each assignment attempt, including retries, runs in a separate scope. Only reported concurrency conflicts are retried, with bounded exponential backoff. Database and timeout failures end the cycle and wait for the polling interval; unexpected programming errors are logged and propagate to the host. Cancellation is passed to database operations and delays.

The system current-user context returns no user ID. Existing audit behavior consequently writes `0` for system updates; this is the existing unauthenticated actor convention, not a new user relationship. Review this convention before using audit values for user attribution.

Run the tests with `dotnet test`. The SQL Server integration test is skipped unless `ORGANIZATIONREQUESTS_TEST_SQL` is set. It writes test records and intentionally does not delete them. Use a dedicated empty database whose name starts with `OrganizationRequests_Test_`, with the existing migrations already applied; never point it at a real database. The test does not create, migrate, reset or drop a database. SQL Server concurrency, process crash/restart and large-queue performance remain unverified until separately tested in a suitable environment.
