# Confirmed Lykes mapping, version 2

Current release: **Aurora 20261028-1 published October 28, 2026; new Lykes/FreightOps synchronization remains disabled.** Use the **MVP revision — 2026-10-28** section below for the current scope and walkthrough. Earlier sections record the prior local implementation and are superseded where they require Delphi or native reverse writes. Existing legacy import endpoints, profile v1 payloads, and single-terminal configuration remain supported.

## Confirmed rules

- Lykes has multiple terminals. Resolve each shipment's shared/Nova `TerminalCode` to its native FreightOps code; reverse the mapping on export and enrollment.
- One barcode represents one piece. Profile v2 takes a positive integer `Pieces` on every freight line. Header `Pieces` must equal the sum of freight-line pieces. Charges create no barcodes.
- FreightOps cube is **cubic feet per piece**, supported by its existing `Configuration/Prompts/pro-document.txt:12` and quantity multiplication in `Pro.cs`. Profile v2 uses `CubePerPiece`; total shipment cube is the sum of `Pieces * CubePerPiece`.

## Terminal configuration

`AuroraSync.TerminalMappings` maps shared codes to native codes. For example, `AUS: FO-AUS` means an incoming AUS shipment is stored at native FO-AUS and exported as AUS. Codes are exact and case-sensitive. Unknown codes and duplicate native targets are rejected rather than guessed. A configuration change must not silently move an existing shipment; reconcile changed mappings and pending deliveries before applying configuration changes.

When `TerminalMappings` is empty, the existing `OriginTerminal` setting retains its single-terminal behavior. When the map is populated it is authoritative; `OriginTerminal` is not a fallback for unmapped shipments. Created/unscanned/unassigned shipments can change between mapped terminals through a version-checked update. Existing physical-execution guards remain intact.

Bryan supplied primary Lykes codes **AUS, DFW, IAH, AMA, LBB, SAT, MIA, FLL, MCO, JAX, TPA**. These can all be mapped; identity mappings are appropriate only where both systems use the same code. No deployment configuration was populated with assumed native codes.

## Profile v2 payload

The existing HTTP envelope stays protocol version 1. Only the business mapping profile advances to `Lykes.Version: 2`:

```json
{
  "Id": "EXAMPLE-ONLY",
  "TerminalCode": "AUS",
  "Shipper": "Example shipper",
  "Customer": "Example consignee",
  "Pieces": 3,
  "Lykes": {
    "Version": 2,
    "WeightUnit": "lb",
    "VolumeUnit": "ft3",
    "Lines": [{
      "LineId": "be56b25d-08e8-4531-a77a-0857d0bf467c",
      "Kind": "freight",
      "Sku": "EXAMPLE-SKU",
      "Pieces": 3,
      "CubePerPiece": 2.5,
      "TotalWeight": 120,
      "PackType": "C",
      "PO": "",
      "BOL": "",
      "Notes": ""
    }]
  }
}
```

This produces three barcodes, 2.5 native cubes per piece, 7.5 total cubic feet, and 40 pounds per piece. `NativeVolumeUnit` must be `ft3` for v2. Weight continues to use explicit units and a total divided by pieces. If transitional payloads also supply `HandlingUnits` or `TotalCube`, they must agree with the piece count and derived total. Fractional piece counts, metric v2 cube, inconsistent header totals, missing pieces and invalid measurements fail before persistence.

Reverse projection returns shared terminal code, line/header pieces, and cube per piece. Native total-per-line cube on an enrolled record is divided by quantity on read; existing write restrictions for that native mode remain. The Nova measurement helper now exposes `Pieces` and `CubePerPiece` methods for the confirmed policy. Nova raw cube still requires correct scalar/dimension normalization before calculating a per-piece value; no blanket interpretation of arbitrary Nova cube text was introduced.

## Compatibility and validation

Validation completed: **54 real endpoint/database checks and 238 FreightOps core tests passed**; the FreightOps API build passed. Existing dependency advisories remain unchanged.

Profile v1 retains `HandlingUnits`, `TotalCube`, and its explicit-unit behavior. Existing shipments cannot silently switch profile versions; conversion requires reconciliation of counts, barcodes, and queued requests. A new mapping version prevents old pallet-count-shaped requests from being reinterpreted as piece counts. No historical events or receipts were rewritten.

The real endpoint/database harness covers both old and new profiles, distinct native terminal codes, reverse mapping, terminal edits, piece growth, cube totals, enrollment, invalid counts/units, ambiguous configuration and version-change rejection. Existing local-only database safeguards remain in place. Logs: `artifacts/lykes-endpoints/mapping-v2-results.log`.

The broader integration limits from `LYKES-ENDPOINTS-AND-MAPPING-2026-10-27.md` remain: disabled scheduler/endpoint defaults, created/unhandled freight writes only, repeated-SKU and split restrictions, snapshot rather than full execution-event capture, and pending deployment rehearsal. Nova identity/import work is described below. The old email draft's barcode and FreightOps cube questions are now answered; actual cross-system code correspondence and Nova cube-entry conventions remain to verify.

## Nova identity and import increment

Local implementation only; no deployment, database migration on an existing Nova installation, backfill, or integration activation was performed.

Nova source files updated in `D:/Development/NewTransEaz`:

- `OrderEntry/OrderEntryDM.pas`: optional `OPL_SYNC_ID` field. Allocate a GUID on save only when the database exposes that column. Generic `LoadFields`/`SaveFields` already tolerate missing fields. Edit/Assign retains identity; Duplicate clears it so a copied order receives fresh IDs. Sequence, SKU and array position never establish identity.
- `AurRating/AurRatingIntf.pas`: optional `SyncLineId` SOAP property. OrderEntry sends and restores it through rating. Before applying a rating result, every identified input line must return exactly once; unknown IDs are rejected. New rating-generated lines receive identities on save. Missing IDs from an older rating server stop an identified order for review instead of silently changing its identity.

Original bytes are backed up under `artifacts/nova-lines/before/`. Native encoding and CRLF were preserved. A reviewable patch is retained at `src/Aurora.Integrations.Nova/Sync/NovaStableLines.patch` because the native source directory is not a Git checkout.

`Sync/StableLineIds.sql` adds a nullable UUID text column and a global unique index without replacing the existing `(order, sequence)` key. Multiple legacy NULLs remain valid. There is deliberately no insert trigger that invents an ID during delete/reinsert. `Sync/LineChangeCapture.sql`, installed after the existing change-capture schema, queues the owning order on insert, update and delete; changes and notifications share the native transaction.

