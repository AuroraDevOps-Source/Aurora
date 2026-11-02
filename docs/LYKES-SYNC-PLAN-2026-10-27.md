# Lykes: Nova ⇄ Aurora ⇄ FreightOps

Status: proposed implementation plan, based on source and read-only database inspection on October 27, 2026. Nova means the existing Firebird application. FreightOps changes are permitted, carefully. Keep orchestration in Aurora and reuse its sync foundation.

First local implementation increment: see [stable line IDs and sync foundation](LYKES-SYNC-FOUNDATION-2026-10-27.md) for changes, test results, and remaining activation requirements. The inspection findings below describe the starting state.

## Recommendation

Build two adapters around Aurora's existing durable sync worker. Aurora holds the shared operational record and identity mappings; Nova retains billing authority; FreightOps retains physical execution authority. Records can originate in either system. Shared edits pass through Aurora with revision checks and explicit conflict handling.

Do not add a broker, another integration service, or a configurable mapping designer for this first tenant. Implement one versioned Lykes mapping profile in code, with tenant configuration for terminal/account/status codes, units, and numbering. Existing Integration Hub routes must be inventoried before cutover so there is only one writer for each synchronized field.

“Complete” means every agreed operational change is applied, retained as information, or visibly held for review. It does not mean every native field has an equivalent writable field in the other product.

## What was verified

| Area | Evidence and implication |
|---|---|
| Aurora | `aurora_sync_connection` has tenant_id as its primary key and permits only provider `nova`. Connection management, worker credentials, worker execution, and review screens assume one connection. Multiple connections require a coordinated change. |
| Aurora delivery | Inbox, outbox, per-connection baselines, native mappings, retries, leases, and conflicts already exist. Ordinary imports do not enqueue onward delivery when the imported value equals the saved value. This also matters when resolving a conflict by accepting the external value. |
| Aurora orders | `TmsOrder` has header totals but no lines. Nova's sync projection accepts exactly one pickup and one delivery; repeated order visits in a manifest are rejected. The routing model cannot be the complete integration contract. |
| Nova database | Read-only connection reached `D:\DB\WAYL_TEST\OPERATIONS.FDB`. Existing metadata catalogs describe Firebird 5.0.3. `OPS_LINE_ITEMS` has key `(OPL_ORDER_ID, OPL_SEQUENCE)`, decimal pieces, textual cube, SKU, keyword, stop reference, weight, rate, and amount. |
| Nova line lifecycle | `TOrder.WriteLineItems` deletes and reinserts the order's lines. `TOrderLineItemList.Resequence` changes sequence numbers. A physical row deletion or a changed sequence is not a reliable business deletion/identity. |
| Nova totals | `CalcTotals` uses rated stops and keyword flags. Freight, charge-only, and mixed freight/charge rows must be distinguished. A small test sample included blank SKUs and charge-bearing zero-piece rows; this is not a Lykes data profile. |
| FreightOps storage | Staging container `fo-staging-db`, database `fo_app_dev`, reports PostgreSQL 12.2. Read sessions explicitly reported `transaction_read_only=on`; statement timeouts were bounded. PROs/manifests are JSON documents backed by Marten streams/events. |
| FreightOps schema | Verified `mt_doc_pro`, `mt_doc_manifest`, `mt_streams`, `mt_events`, and `mt_event_progression`, plus tracking, inventory, exception, history, and terminal/location projections. Stream inventory: 88,863 typed PRO streams, 11,321 manifest streams, 28 untyped streams. Counts include history and are not active-work counts. |
| FreightOps line identity | `LineItem` has no immutable line ID. Normal lookup/update/remove uses SKU. `Exref` exists but is not the operative identity. Stored sample lines likewise have no line ID. |
| FreightOps quantities | Quantity drives tracking-unit creation. Active count changes with deactivation, shortage, destruction, and splits. Damaged units remain active. Tracking units have GUIDs and barcodes that must survive paperwork edits. |
| FreightOps measures | `Pro.CalcTotals` multiplies quantity by line weight/cube/seats. `LineItemDto.FromModel` omits `CalculatedPerUnit`; `ToModel` does not pass it through. The flag is not a reliable basis for conversion today. |
| FreightOps import | TMSImport is an upsert by PRO and SKU, not full replacement. Omitted lines remain; quantity zero requests removal. Split parents and children are rejected. Some missing/blank fields mean “leave unchanged.” Creation, subsequent changes, and manifest notification updates use separate saves. |
| FreightOps concurrency | Repository methods differ: `StoreAllAsync` uses expected aggregate versions; `AppendAsync`/`AppendAllAsync` append without expected versions. Existing imports are not an atomic, version-checked sync operation with a target receipt. |
| FreightOps outbound | General `OutboxSubscription` only handles ProStowed, performs HTTP inside processing, and catches failures. It is insufficient as the complete change feed. Separate manifest capture code has durable publication, but its outbox table and checkpoint were absent from inspected staging. |

