# Closure verification

`RequestClosureService` is an internal business operation. A caller must first authorize the user to close the specific request. The current model has no reliable user-to-expert mapping or existing privileged role, so no closure HTTP endpoint is exposed. Use a fresh DI scope for each closure attempt, including retries.

The closure transaction obtains an update lock on the expert before the conditional request update. Assignment updates the expert before updating the request; using that order reduces the lock-order inversion between these workflows. The request UPDATE, expert counter update and rowversion check use the same transaction. SQL Server can still encounter deadlocks for other reasons, and real multi-instance behavior needs integration testing. Unexpected database errors are not automatically treated as rowversion conflicts.

Run the regular tests with `dotnet test`. SQL Server closure verification requires `ORGANIZATIONREQUESTS_CLOSURE_TEST_SQL` to contain a connection string for a **dedicated, empty, migrated test database** named `OrganizationRequests_Test_*`. It inserts test data and does not delete it. The assignment integration test uses the separate `ORGANIZATIONREQUESTS_TEST_SQL` variable and likewise requires its own empty test database. Neither test creates, migrates, resets or drops databases. Never point either variable at real data.

The closure integration scenario verifies rollback after the conditional update, concurrent duplicate and different closures, concurrent assignment, SQL Server rowversion and worker use of released capacity. Without a disposable SQL Server these scenarios are skipped, not proven by the unit test doubles.