The .NET `LykesNovaMapping` reader projects native lines into profile v2. It uses `OPL_PIECES` for barcodes, `OPL_WEIGHT`/`OPL_KILOS` for total pounds, and confirmed cube normalization/basis for cubic feet per piece. Pallets, raw cube text, stop/sequence, description, UOM, commercial amount and other native values are retained under each line's `Nova` object. Charge rows produce no barcodes. Existing canonical fields owned elsewhere are retained by stable ID.

The mapping is disabled by default. Host settings are under `FirebirdRouting:Lykes`. `FirebirdRouting:TenantId` and `FirebirdRouting:SourceKey` must both match the saved tenant/database connection before host defaults can select this profile. Configuration requires:

- `TerminalMappings`: native Nova terminal -> shared Lykes code. FreightOps separately maps shared code -> native terminal. The provided AUS/DFW/IAH/AMA/LBB/SAT/MIA/FLL/MCO/JAX/TPA list does not prove either system's native codes. Unknown or ambiguous mappings stop import.
- `KeywordKinds`: actual Nova keyword -> `freight` or `charge`; never infer classification from piece count or amount.
- `PackTypes`: actual Nova UOM -> the confirmed FreightOps packaging code.
- `ScalarCubeUnit` (`ft3` or `m3`), `DimensionUnit` (`in`, `cm`, `ft`, `m`), and `CubeBasis` (`per-piece` or `line-total`). FreightOps is confirmed ft3 per piece; these settings describe how the Nova source represents it. Leading C dimensions remain centimeters; two-dimensional area strings are rejected.
- `FreightStopKind` (`P` or `D`) identifies the expected freight-bearing stop. Freight on another stop is held, not silently filtered; this avoids double-counting mirrored pickup/delivery lines.

Missing/duplicate IDs, fractional pieces, repeated/missing freight SKUs, unsupported packaging, unclassified rows, conflicting pounds/kilos, and unknown cube conventions stop import before persistence. Limits match the target: 200 lines, 500 pieces per freight row, 2,000 pieces per shipment. Disabling the profile for an already mapped order cannot silently fall back to a header-only import.

### Local validation and remaining work

Passed locally: **53 Nova Firebird/mapping checks, 48 adapter HTTP checks, 56 real FreightOps endpoint/PostgreSQL checks, and 45 sync-foundation database checks**. Tests use disposable local databases and remove them afterward. The bridge checks run the Nova projection through the actual FreightOps HTTP endpoint, verify stable ID/terminal/barcode/cube values, and retain Nova metadata on the reverse snapshot. The Aurora API builds successfully; its existing Pkcs dependency advisories remain. Logs are in `artifacts/nova-lines/`.

**Native Delphi compilation and runtime validation are still required.** No Delphi compiler is installed here. The Firebird tests execute the real additive SQL and reader, including supplied-ID delete/reinsert and rollback; they do not execute the Delphi UI or SOAP server. Before release, compile OrderEntry and the rating server together, test save/reload/resequence/edit/copy/rating with both old and migrated database schemas, and rehearse an older rating-server response. Verify EDI/import/other writers either preserve IDs or are held for explicit reconciliation. A legacy writer can still create a NULL identity; Aurora rejects it rather than assigning a replacement to an enrolled line.

Existing orders receive IDs on an updated native save after the schema is installed; a bulk adoption process is not yet provided. Do not automatically re-adopt missing identities on an already synchronized order. Verify actual terminal master/workspace correspondence, keyword/UOM mappings and representative Nova cube samples before activation.

At the end of the identity/import increment, Nova reverse writes were held entirely. The next local increment below now supports a bounded physical-correction path. Ordinary legacy Nova writes remain unchanged. No endpoints, workers or deployment gates have been enabled.

## Nova reverse physical corrections

Implemented locally in `Sync/LykesNovaLineWriter.cs` and routed from the Nova sync push path before the legacy header-only writer. `FirebirdRouting:Lykes:WritesEnabled` is a **separate opt-in, default false**; import alone never enables reverse writes. The existing tenant/source matching still applies. No configuration has been enabled, no existing database changed, and no deployment performed.

Supported: updates to piece count, total pounds and cubic feet per piece on **existing, imported, identified freight lines**, with a complete v2 snapshot. Orders must be ready delivery work in Nova, have one pickup and one delivery, and be unassigned/undelivered. FreightOps state must be absent or Created, without split lineage. Canonical totals must match freight lines. IDs identify rows; sequence, SKU and array position do not. Empty optional PO/BOL/notes fields supplied by FreightOps are tolerated without interpreting them as native changes.

The writer locks the order, stops and native lines, checks the imported line baseline again, and validates all changes before applying updates. It updates pounds, kilograms and derived ton values together. Scalar Nova cubic feet are supported in both explicit `per-piece` and `line-total` configurations. Dimensions and metric cube strings require native review rather than being replaced with scalar text. Values that would lose supported decimal precision are rejected.

Native totals follow Nova code, not FreightOps physical-volume totals: `CalcTotals` sums scalar line cube and includes pieces/weight according to keyword flags on rateable stops; `CalcStopTotals` also follows keyword flags. This path requires ordinary freight keywords contributing both pieces and weight, applies scalar cube/piece/weight deltas to the owning stop, and applies header deltas only for a rateable stop. It preserves other rows, sequence, SKU, descriptions, pallets, packaging, charges and commercial values. It does not attempt to repair unrelated historical total discrepancies.

**Rating remains native-owned.** Nonzero header bill/revenue, native rating notes (including zero-dollar rated shipments), nonzero line rate/amount/dimensional weight, or populated rate type block physical changes. There is no pretend rerating or automatic clearing of commercial values. The subsequent increments below add freight line lifecycle and bounded order creation/cancellation. Terminal/address changes, SKU/packaging changes to existing lines, charges, split freight and manifests remain explicitly held for native review. Unsupported changes cannot be silently acknowledged by the legacy writer.

Business updates, native stop/header totals, the existing durable receipt and one normalization-refresh queue entry share the same Firebird transaction. Receipt retries do not repeat updates or deltas. The explicit refresh sends the updated raw Nova metadata back to Aurora so its comparison baseline can advance; acknowledgement removes it. Ordinary trigger echoes to the same consumer remain suppressed. Native changes or concurrent edits produce a conflict; a failed transaction leaves neither changed freight nor a receipt or refresh event.

### Reverse-write validation

Passed locally: **102 Nova Firebird/mapping checks and 59 real FreightOps endpoint/database checks**. The Aurora API build passed with its existing Pkcs advisories. Tests use disposable local Firebird/PostgreSQL databases, removed afterward. Logs: `artifacts/nova-reverse/`.