Local FreightOps configuration points to an unavailable localhost:5433 database. Staging inspection was used instead. Staging is not confirmed to be Lykes' database. No application was started, no migration or business command ran, and no database or application source was changed during this review. Source behavior and staging deployment parity still require a release check.

## Shared data and ownership

Keep one shipment record, child freight items, separate charges, stops/movement legs, and append-only execution facts. Store handling-unit identity/state under its freight item; FreightOps remains its operational authority. Preserve source-only fields in source-specific metadata without sending them back as blank values.

| Data | Mapping and proposed authority |
|---|---|
| Shipment identity | Aurora UUID maps to Nova `PB_ID` and FreightOps PRO stream ID. Store displayed PRO/BOL separately. Scope mappings by tenant, connection, and database generation. Never equate `FB-<PB_ID>` with the customer's PRO. |
| Parties | Nova account references and order party snapshots map to FreightOps shipper, bill-to, carrier consignee, and optional shipper consignee. Keep delivery destination separate from ultimate consignee. FreightOps does not require a new account-master service. |
| Commercial fields | Nova owns rates, charges, taxes, invoice totals, payment terms, and billing adjustments. FreightOps receives agreed informational revenue/cost values. Operational changes must not silently rerate invoices. |
| Freight description | Shared, version-checked edits before processing. SKU, description, references, and ordered quantity remain independent of scans. After first touch, changes that would discard or reidentify physical freight require review. |
| Measurements | Preserve declared and observed values separately, with units and source. Use explicit total/per-unit conversion; observed shortages must not reduce declared shipment totals or revenue automatically. |
| Planning | Aurora coordinates planned legs and assignments. Changes entered in Nova or FreightOps are accepted when the leg is editable; concurrent competing plans become a conflict. Once execution starts, FreightOps owns execution state and constrains replanning. |
| Execution | FreightOps owns actual scans, dock positions, loading/unloading, shortages, damage, destruction, orphan reconciliation, and split movements. Aurora retains details; Nova receives supported status/history/exception summaries with real event times. |
| Delivery/POD | FreightOps-originated delivery facts flow to Aurora and Nova. Preserve document identity/reference and access control; do not embed document bytes in every shipment update. Define reversal/correction events explicitly. |
| Cancellation | A business command with guards, not a table delete. Cancel unprocessed work through both adapters. Scanned, loaded, delivered, invoiced, or split work may need explicit reversal/review. Source archival is not cancellation. |

Field ownership restricts competing writes; it does not restrict where a shipment may be created. A FreightOps-created shipment receives a Nova native ID through a validated creation path. Nova's current Aurora writer requires a positive numeric PRO; FreightOps allows string PROs and `-SPA` style split children. Define number allocation and retain external PRO references; do not strip suffixes or invent numeric matches.

## Line items: solve identity once

1. Add an immutable integration line UUID to Nova's line model and persistence. Preserve it through edit, reorder, and delete/reinsert saves. New/copy lines receive new IDs; split relationships retain an explicit origin reference. Audit all line-writing paths used by Lykes, including imports. A trigger that generates a new UUID on every insert would defeat the design.
2. Add a stable `LineId` to FreightOps line models, events, contracts, and commands. Select lines by ID; SKU remains editable business data and may repeat. Update SKU-based split/grouping/lookup paths and affected UI actions as part of this change. This is a focused domain migration, not just a DTO property.
3. Give existing FreightOps lines IDs through recorded adoption events, preserving tracking-unit GUIDs/barcodes and event replay. Do not generate random IDs during historical replay. Keep legacy SKU commands compatible only when the selection is unambiguous.
4. Backfill identities and mappings before enabling bidirectional item edits. Automatically adopt only proven unique matches. Repeated SKU, missing SKU, differing references, and previously split freight require explicit reconciliation. Do not manufacture business SKUs to serve as integration IDs.
5. Capture Nova line changes as “order changed”; read the complete committed aggregate and compare stable IDs. Coalesce its delete/reinsert churn. Retire a line only when absent from the completed snapshot, and protect any associated execution history.

Represent a mixed Nova row as a freight item with an associated charge component, linked back to the same native line. Charge-only rows never create FreightOps tracking units. Classification uses Lykes' keyword configuration and rated-stop rules, not merely amount or positive quantity.

For a line of 10 countable units weighing 500 lb total, FreightOps' current per-unit convention requires 50 lb per unit. Returning 50 lb as Nova's total or sending 500 lb per unit is wrong. Revenue is already summed as a line amount and must not be multiplied by quantity. Keep decimal totals in Aurora and conversion/rounding rules at the boundary; verify target readback within defined tolerances.

