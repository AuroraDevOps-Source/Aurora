# Lykes sync foundation — first implementation increment

Implemented locally October 27, 2026. Nothing deployed or enabled against remote business databases.

Follow-on: [Aurora-side FreightOps HTTP adapter](LYKES-FREIGHTOPS-ADAPTER-2026-10-27.md), including durable request preparation and local transport tests. The target FreightOps business API is still pending.

## FreightOps

- Optional `LineId` on the domain line and DTO keeps existing callers valid. New lines receive IDs during commands; constructors and historical event replay never generate IDs.
- Updates, audited SKU corrections, and removals preserve the identity. Tracking-unit identities, barcodes, scan guards, and existing quantity/weight/cube behavior remain in place.
- Supplied IDs cannot silently select another SKU or replace an existing identity. Legacy SKU matching remains supported. Repeated SKUs are not enabled by this increment.
- Existing lines can receive IDs through explicit `LineItemIdentifiedEvent` events. Ambiguous legacy SKU sets fail before mutation.
- Internal `AdoptLineIdentities` service combines expected stream version, connection/change request identity, a request fingerprint, and a receipt committed in the same Marten transaction as the events. Identical retries return the original receipt. Changed requests using the same change ID fail. Concurrent version conflicts require retry/reconciliation by the caller.
- This service has no public endpoint yet. Its eventual caller must authenticate and bind the connection to the correct FreightOps database; caller-supplied GUIDs alone are not authorization.

## Aurora

- Migration `0042_sync_connections.sql` permits one Nova and one FreightOps connection per tenant while retaining existing connection IDs and dependent mappings.
- Existing settings, scheduling, credential lookup, and Nova workspace-source selection remain scoped to Nova. This does not start a FreightOps worker or expose connection activation in the UI.
- Accepted imports and conflict resolutions forward changes to other destinations. Unchanged readbacks do not echo; normalization corrections can return to their origin.
- Conflict/retry actions identify the connection. Superseded deliveries can be revived for an identical same-revision correction without allowing different data at the same revision.

## Local verification

| Check | Result |
|---|---|
| FreightOps core suite | 238 passed |
| FreightOps PostgreSQL/Marten integration harness | 13 passed |
| Aurora existing sync regression harness, including database checks | 115 passed |
| Aurora new multi-connection database harness | 25 passed |
| FreightOps API and async daemon builds | Passed |
| Aurora API build | Passed |

Database checks used fresh disposable databases on local PostgreSQL 18, never production/staging data. They exercise historical replay, snapshots, stable barcodes, receipt retries, stale versions, concurrent adoption, transaction rollback, two-provider fanout, conflict resolution, tombstones, queue behavior, and tenant isolation. Local PostgreSQL 18 does not substitute for staging's PostgreSQL 12 compatibility validation.

Run `dotnet test FreightOpsCoreTests/FreightOpsCoreTests.csproj` in FreightOps. Set `FREIGHTOPS_SYNC_TEST_CONNECTION` to an empty local database named `freightops_sync_check_*`, then run `dotnet run --project FreightOpsSyncChecks`.

In Aurora, set `SYNC_FOUNDATION_TEST_CONNECTION` to an empty local database named `aurora_sync_check_*`, then run `dotnet run --project tests/SyncFoundationChecks`. The local server must have Aurora's `aurora_app` role provisioned. The harness runs the relevant production migrations under its setup account, then exercises application access using that role. The existing regression harness uses `SYNC_TEST_CONNECTION` and requires the normal sync/business schema. Never point either harness at a business database.

Builds report existing dependency advisories, including critical Marten advisories. These were not upgraded in this change and need resolution before deployment. Output-directory builds were used where necessary to avoid disturbing the running local Aurora API.

## Remaining before Lykes activation

1. Add Nova persistent UUID storage and preserve IDs through delete/reinsert, resequencing, copy, and rating rebuild paths. No Delphi compiler is installed here, so Nova runtime changes are deferred until they can be compiled and exercised locally.
2. Implement the shared line contract, repeated-SKU handling, split/merge lineage, unit/total mapping, and the Lykes field-ownership rules from the full plan.
3. Add the authenticated FreightOps adapter, durable outbound capture, and transactional/versioned business mutation endpoints. The adoption receipt is the first target-side primitive, not a complete business sync API.
4. Upgrade all FreightOps writers and event consumers before identity adoption; mixed old/new writer deployment is not claimed safe. Older request payloads are supported by the new code, but older binaries do not understand the new event contract.
5. Validate PostgreSQL 12 staging compatibility and complete end-to-end Nova/Aurora/FreightOps tests, mapping rehearsal, and staged rollout. No live connection or backfill was enabled in this increment.

See [the full implementation plan](LYKES-SYNC-PLAN-2026-10-27.md) for the complete scope. Existing unrelated Aurora working-tree changes were retained.