Coverage includes stable-ID updates, native totals, preservation of unrelated data, retry receipts, stale baselines, concurrent writers, rollback after updates, normalization refresh/acknowledgement, unchanged snapshots, malformed values, precision rejection, scalar per-piece/line-total cube, rating and assignment guards, unsupported edits, and disabled-write defaults. The bridge uses a snapshot returned by the real FreightOps HTTP endpoint, writes it through `NovaSyncProvider` and the actual line writer, then reprojects the saved Nova row to verify the return path.

No additional Delphi code changed in this increment. The earlier stable-ID source patch still requires Delphi compilation and native UI/rating runtime validation before rollout. The actual deployed Nova schema, keyword/rating behavior and writer mix must be rehearsed before enabling this path. Native rerating remains outside this implementation; the later order-lifecycle increment is documented below.

## Nova freight line additions and deletions

Implemented locally, with **no deployment or configuration activation**. Existing header-only legacy sync remains unchanged. The v2 complete `Lines` array is the desired freight list: a new GUID adds a freight line; an existing freight GUID absent from the array deletes that line. Existing charge rows must remain present and unchanged. At least one freight piece must remain; removing all freight requires the separate cancellation workflow.

Additions require `FirebirdRouting:Lykes:NewFreightKeyword` to explicitly name a keyword classified as `freight` by `KeywordKinds`. Its native keyword must contribute pieces and weight. Null (the default) holds additions. `PackType` must resolve to exactly one native UOM via `PackTypes`, and the configured `FreightStopKind` selects the existing stop. New lines append after the highest native sequence without renumbering other rows. Their incoming UUID is preserved; rates, amounts, pallets and pallet positions start at zero. No pallet count is inferred from pieces. New lines cannot supply a `Nova` metadata object or replace an existing SKU with a fresh identity. PO/BOL/notes remain canonical overlay fields, not new native commercial writes.

All previous assignment, delivery, split, rating, dimension-string, precision and stale-baseline guards apply to additions and deletions. Rating is checked against the original native rows **before deletion**, so removing a rated row cannot bypass it. Charges cannot be added/deleted through this path. Full snapshot totals, unique IDs/SKUs, the 200-row limit, 500 pieces per line and 2,000 pieces per shipment are validated before mutation. Native `D_WEIGHT` has three decimal places in the source schema; writes now reject extra weight precision rather than permitting database rounding.

Deletion subtracts native scalar cube, weight, pieces, pallets and pallet positions from the owning stop and, for rateable stops, the header. Pallet positions are now included in canonical Nova metadata so an intervening position change produces a baseline conflict. **Refresh existing Lykes baselines before enabling the new write capability**; old snapshots missing this metadata are held rather than used to delete current freight. Updates that do not add/delete still work without the new identity-retention table.

`Sync/LineTombstones.sql` is a new **manual additive migration**, after the stable-ID schema. It creates `AURORA_SYNC_LINE_TOMBSTONE`; it is not installed by application startup. Sync-created IDs are reserved with `DELETED_AT` null, and deletion retires the reservation. Deleting a pre-existing native ID records a retired entry directly. The unique reservation closes competing-connection allocation races; retired IDs cannot be reused by this writer, including on another order. This protects sync writes; other legacy/native writers still require the previously documented rollout review. The sync database user needs access to the new table during deployment rehearsal.

Native insert/update/delete operations, identity reservations/retirement, stop/header totals, receipt and normalization refresh all commit or roll back together. Retries return the existing receipt. Mixed additions and deletions are supported on eligible orders. A duplicate/retired ID, invalid SKU/UOM, unsupported change or stale snapshot fails rather than silently replacing another line.

Local validation: **146 Nova Firebird/mapping checks and 63 real FreightOps endpoint/database checks passed**. The Aurora API build passed with the pre-existing Pkcs advisories. Disposable local databases were removed after testing. Logs: `artifacts/nova-line-lifecycle/`.

Tests cover append/retry, explicit keyword and migration requirements, unique packaging resolution, empty/foreign/retired UUIDs, preservation of native sequence, rating blocks on both additions and deletions, charge/last-freight deletion holds, changed pallet-position conflicts, pallet/position totals, rollback of a mixed request including identity records and notifications, and concurrent additions. The HTTP bridge publishes refreshed Nova metadata back to FreightOps, then adds and removes a line through the real FreightOps endpoints and writes the resulting snapshots to disposable Nova, verifying both return projections and retry receipts.

No additional Delphi changes were made here. Native compiler/UI/rating validation of the earlier stable-ID patch and a rehearsal against the actual tenant schema remain required before deployment. Existing integration and write-enable settings remain off.

## Nova order creation and cancellation

Implemented locally in `Sync/LykesNovaOrderWriter.cs`, dispatched from the Lykes-specific Nova push path. **Nothing has been deployed or enabled.** Legacy header-only sync behavior is unchanged. New operations additionally require `FirebirdRouting:Lykes:OrderLifecycleEnabled` (default false) and configured `OperatorInitials` (1–8 characters), as well as the existing mapping/write flags. Initials identify the integration in native creation/cancellation audit fields.

### Creation

The incoming order must be new, unassigned delivery work in a ready state, with shipper and consignee. Its `Id` must be a canonical positive numeric PRO of at most nine digits; alphanumeric or zero-prefixed PROs are held rather than renumbered. Existing NativeOrderId, manifest association, split lineage and active FreightOps execution state are rejected. New freight uses the previously configured keyword, UOM, cube-basis and stable-ID rules; no commercial charge creation or rating is inferred.

Creation resolves the shared terminal to a real native terminal, validates an optional consignee account code, and uses an allowed ready status. If the connection has multiple ready statuses, configure `NewOrderReadyStatus` explicitly. An unambiguous single ready status needs no override. Execution/VOID statuses cannot be used as creation defaults.

`Sync/OrderKeys.sql` is a **manual additive migration**, after the existing sync/line migrations. It reserves sync-created PROs in `AURORA_SYNC_ORDER_KEY`. Existing native PROs, including cancelled ones, are also checked before creation. Concurrent independent sync connections cannot reserve the same PRO; the reservation persists through cancellation. This does **not** change Nova's legacy PRO-number allocator or its nonunique location/PRO/suffix index. Before rollout, confirm an allocation policy or reserved range that prevents native users/importers and sync from independently selecting the same new PRO. Do not treat the sync reservation table as database-wide uniqueness enforcement for legacy writers.

The writer allocates native order/stop IDs with the existing generators, creates a type O/load-type O header (matching the existing API creation path), records the mapped terminal as PRO location, and creates pickup/delivery stops with names, addresses, optional coordinates and consignee account. Bill-to text, BOL reference and notes are retained. It stores the requested delivery appointment window, or a point window at ScheduledAt, in the configured Nova timezone. Account/rate matching is not guessed. Freight lines are then seeded through the existing validated line writer; native totals are calculated from those lines rather than trusting supplied header totals.