Define `orderedQuantity`, `handlingUnitCount`, `piecesPerHandlingUnit`, and execution exception counts separately. Twelve cartons on three pallets do not imply twelve pallet barcodes. Fractional Nova quantities are not countable FreightOps tracking units; require a unit conversion or hold the record. Preserve raw cube strings; dimensional expressions, metric prefixes, area, and volume need explicit interpretation, not generic decimal parsing.

## Splits, stops, and status

FreightOps splits move existing tracking units to a child PRO, update the parent's manifest membership, leave the child unassigned, and keep revenue on the parent; the child is Free Astray. Preserve this operation as one business change with parent/child identities and item allocations. Never import the original unsplit quantities back over the parent.

Recommended initial representation: one commercial shipment with child operational movements in Aurora. Project a split into Nova's verified native split/related-order behavior where supported, preserving total revenue and quantity allocation. If Lykes requires separate Nova orders, allocate valid native numbers and link them; do not pass FreightOps suffixes to the numeric Nova creation writer. The precise Nova split representation is a mapping gate before split writeback, not a reason to discard the split.

Map manifest membership to a movement leg and stop, not just an order ID. FreightOps has separate inbound/outbound/current manifest references and a queue of physical units; Nova has routing stops linked to pickup/delivery stops through `OPS_MANIFEST_STOPS`. Extend Aurora's integration representation for these relationships while keeping the routing UI a simpler view.

Use an explicit transition table keyed by business event, direction, and leg. Arrival at a terminal is not delivery to a consignee; ClosedInbound is not a completed customer delivery. Keep native status codes alongside the common state. Emit valid commands through target rules; never fabricate intermediate scans or timestamps to make labels match. Stowed and detailed dock states may be informational history in Nova if no supported equivalent exists. Normalize event times to UTC while retaining the configured local zone and original time.

## Minimal implementation architecture

### Aurora

- Migrate connections to a tenant/connection key; retain existing connection IDs/mappings and allow Nova plus FreightOps. Scope credentials, checkpoints, status, conflicts, retry, and enable/disable actions to a connection. Keep tenant isolation in every query.
- Add the shared child records above and provider-specific projections. Do not merge a reduced partner payload as though missing fields were deletions. Track synchronized field sets and explicit clear operations. Merge line items by ID rather than treating an entire array as one conflicting value.
- On accepted import, commit business changes, inbox receipt, source baseline, and outgoing work for other destinations in one transaction. Only send a correction back to the origin when an accepted merge actually differs. Apply the same rule to conflict resolution and local edits.
- Persist event ID, origin, causation ID, source revision, payload hash, and mapping version. Suppress echoes by identity and projected-value equality; do not rely on timestamps. Keep each target's baseline independent.
- Order work by dependencies and aggregate. One bad order must not block all tenant work: retain it in a visible failed/conflict queue with retry while unrelated records continue. A capture checkpoint may advance only after successful intake or durable retention of the unresolved work.
- Reconcile periodically: missing mappings, mismatched totals, missing units, divergent memberships, stale target revisions, and unresolved delivery receipts. One per-shipment screen should show both destination states and the precise blocked field/operation.

### FreightOps

- Add a versioned sync read/write contract using stable IDs and explicit field-presence semantics. Reuse domain behavior, but do not wrap the existing TMSImport call and assume it is atomic.
- Within one Marten/database transaction, check expected versions, apply the permitted command, update affected PRO/manifest aggregates, and store an immutable receipt keyed by connection/change ID and payload hash. A retry returns the same receipt without generating more barcodes. Persist or durably schedule required side effects in that same transaction.
- Add correlation/origin metadata to resulting events. Never use administrative ForceUpdate/ForceRemove as an automatic conflict bypass.
- Extend the durable capture pattern already present in manifest integration to PRO, manifest, and required physical-execution events, with a dedicated Aurora feed. Persist outgoing records with the subscription checkpoint and deliver/retry separately. Reuse existing tables/patterns where appropriate instead of creating a second orchestration system.
- Carry actual event facts for execution history; a notice to reread current state alone can lose intermediate milestones. Use snapshots for editable current state and immutable event facts for scans, splits, exceptions, delivery, and reversals.
- Preserve existing behavior for tenants not enabled for this contract. Additive event fields and replay/adoption tests are required. Do not rewrite historical events or directly update `mt_doc_pro`/`mt_doc_manifest`.

### Nova

- Implement durable line identity through supported application save/import paths and capture line/keyword/reference changes needed by the contract.
- Extend the current transactional adapter and target receipts. Write supported business operations, not only isolated columns. Account for header total calculation, rated-stop/keyword logic, history, cancellation, and billing locks; route through an application operation where direct SQL cannot preserve those rules.
- Retain existing Firebird transaction-origin suppression and exact event acknowledgements. Do not use the largest allocated generator/event number as a committed watermark.

