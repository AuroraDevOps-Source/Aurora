# Lykes endpoints and first field mapping

Superseding confirmed Lykes rules: [mapping v2](LYKES-MAPPING-V2-2026-10-27.md) supports multiple terminals, one barcode per piece, and cubic feet per piece. The v1 details and email below are historical; the barcode and FreightOps cube questions have been answered.

Local implementation, October 27, 2026. Nothing deployed or enabled; no remote business writes or Nova runtime changes.

## What Nova code establishes

- `OrderEntryDM.pas:1626`: `OPL_WEIGHT` is pounds. Nova converts it through `PoundsToKilos` into separate `OPL_KILOS`, converts back with `kilosToPounds`, and uses 2,000 pounds per short ton.
- `OrderEntryDM.pas:638`: pieces, pallets and pallet positions are separate fields. `EDI/EDI204ToOrder.pas:3008` sends `PLT`/`PAL` quantities into `OPL_PALLETS`; other quantities go into `OPL_PIECES`. Commercial pieces are not automatically barcode counts.
- `OrderEntryDM.pas:6142`: cube accepts a scalar, two dimensions (area), or three dimensions. A leading `C` converts centimeters to inches before multiplication; that routine does not divide the product by 1,728. `AurRating/AurRatingIntf.pas:623` has a related scalar/dimension parser. Raw `OPL_CUBE` cannot universally be treated as cubic feet.
- The inspected Pascal source did not establish Lykes's actual terminal, barcode grouping, or cube-entry convention. Previously inspected test/staging data is not proof of the Lykes installation.

`LykesNovaMeasurements` now implements the verified weight interpretation, explicit scalar/dimension volume conversion, and a required piece/pallet barcode rule. It rejects conflicting pound/kilogram values, fractional handling units, area-as-volume and unspecified scalar units. Retain raw cube text. Nova UUID persistence and wiring its line projection into live synchronization remain outstanding.

## Implemented FreightOps endpoints

An isolated machine branch serves POST requests under `/api/aurora-sync/v1`; legacy controllers/imports and user authentication are unchanged:

- `changes`: versioned order create/update/cancel and immutable receipts.
- `pull`: bounded pending shipment snapshots.
- `acknowledge`: exact connection-bound delivery IDs; retries are safe.
- `enroll`: explicit existing-PRO enrollment with operation `enroll`, expected native version and an empty profile `Lines` array. It adopts missing IDs through events and returns native lines for reconciliation, preserving freight/barcodes. It does not guess matches to Nova lines.

`AuroraSync.Enabled` defaults to false. Enabling requires `ConnectionId`, generation-specific `SourceIdentity`, `TokenSha256`, `OriginTerminal`, `NativeWeightUnit` (`lb`/`kg`) and `NativeVolumeUnit` (`ft3`/`m3`). No real values were installed. The bearer credential is scoped to this installation/connection and checked against its configured SHA-256 digest in constant time. Deploy behind TLS. A request cannot choose a different tenant/database.

Domain events, enrollment, source-only projection data and receipts commit in one Marten transaction. The target checks receipts before stale versions, enabling response-loss retries. New `LykesProjectionRecordedEvent` markers give retained commercial/source-only data a stream revision too. Schema registration adds `SyncLink`, `SyncTargetReceipt`, and `SyncDelivery`; prepare their schema using the existing Marten deployment process before enabling. Upgrade event consumers together; backward-compatible payloads do not mean old binaries understand new events.

## Mapping profile

The shared order retains Aurora's Pascal-case fields and adds a `Lykes` object with `Version: 1`, explicit `WeightUnit`/`VolumeUnit`, and a complete `Lines` array. Each freight line requires a stable UUID `LineId`, `Kind: freight`, nonblank unique `Sku`, integer `HandlingUnits`, `PackType`, `TotalWeight`, and `TotalCube`. PO, BOL and notes are mapped to native references. `Kind: charge` rows are retained as information and never create physical units or overwrite native revenue.