Header, stops, freight, line identity reservations, PRO reservation, receipt and normalization refresh share one transaction. A validation/database failure leaves no partial order or receipt. Generator gaps after rollback are normal. A retry with the same change ID returns the committed receipt.

### Cancellation

Cancellation is a soft void: the native header, stops, lines, IDs, measurements and financial values are retained. The writer sets `PB_DT_CANC`, the unique native `VOID` status, `PB_STATUS_TIME`, `PBCINIT`, and an audited reason in `PB_RATE_NOTES`. Optional `CancellationReason` allows 500 characters; otherwise a system reason identifies the sync change. It does not run or approximate native accounting reversals.

Only ready, unassigned, undelivered, unconsolidated orders are eligible. In addition to existing line rate/amount/dimensional-weight/rate-type safeguards, cancellation blocks rated/audited/billed/invoiced dates, bill/revenue/payment/interline/COD/discount values, rating notes, and Nova's no-charge billing flag. Both stop manifest fields and actual `OPS_MANIFEST_STOPS` relationships are checked. A full unchanged freight snapshot is required; cancellation cannot hide a line edit or deletion. Actual VOID status must resolve uniquely. Posted, rated or financially special orders remain native-review cases even if their current total is zero.

Cancellation, its receipt and the outgoing tombstone commit together. Response-loss retries do not repeat voiding, and an already-cancelled record keeps its original cancellation audit. An unmapped local deletion does not create a native order. New upserts cannot reuse a cancelled PRO. Reinstatement remains unsupported.

### Compatibility and validation

Testing exposed the pre-existing ready-state naming mismatch: Aurora uses `ReadyToRoute`, while the first Lykes adapter used `Ready`. Nova and FreightOps now accept both ready aliases, retaining existing payload spelling and continuing to reject execution-state fabrication. FreightOps also allows a **first** positive NativeOrderId binding and its associated scheduling/consignee-routing normalization. Once bound, that ID cannot be reassigned or cleared. This is a targeted compatibility change; other informational-field restrictions remain.

Passed locally: **218 Nova Firebird/mapping checks, 69 real endpoint/database checks, and 238 FreightOps core tests**. Both Aurora and FreightOps API builds passed. Existing Pkcs/Marten dependency advisories and unrelated compiler warnings remain. Logs: `artifacts/nova-order-lifecycle/`.

Coverage includes atomic creation, native totals/addresses/timezone/audit references, duplicate/cancelled PRO rejection, same-connection and independent-connection races, invalid child rollback, lifecycle defaults, ambiguous ready/VOID status, paid/posted/no-charge/rated/manifested cancellation holds, stale snapshots, failure after cancellation, preserved line history, retry receipts and cancellation tombstones. The bridge creates a Nova order from a real FreightOps endpoint snapshot, binds the resulting native identity back into FreightOps, cancels through FreightOps, and verifies the resulting Nova VOID state and retained lines.

No Delphi source changed in this increment. The earlier identity patch still needs a Delphi build and native UI/rating validation. Deployment rehearsal must verify actual schema/defaults/triggers, operator identity, terminal/PRO-location conventions, status configuration, PRO-number allocation and permissions on the new tables. All new capabilities remain disabled pending that rollout work.

## Careful implementation review — 2026-10-27

**Readiness correction:** the local components are implemented, but this is not yet a complete production three-way sync. The following findings supersede any broader readiness interpretation of the test counts above. Nothing was deployed or enabled during this review.

### Fixed and regression-tested

The physical line writer checked bill/revenue, rating notes and line rating fields, but omitted the stronger zero-dollar posting/payment/consolidation guards used by cancellation. It also checked the stop's manifest number without checking actual manifest membership. This could allow a line edit, addition or deletion on protected freight when those simpler indicators were empty.

`LykesNovaLineWriter` now rejects mutations when rating/audit/bill/invoice dates, payments, interline amounts, discounts, COD, consolidated-master identity or the no-charge flag indicate protected work. It checks actual `OPS_MANIFEST_STOPS` membership under the existing transaction locks. Existing no-op snapshots still avoid rewriting financial data. Tests cover every added header guard for edits and representative posted/no-charge/consolidated cases plus actual memberships for additions/deletions, preserving native lines and withholding receipts on rejection. No new migration or opt-in was introduced.

### Open findings before a pilot

1. **Business PRO identity is not correctly established for first-time Nova imports.** `FirebirdRoutingSource.ReadSyncOrder` initializes an unmapped record with `Id = FB-<PB_ID>`; `AuroraSyncService.Change` uses payload `Id` as the FreightOps PRO stream ID. The reader does not map `PBNUM`/location/suffix to a separate agreed business identity. Wiring these paths together as-is could create FreightOps shipments under internal Aurora IDs. Preserve existing mappings; implement explicit business-PRO mapping and collision/suffix reconciliation before enabling forwarding. Do not rename established records or strip suffixes automatically.

2. **Scheduled orchestration is incomplete.** `TenantSyncExecutor` constructs only a Nova provider. The FreightOps HTTP provider exists and is tested but is not registered in that scheduled path. Moreover, `SyncStore.Enqueue` currently fans all entity types to every destination, while FreightOps accepts only orders. Provider-specific entity eligibility is needed before wiring the worker, otherwise terminals/customers/resources/manifests will generate unsupported requests. This is implementation work, not merely a configuration switch.

3. **Native order notes are not refreshed.** Nova creation writes `PB_NOTES`, but the production order reader's `SyncOrderFields` does not read it. Native note edits can therefore leave the canonical snapshot stale. Define and test note ownership/projection before calling the field mapping complete. Line PO/BOL/Notes are retained canonical FreightOps metadata; the Nova line writer does not map them into native line columns. Do not describe those fields as native two-way writes.

4. **Existing tests do not exercise the entire production mapping path.** The endpoint bridge uses real FreightOps HTTP/domain/database behavior and real Nova lifecycle writers, but supplies a test `INovaSyncMapper` and constructs Nova normalization snapshots itself. This bypasses `FirebirdRoutingSource.ReadSyncOrder`, production mapping lookup and scheduled orchestration. Add a disposable full-path test beginning with a native Nova order (including actual business PRO and changed notes), importing through Aurora and forwarding to FreightOps, then reversing through the same production path. Existing passing counts do not establish that coverage.

5. **Native deployment constraints remain unresolved.** Compile and rehearse the Delphi identity patch, including the rating service's optional identity round trip, existing-order adoption and all native line-save paths. The sync reservation table coordinates sync creators only; legacy PRO allocation still needs coordination. Validate actual schema widths/defaults/triggers and mapping configuration. FreightOps pull is explicitly limited to 500 enrolled shipments and does not automatically enroll every new native shipment; define the pilot cohort and enrollment workflow before activation.