## Delivery sequence and acceptance gates

1. **Lykes mapping fixtures.** Confirm actual database/API identities, terminal/account/status codes, numbering, quantity units, keywords, existing integration routes, and Nova split behavior. Assemble representative paired records covering repeated/missing SKUs, freight plus charges, pallets/cartons, multi-stop work, split parents/children, and completed/cancelled records. Produce expected results for both directions.
2. **Identity and transport foundation.** Add stable item identities, backward-compatible adoption, multi-connection Aurora support, onward delivery, target receipts, version checks, and durable FreightOps capture. Gate: replay and response-loss tests produce no duplicate shipment, line, tracking unit, or execution event.
3. **Orders and items in both directions.** Include create/edit/clear/cancel, accounts/parties, references, appointments, measurements, and commercial projections. Gate: repeated SKUs remain separate, resequencing preserves IDs, charge rows create no freight, and round trips preserve supported values and source-only data.
4. **Operational round trip.** Add legs/manifests, execution, exceptions, splits, delivery/POD references, and reversals. Gate: barcode identity/history survives edits and splits; quantity allocations and revenue reconcile; inbound completion never becomes customer delivery; active-work guards remain enforced.
5. **Read-only shadow comparison, then limited live cohort.** Capture and compare without outbound business writes first. Adopt existing mappings before creating targets. Define a safe capture start, scan existing work, then replay overlap with deduplication; do not enable capture only after a long scan. Start one terminal/customer cohort, expand only after reconciliation passes.

Do not call the tenant fully integrated after step 3; step 4 completes the operational scope. Run failure tests for simultaneous edits, duplicate/reordered events, source restore/feed-generation change, late transaction commits, lost target responses, worker restart, offline destinations, partial dependency failure, and poison records. Exercise tenant isolation and legacy event replay explicitly. No broad test execution against a live business database.

Pause writes per destination to recover from an incident while retaining captured work, mappings, and receipts. Reconcile uncertain writes by receipt before retrying. Rollback means stopping/reverting the new application path safely; it must not delete identity ledgers or erase physical execution history. Keep old integrations from writing the same cohort during cutover.

## Remaining Lykes-specific decisions

- Actual Lykes endpoints/database generation and existing-data overlap; inspected test/staging databases are not proof of Lykes deployment contents.
- Definition of a countable unit and total/per-unit measures, keyword classification, status mappings, and revenue fields needed operationally.
- Native numbering for FreightOps-originated work and Nova representation of splits/returns/multiple legs.
- Who may edit shared fields after processing starts, and who resolves exceptions.
- Required sync latency, historical backfill window, document retention/access, and acceptable reconciliation tolerance. A short polling interval using existing workers is sufficient to start; measure load before choosing the production interval.

These are bounded mapping/configuration choices. They do not require another architectural layer.

## Source trail

- Aurora: `src/Aurora.Migrations/Scripts/0030_integration_sync.sql`; `src/Aurora.Modules.Sync/{SyncImporter,SyncStore,SyncReview,SyncConnections,SyncContracts}.cs`; `src/Aurora.Api/Features/Routing/TenantSyncExecutor.cs`; `src/Aurora.Contracts/Tms.cs`; `src/Aurora.Integrations.Nova/FirebirdRoutingSource.SyncOrders.cs`, `.SyncWrites.cs`, `.SyncPush.cs`; `src/Aurora.Integrations.Nova/Sync/{NovaSyncProvider.cs,ChangeCapture.sql}`.
- Nova: `D:/Development/NewTransEaz/OrderEntry/OrderEntryDM.pas` (`CalcTotals`, `WriteLineItems`, `Cube`, `Resequence`); Aurora's `docs/nova-database/tables/{OPS_LINE_ITEMS,KEYWORDB,OPS_MANIFEST_STOPS}.md`.
- FreightOps root: `D:/Development/FreightOps-All/FreightOps`. Reviewed `FreightOps-Core/Domain/Pros/{Pro,LineItem,TrackingUnit}.cs`, `Dto/{ImportProDto,LineItemDto}.cs`, `Commands/{ImportProCommand,SplitProCommand,UpdateLineItemCommand}.cs`, `Subscriptions/OutboxSubscription.cs`; `Domain/Manifest/{Manifest.cs,Notifications/LineItemsUpdatedNotificationHandler.cs,Integration/ManifestHubSubscription.cs}`; `Infrastructure/AggregateRepository.cs`; `Application/ServicesDependency.cs`; `MANIFEST-HUB-INTEGRATION.md`.

This document records a design review, not a passed end-to-end test or deployment readiness certification.
