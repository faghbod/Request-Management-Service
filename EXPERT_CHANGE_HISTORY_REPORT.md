# Code Improvement 8 — Expert configuration history

## Implemented and verified without SQL Server

- Created the ExpertChangeHistory entity and ExpertChangeType enum. Snapshots exclude operational active-request counts.
- Added sorted, distinct numeric JSON specialty snapshots and structured response arrays. Created records have null old values; updates capture a complete before/after pair.
- ExpertManagementService validates a positive authenticated user ID, preserves the existing RowVersion contract, and skips writes for unchanged configurations. Specialty-only updates still force a concurrency-checked parent update.
- Added GET /api/experts/{id}/changes with keyset pagination (default 50, maximum 100), protected by the existing ExpertsManage policy. Both cursorChangedAtUtc and cursorId are required together. Access remains disabled until the issuer's experts.manage permission is explicitly trusted. The existing request-history behavior is unchanged.
- The repository adds records without saving or starting its own transaction. Creation tracks expert and event together for one SaveChanges. Updates add the event before saving and committing the existing transaction. Failed attempts discard pending history.
- Normal application workflows provide no update or delete operations for history. This is not protection against privileged direct database changes.
- Unit tests cover snapshots, identity, authorization, pagination, failure handling, no-op updates, and regression behavior. EF metadata and generated migration SQL are inspected without connecting to SQL Server.
- .NET build and database-independent Category=Unit tests are run using a temporary SDK and package cache; see the final delivery for actual totals.

## Implemented but database-unverified

SQL Server transaction atomicity, generated-key fixup during actual insertion, migration execution, RowVersion conflicts under concurrent requests, and keyset query behavior against real SQL data remain unverified. No database was contacted or modified during this phase.

A no-op checks the submitted RowVersion against the loaded expert but performs no write or database lock; another transaction may change the expert after that read.

## Required before deployment

1. Review and manually apply migration 20261102142333_AddExpertChangeHistory to the target schema before enabling any version that writes expert history. Never run it automatically at startup.
2. Verify create, multi-property and specialty-only updates, concurrent RowVersion conflicts, failure rollback, and paged reads on a dedicated SQL Server test database. Confirm no orphan/extra event persists on rollback.
3. Validate the issuer's permission contract before setting Jwt__TrustExpertManagementPermission=true; until then management and change-history routes are inaccessible to authenticated users.
4. Coordinate rollout: older application versions do not write expert change events. Existing experts and prior changes are not backfilled. Rolling back this migration drops the history table and its contents; back up and plan retention first.