### Review validation and scope

Passed after the guard fix: **261 Nova checks, 69 FreightOps endpoint/database checks, 48 adapter HTTP checks, 45 sync-foundation database checks, and 238 FreightOps core tests (661 total)**. Local PostgreSQL databases were uniquely named disposable fixtures and dropped afterward; embedded Firebird fixtures were also disposable. The Aurora API build passed into a separate output directory. An initial ordinary build failed because the running local API locked its output DLLs; the running application was left intact. Existing dependency warnings remain. Logs are in `artifacts/sync-review/`.

Reviewed the changed sync foundation, durable HTTP requests/receipts, endpoint authentication and native state guards, Lykes line mapping, Nova writers/readers, manual capture/identity scripts, and the earlier Delphi patch. Only the Nova line safeguards and test fixtures were changed in production/test code in this pass. The open identity, orchestration and notes findings above were deliberately not resolved by guessing business mappings or activating the worker. No production database was queried or changed in this pass. This review is evidence of the stated checks, not a certification of untested native behavior.

## PRO identity and production mapping path — subsequent implementation

The business-PRO and native-notes findings above are now addressed locally. For Lykes only, `FirebirdRoutingSource.ReadSyncOrder` reads `PBNUM` as the canonical business `Id`; `PB_ID` remains the external database mapping key and `NativeOrderId`. Manifest references to newly imported orders use that same projected identity. The reader now also refreshes `PB_NOTES` into `Notes`. Legacy non-Lykes imports retain their existing `FB-<PB_ID>` behavior.

The supported business PRO is a positive numeric value of at most nine digits with no suffix. Null/zero/negative values, suffixed PROs, and duplicate PRO numbers across locations/suffixes (including cancelled orders) are held. No suffix is stripped and no location-based identifier format is invented. An existing mapped `Id` that differs from the native business PRO is held for reconciliation, not renamed. This includes earlier `FB-*` Lykes mappings and later native renumbering. Before activation, reconcile any such mappings and already-prepared/queued legacy-ID exports; this increment does not rewrite them or existing FreightOps streams. PRO allocation coordination with legacy writers is still required.

`ProductionMappingChecks` now runs through **the real `FirebirdRoutingSource`, `NovaSyncProvider`, `OperationalSyncEntities`, `SyncImporter`, `SyncStore`, `SyncRunner`, `FreightOpsRequestStore`, HTTP adapter, and FreightOps endpoint/domain persistence**. It does not supply a test Nova mapper or construct normalization snapshots. Tests use a disposable embedded Firebird database and a disposable local PostgreSQL database with the required operational tables and actual sync migrations. Native seed data is created with the native writer, then read through the production reader. Runners are invoked explicitly; no scheduler is activated.

The new production-path coverage includes native business identity versus database identity, legacy behavior, invalid/suffixed/duplicate PRO holds, unchanged existing mappings, changed native PRO holds, Nova-to-Aurora-to-FreightOps import, native note forwarding, reverse measurement corrections, stable line retention, normalization convergence, FreightOps-origin creation and native-ID binding, receipt replay without duplicate creation, cancellation with retained line history, and rejection of posted freight without mutations or receipts.

This path exposed and fixed two additional compatibility issues:

- FreightOps snapshots now omit Aurora-only workspace bookkeeping (`SourceId`, `Revision`, `TerminalId`, `CustomerId`, `OptimizationDraftId`, `ManifestId`, `ManifestNumber`, `StopNumber`). These fields are ignored in informational-field comparisons; they do not assign native freight or change stream versions. Business PRO, bound native identity, expected target revisions, and actual FreightOps workflow checks remain enforced. Old canonical documents containing these fields remain readable.
- `SyncJson` compares appointment/schedule timestamps at PostgreSQL/Npgsql microsecond storage precision. Discarding a final 100-nanosecond digit no longer causes a false competing-edit conflict during Nova normalization. Tests use a deterministic seven-digit timestamp and verify that a real one-microsecond difference remains distinct. No broader tolerance is used.

Validation for this increment: **101 endpoint/database checks (including 32 added production-path checks), 261 Nova checks, 238 FreightOps core tests, and 45 sync-foundation database checks passed: 645 total**. Both API builds passed to isolated local output folders, with existing dependency/compiler warnings. Logs: `artifacts/pro-identity/`. Test databases were removed after each run.

No new database migration was required by these fixes. Nothing was deployed or enabled. Remaining work still includes FreightOps scheduled-worker wiring and entity eligibility, an enrollment/cohort plan, Delphi/rating-service validation, actual tenant-schema rehearsal and deployment/configuration review. This closes the tested order-mapping gap; it does not certify the scheduled runtime, native Delphi UI, manifests, or production infrastructure.

## Morning review: runtime, enrollment and local completion

The scheduled FreightOps path and destination filtering are now implemented locally. `TenantSyncExecutor` runs the existing Nova pass and a separate bounded `FreightOpsSyncCycle`. Both use the same tenant advisory lock. A non-cancellation failure in one provider does not prevent the other provider's pass; each retains its own status. FreightOps pulls before pushing, uses the durable prepared request on retries, and recovers an uncertain/expired write before retrying a blocked import. Scheduling remains governed by the existing global worker switch, the new default-off `Synchronization:FreightOps:Enabled` switch, and the tenant connection's enable flag. No switch was enabled here.

`SyncStore` now queues/claims only orders for FreightOps; Nova's supported master/resource/manifest behavior is unchanged. The adapter also rejects non-order incoming events before import. PostgreSQL migration **0044_freightops_runtime.sql** discovers tenants with either provider enabled. It retires unsupported historical pending/conflict work only when no durable target request was prepared. In-flight and prepared requests remain available for explicit review; the runtime does not send unsupported entities.

Machine authentication deliberately uses a dedicated secret configuration, not the existing FreightOps user/password settings. `FreightOpsRuntimeOptions` binds tenant ID + credential reference + source generation to an installation URL and bearer token. It rejects ambiguous/missing tenant configuration, source-generation changes and unsafe URLs. HTTPS is required except on loopback. Tokens are never returned by the connection/status APIs or persisted in errors. Restart the host after rotating startup-bound machine configuration. The target uses the same token's SHA-256 hash.

### Operational surfaces