| Shared field | Native behavior |
|---|---|
| `Id` | Explicit PRO identity; existing streams conflict with create. No guessed numbering. |
| `TerminalCode` | Must match the configured origin cohort. |
| Shipper/consignee names and address fields | Supported native updates; existing contacts, suites, dock/classification and other unmapped details are preserved. |
| `Notes` | Native comment; explicit empty/null clears. Omitted fields in existing complete snapshots are rejected. |
| Stable line ID/SKU | ID-based matching. Replacing an ID, correcting a SKU, or repeated/missing SKUs requires review. |
| `HandlingUnits` | Physical barcode count. Existing barcodes stay stable; new units receive the specified pack type. |
| Line total weight/cube | Explicit conversion to native units and native per-unit values. Planning header `Weight`/`Cube` are pounds/cubic feet and must agree when supplied. |
| `Pieces` | Commercial quantity retained separately from barcodes. |
| Charge/source-only data | Retained in canonical data and projection events. Not posted as native billing. |
| Execution state/parent | Exported separately as `FreightOpsState`/`FreightOpsParent`; no fabricated scans or commercial status transitions. |

Initial creates are standard/direct deliveries ready for planning. Writes/cancels are limited to Created, unassigned, unsplit, unscanned PROs. Revenue-bearing removals/cancellations, native total-per-line measurement mode, pack-type changes and quantity changes involving inactive units require review. Domain guards still run; there is no force-update bypass. Appointments, billing/pay terms, pickup scheduling, manifests and split execution are not supported business mutations in this profile. Unmapped existing header changes are rejected, not reported as applied.

## Feed behavior and remaining scope

The feed compares committed **per-PRO** stream versions with their acknowledged versions, not a global event sequence watermark. Pull uses repeatable read and an exact-version snapshot or replay, then durably freezes the response until acknowledged. Later native writes remain discoverable after older snapshots are acknowledged. Legacy writers need no new capture hook for current-state synchronization.

This is a **current-snapshot feed**, not a feed of every intermediate scan/split/milestone. Native history remains in the event log; complete execution-event mapping remains required before full Lykes activation. Enrollment is explicit. Source restoration and missing/archived streams require reconciliation.

Initial bounds: 200 snapshots, 4 MiB request/response, 200 input lines, 2,000 handling units per shipment, and 500 enrolled PROs. Oversized/unsupported records fail visibly. Larger-cohort capture and poison-record isolation remain rollout requirements. Aurora scheduler activation, Nova line UUIDs, repeated SKUs, split lineage, Lykes configuration confirmation, and PostgreSQL 12 staging compatibility remain outstanding.

## Local verification

Final results: 37 real endpoint/database checks and 238 FreightOps core tests passed. FreightOps API and daemon builds passed. Endpoint test output is saved in `artifacts/lykes-endpoints/results.log`.

`tests/FreightOpsEndToEndChecks` runs the real Aurora adapter against the real endpoint source/domain service on loopback and disposable PostgreSQL, including native PRO and tracking-lookup projections. The endpoint source is linked into this harness so normal FreightOps startup/seeding/background services do not run. The actual FreightOps API is built separately.

Set `FREIGHTOPS_SYNC_TEST_CONNECTION` to an empty local `freightops_sync_check_*` database and run `dotnet run --project tests/FreightOpsEndToEndChecks`. The project expects sibling checkout `../FreightOps-All/FreightOps`; override MSBuild `FreightOpsRoot` if necessary. It is intentionally outside the production Aurora solution. The harness refuses nonlocal/nonempty databases.

Coverage includes retries, conflicting reuse, concurrent creation, stale versions, receipt rollback, explicit clears, partial snapshots, quantity/barcodes, tracking lookup, charge retention, enrollment, cancellation, native-write discovery, authentication, disabled behavior and measurement rules. Local PostgreSQL 18 is not PostgreSQL 12 certification. Existing Marten advisories remain unresolved and need attention before deployment.

## Revised email draft — not sent

**Subject: Confirming the remaining Lykes mapping details**

Hi Steve and Mike,

We're working on the Nova–Aurora–FreightOps integration for Lykes. The Nova code confirmed that its weight field is pounds and that it stores pieces and pallets separately. Cube can be entered as a total or dimensions, so we still need to confirm Lykes's convention.

Could you remind me:

- Which FreightOps origin terminal code should we use for Lykes?
- Should each barcode represent a carton/piece or a pallet? If pallets, where do we get the pallet count and cartons-per-pallet relationship?
- Does Lykes normally enter cube as a total volume or dimensions? What units do they use for scalar cube and unprefixed dimensions?

A sample Nova shipment and corresponding FreightOps labels would help verify the mapping. I know we've discussed this before, but I can't remember the decisions.

Thanks,
Bryan