- Existing `GET /api/v1/synchronization/connection` retains its Nova-only behavior.
- `GET /api/v1/synchronization/connections` lists both connections without secrets; Admin/PowerUser authorization is required.
- `GET /api/v1/synchronization/connections/status` shows each provider's configuration/runtime flags, attempts, last success/error, queued/working/held writes and unresolved field conflicts. These separate counts matter: a successful poll does not mean every held shipment is synchronized.
- Existing Admin `PUT /api/v1/synchronization/connection` now accepts `Provider: "freightops"` with a configured credential reference. It retains optimistic revisions and rejects a changed source generation. Save it **disabled** initially. Read the resulting connection ID before configuring the target; the target must use that exact ID, not the credential-reference UUID.
- New Admin-only `POST /api/v1/synchronization/freightops/enroll` accepts `ChangeId`, `Pro`, `ExpectedRevision` and `Data`. It forwards an explicit expected-stream-version enrollment, validates the returned receipt, returns 409 on a target conflict and 503 on an uncertain transport result. Retry the **identical change ID and body** after an uncertain response. Enrollment is a write, not a discovery endpoint. It requires the machine-runtime switch but can precede enabling the tenant's scheduled connection.

Enrollment `Data` must identify the exact existing FreightOps PRO and shared terminal, supply the reviewed canonical scheduling/other source-only metadata, and include a v2 Lykes profile with `WeightUnit: "lb"`, `VolumeUnit: "ft3"`, and an **empty** `Lines` array. FreightOps returns its own identified native lines; the caller must not guess IDs by SKU, sequence or array position. Use the actual current stream version as `ExpectedRevision`, never zero or a guessed number.

### Rehearsal and activation order — not executed

1. Work from restored tenant databases and keep all write/worker flags off. Record the intended tenant ID, both database generations, exact terminal crosswalks, keywords/UOMs, cube interpretation, ready/VOID statuses and audit initials. The supplied shared codes AUS/DFW/IAH/AMA/LBB/SAT/MIA/FLL/MCO/JAX/TPA do not establish native codes. Use `docs/lykes-sync.example.json` as a **disabled template**, not a deployable configuration. The `FreightOpsTargetExample.AuroraSync` block belongs under `AuroraSync` in the FreightOps host, not Aurora. Populate machine tokens through deployment secrets/environment; never commit them.
2. Compile and exercise the Delphi OrderEntry/rating-service changes together. Verify create, edit, save, copy, rerate, generated charges and failure rollback. Updated native saves establish missing stable IDs; bulk adoption is not automatic. Reconcile previously synchronized rows with missing IDs instead of minting replacements. A compiler was not found in PATH or the checked Embarcadero locations on this machine, so this step remains unverified.
3. Rehearse the additive Nova scripts in their documented order: change capture, stable IDs, line capture, line identity retention, then order-key reservations. Apply Aurora migrations through 0044 on the clone. Verify actual schema defaults/widths/triggers and database permissions. `Sync/LykesPreflight.sql` supplies read-only identity/schema/catalog checks; run its metadata checks first and its line queries only after the stable-ID column exists. It performs no adoption or enablement. Verify runtime behavior/performance with representative volume, not only fixture schemas.
4. Resolve duplicate/suffixed/invalid PROs, old `FB-*` mappings and already-queued/prepared old-ID requests. Coordinate PRO allocation with all legacy native writers; the reservation table does not constrain them. Do not auto-merge an existing Nova shipment with an existing FreightOps shipment merely because their PROs match. Reconcile those pairs and their line identities explicitly before scheduling either side.
5. Choose a reviewed initial cohort. The current FreightOps pull implementation supports at most **500 enrolled PROs** and fails visibly above that limit. It does not silently enroll all native shipments. Start with new shipments and explicitly enrolled native shipments whose counterpart/identity plan is known. A larger cohort needs a capture/paging implementation before activation; do not exceed the bound and call that a production rollout.
6. Save the FreightOps connection disabled, configure the target with the returned connection ID/source generation and token hash, then rehearse explicit enrollment and both directions on the clone. Confirm header/line measurements, stable IDs, piece barcodes, totals, receipts, stale-write holds, normalization convergence and soft cancellation. Test failures and process restarts, including response loss.
7. Review results before any deployment or activation. Once approved separately, enable the smallest reviewed cohort and watch both provider statuses and unresolved record issues. Disabling the tenant connection stops new claims; already-issued operations must be reconciled through their existing receipts. Do not erase ledgers or generate new change IDs to bypass uncertainty.

### Evidence from this completion pass

The expanded endpoint suite now includes the **actual Aurora tenant executor, connection catalog, authenticated status routes and Admin enrollment route**, in addition to the real mapping/import/outbox/HTTP/domain path. Host tests verify that FreightOps runs when Nova is disabled, anonymous access is rejected, PowerUser cannot enroll, the runtime-off flag blocks enrollment, replay returns the same receipt, changed enrollment preconditions conflict, and status responses omit the token. Runtime tests cover actual queued-order delivery, advisory-lock contention, invalid credentials, source-generation holds, tenant isolation and recovery. Foundation tests run under the non-bypass RLS role and cover provider filtering, FreightOps-only discovery and preservation of prepared historical requests during migration.

Final validation: **124 endpoint/database/host checks, 261 Nova checks, 238 FreightOps core tests, 52 foundation database checks and 49 adapter HTTP checks passed (724 total)**. Both API builds passed with existing dependency/compiler warnings. The host suite also verifies that a Nova failure does not prevent the FreightOps pass and that provider failure statuses remain independent. Logs are recorded in `artifacts/sync-completion/`. All databases used by these suites are disposable local fixtures and are removed afterward. The code and disabled configuration template are ready for review; no application was deployed, no live integration was enabled, and no production data was changed. Native Delphi build/UI/rating validation, tenant-specific configuration, existing-pair reconciliation and the restored-database rehearsal remain release gates, not completed claims. Supported reverse writes remain deliberately limited to eligible unhandled delivery freight; protected financial/execution changes and unsupported workflows stay held for native review.

## MVP revision — 2026-10-28 (supersedes the Delphi-dependent release plan above)

Bryan selected a smaller MVP that leaves Nova's application and rating unchanged. Use `FirebirdRouting:Lykes:NativeReadOnly=true`, with `WritesEnabled=false` and `OrderLifecycleEnabled=false`. The template now includes this mode. Lykes and FreightOps runtime enable flags remain false until the test connection and mappings are reviewed. Existing non-Lykes tenant behavior is retained; the MVP flag does not block legacy writes when the Lykes profile is disabled.

Nova owns freight. Aurora reads native rows without selecting or requiring `OPL_SYNC_ID`. First import assigns deterministic operational IDs in Aurora; they are NOT persistent native row identities and must never be used for native writes. Only explicitly classified freight is exported; rating/charge rows and amounts stay in Nova. A canonical freight snapshot, sorted by unique SKU, must remain unchanged to retain these operational IDs. Reordering/resequencing and changes to native row IDs or financial amounts do not alter the snapshot. Unknown keywords, repeated/missing SKUs, changed freight measurements/counts/descriptions/packaging, and switching an existing native-ID mapping into this mode are held for explicit reconciliation. No guessed line matching or barcode replacement occurs. An indistinguishable replacement row is treated as the same operational snapshot, not proven physical identity.

Reverse order/line updates, additions/deletions, creation/cancellation and sync manifest writes are held with a review error before native mutation—even if older write flags are accidentally enabled. Operational snapshots carry an ownership marker that also blocks switching them into the native writer. FreightOps data can still enter Aurora; its correction is not claimed to have reached Nova. Even an initial FreightOps normalization/metadata return can produce a held Nova export in this deliberately conservative MVP. There is no new allowlist of operational Nova return writes in this release. Lykes optimization remains local to Aurora; exporting its resulting manifests to Nova is outside this MVP. Existing non-Lykes optimization is unchanged.

The two local Delphi edits were reversed using the saved patch, with reverse applicability checked first and forward applicability checked afterward. `Sync/NovaStableLines.patch` is retained for future work only. Do NOT apply it or the stable-ID, line-retention or order-key migrations for this MVP. Continuous Nova capture needs the existing `ChangeCapture.sql` and `LineChangeCapture.sql`; the latter has no stable-ID dependency. No native schema or application is being deployed with this Aurora publication.

The conservative hold occurs while reading the native aggregate. It leaves its capture event unacknowledged and can block that Nova batch until reconciled; this release does not add automatic reconciliation or per-record quarantine. Do not repeatedly retry a changed freight snapshot expecting an automatic match. Begin with a small reviewed cohort and no shipments already paired under the earlier identity model.

Current local validation: **273 Nova checks, 130 real endpoint/database/host checks, 14 tenant-settings checks and the order-selection regression suite passed**. The new production-path checks import a native order whose line ID is NULL, deliver it to real FreightOps HTTP/domain persistence, preserve IDs through resequencing/rating-amount changes, ingest a FreightOps correction, and verify Nova measurements/rating remain unchanged with a visible held export. Existing full-write tests remain as regression coverage for the separately gated future capability; they do not imply it is enabled in the MVP. API, client and migration Release publishes passed. Existing dependency warnings remain.

### FreightOps test-server handoff and walkthrough

1. Publish the existing changed FreightOps application from `D:/Development/FreightOps-All/FreightOps`, including its core project, to the test server. Preserve that server's normal configuration. Do not deploy Nova/Delphi. Keep `AuroraSync:Enabled=false` initially. The Aurora release does not publish FreightOps for Bryan.
2. Sign in to Aurora at its usual site. Verify existing Orders and optimization still load. Settings contains tenant connections; `/aurora/synchronization` remains available directly even though its sidebar link is hidden. The per-provider status API is `/api/v1/synchronization/connections/status`.
3. Supply the FreightOps test installation URL and verify which tenant/database is the Lykes test source. Configure dedicated machine authentication, matching connection/source-generation IDs and actual terminal crosswalks. The normal FreightOps user/password settings do not replace the machine token. The disabled example contains placeholders, not usable secrets.
4. Confirm Nova keyword classifications, UOMs, cube basis and source units against restored data. The shared Lykes codes and confirmed per-piece cubic feet do not establish every native code or cube entry convention. Keep the existing WayTest source separate from Lykes.
5. Enable only the reviewed test connection/cohort after FreightOps is ready. Use one native test order with a unique numeric PRO and unique freight SKUs. Verify terminal, piece count, one barcode per piece, weight and cubic feet per piece in FreightOps and Aurora.
6. Verify an unchanged-freight Nova save/rerate retains operational IDs. Change a physical measurement in FreightOps: Aurora must show the update/held return while Nova remains untouched. Change the native freight list: Aurora must hold it for reconciliation rather than replace barcodes. Test cancellation separately; handled FreightOps shipments retain their existing workflow safeguards.
7. Use those orders in Aurora optimization and inspect the plan locally. Do not expect this MVP to publish Lykes manifests back to Nova. Review holds before expanding the cohort (FreightOps capture still has the 500-enrolled-PRO bound).

### Publication result

Aurora API/client release `20261028-1` is live at `https://user.aurorasoftware.com`. Aurora PostgreSQL migrations 0040–0044 were rehearsed against a restored server database and then applied with the API stopped and a verified backup. Record counts and existing source bindings were preserved. No Firebird migration, Delphi publish, FreightOps deployment, seeding or business-data reset was performed. Existing WayTest synchronization remains enabled and healthy; its display name now shows the stored database path after the tenant-settings migration. The one held outbox record was confirmed present in the pre-release backup and was not changed by this release.

Live authenticated checks passed for login/PKCE, local orders/masters/manifests, planning settings, default driver contract, legacy endpoint protection, client assets, both connection/status APIs and review issues. The initial rehearsal stopped on the clone's database-owner permissions (live untouched); correcting the clone owner allowed it to pass. Publication's readiness script initially lacked the established User-Agent and reported failure despite public readiness returning 200; this was corrected, and authenticated verification completed successfully. No database rollback was performed after startup.

Recovery backup: `/opt/aurora-auth/backups/20261028-1-attempt2`. This release includes the earlier tenant-settings schema conversion: do not roll back only the application images without a compatible schema recovery plan. Original uploaded archive SHA256: `d29094e1f6f1dd67dd2c47bdde6157cb6cbb24bbc8ddd24874d58f84583f7b43`; deployment/verification helpers were corrected separately afterward without changing application binaries. API image: `sha256:3b324370bdd5356778d19459b31f079052cc67f239e797923b0ba71a14739652`. Web image: `sha256:42b9e2a8d96b6404fb8820e83062ed08784698b960f06266b57ca37080df4e62`.

Logs: `artifacts/mvp-publish-attempt2.log`, `artifacts/mvp-live-verification.log`, `artifacts/mvp-final-status.log`. Bryan will publish FreightOps to the test server before configuration and the integrated walkthrough.


## Bidirectional planning update — 2026-11-02

Aurora release `20261102-4` removes the artificial FreightOps handoff lock. Initial manifest publication is still explicit; later edits and cancellations automatically enter that connection's durable outbox. Published planning routes can be edited and reoptimized. FreightOps applies membership changes to both native manifest and PRO aggregates in one transaction, checks stream versions, preserves receipt-based retries, and rejects changes after freight handling starts. An empty untouched plan can be voided after audited unassignment; removal no longer incorrectly marks an empty planning queue Loaded.

FreightOps now emits immutable manifest snapshots and cancellation tombstones alongside order snapshots, with exact acknowledgement tracking. Workspace order identities are retained on enrollment/publication. Driver names, truck, date, notes, trailer, membership and native status flow back to Aurora. A changed native driver name clears the old Aurora driver ID rather than associating the name with the wrong person. Native Closed outbound means Dispatched, not delivered/Complete. Assignment snapshots can import alongside order snapshots without deadlocking on the Nova writes queued by the same batch. Assigned orders accept scheduling and notes-only updates while native freight changes remain guarded.

Validation: 170 real endpoint/database checks, 50 transport checks and 239 native core tests passed. API/client and native core Release builds passed. Existing compiler/package warnings remain. Published Lykes FreightOps API and daemon image `aurora-bidirectional-20261102-v2`; other projects unchanged. Backups: `/srv/freightops/lykes/aurora-bidirectional-20261102-v2/freightops.dump` and `/opt/aurora-auth/backups/20261102-4`. Authenticated post-deploy checks verified login, orders, manifest lock removal, duplicate preflight, release marker and preservation of the 50 fixtures.

Limits: native Nova writes remain disabled (`NativeReadOnly=true`, `WritesEnabled=false`) by the prior MVP configuration. This publication does not claim that Aurora changes are being written into Nova. FreightOps execution transitions still require their validated physical workflows; editing a planning status is not a substitute for loading/releasing/dispatch commands. Native manifests outside the linked cohort are not automatically enrolled. Reverse membership requires enrolled orders with known Aurora identities.

At Bryan's request, created 50 synthetic optimization orders in Lykes Test, PROs `99003001`–`99003050`, terminal TPA, ship date 2026-11-02, reference prefix `TEST50`. All were verified ReadyToRoute, unassigned and unlocked with distinct destination coordinates. Added explicit synthetic v2 freight lines (TEST50-FREIGHT, stable line IDs, pounds/cubic feet, one barcode per piece) and saved through the normal order endpoint to queue FreightOps. The initial rejected fixture deliveries are retained as superseded audit records; real orders were not changed. Existing demo fleet capacity was not changed.

Live follow-up found that the legacy SystemUnitUpdated event constructor parameter did not match its serialized SystemUnit property, causing event replay (but not stored snapshots) to lose the assigned truck. Renamed the constructor parameter and added a replay regression assertion. The first replay generated three incorrect manifest deliveries; two had reached Aurora. A scoped transactional repair retired those malformed deliveries and appended audited native truck events using the unchanged stored truck assignments. Corrected snapshots restored Aurora's assignments. The repair used production Marten schema configuration and left domain history intact. Final live FreightOps status: last_error null, pending 0, working 0, held 0, field_conflicts 0. Nova still has queued outbound records because native writes remain deliberately disabled.


## Freight-line descriptions — 2026-11-02

Read-only investigation of historical FreightOps staging records and the connected Nova test database confirmed that FreightOps stores SKU descriptions on freight lines, with individual tracking units under each line. Shared PRO 1862 had Nova description ANTENNA AND RADIO EQUIPMENT but a blank native FreightOps SKU description. Historical records such as 00012759481 retain separate HEADBOARD and RAILS AND FOOTBOARD lines with separate barcodes. This investigation did not infer mixed-pallet contents from PackItemCount.

Fixed the supported mapping: Nova projection now includes a first-class Description on every freight line. FreightOps maps that description onto the native SKU, copies all other existing SKU attributes, exports the current description back to Aurora, and recognizes description-only edits even though native SKU equality excludes description. Explicit empty description clears are supported for otherwise editable freight. Missing legacy Description preserves existing native text; new legacy payloads can obtain it from retained Nova metadata. Existing line IDs, quantities, pack counts and barcodes are preserved by the native update command.

Tests: 177 real endpoint/database checks and 285 Nova checks passed, including two freight lines with distinct descriptions and barcode sets, description updates and explicit clears without barcode replacement, legacy payload compatibility, and Nova projection through the real FreightOps endpoint. Reverse Nova description changes remain held under the existing write policy. No missing-SKU or pallet/barcode interpretation was changed. Existing snapshot fingerprint, assigned/scanned-freight guards and disabled Nova writes remain in effect; this release does not mass-backfill older shipments or bypass reconciliation.

Release: Aurora 20261102-5 and Lykes FreightOps API/daemon aurora-line-descriptions-20261102. Backups retained at /opt/aurora-auth/backups/20261102-5 and /srv/freightops/lykes/aurora-line-descriptions-20261102/freightops.dump. Prepared a draft email (not sent) asking Mike to confirm missing-SKU handling, physical piece/pallet barcode rules, and reverse-edit ownership before expanding these behaviors.


## Practical freight defaults — 2026-11-02

Bryan authorized proceeding with assumptions rather than waiting for mapping answers. One Nova freight row remains one FreightOps line; Pieces determines its tracking-unit count. Thus one row with 12 pieces is one line with 12 barcodes, not 12 lines. Two rows with 12 and 2 pieces remain two lines with 14 barcodes. Pallets remain source metadata and do not add barcodes. No mixed-pallet contents are inferred.

Read-only Nova snapshots now accept blank or repeated source SKUs. Those lines receive deterministic internal AUR- codes, with the original value retained in Nova.Sku and SkuGenerated=true. Distinct rows and repeated identical rows remain separate. Codes avoid collisions with unique source SKUs. Source order, native resequencing and rating amounts do not alter the unchanged operational snapshot. Unique existing SKUs retain the previous projection. Nothing writes these generated codes into Nova.

Existing physical-freight fingerprint reconciliation remains enforced: a later source freight change cannot silently replace barcodes. Unmapped keywords/units remain explicit errors. FreightOps assignment/scanning guards remain enforced. No bulk historical backfill was performed.

Validation: 292 Nova checks and 181 real FreightOps endpoint/database checks passed, including missing/repeated SKUs, collision handling, unchanged snapshot identity, native line descriptions/counts, exact barcode retention on retry and original-SKU metadata on return. Aurora Release build passed. This change requires Aurora only; the previously published FreightOps description support already accepts these lines.

Nova writes remain disabled. The connected test database lacks the durable native line identity required for safe reverse mapping through rerating. Internal snapshot IDs are deliberately not used as native row IDs. This is an implementation prerequisite, not an outstanding question for Mike; full Nova freight write-back is not claimed by this release.

Published Aurora 20261102-6. Backup: /opt/aurora-auth/backups/20261102-6. Authenticated verification passed for orders, imports validation, duplicate preflight, release marker, all 50 test orders and unlocked planning manifests. Live FreightOps sync: no error, pending 0, working 0, held 0, field conflicts 0. Nova outbound pending 216 with writes disabled. Integration Hub conversion tenant environment settings remain absent; document conversion was not exercised by this release.
