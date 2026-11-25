# Aurora Test dataset rebuild — November 25, 2026

Completed the user's request to reset incomplete Orders and Equipment data. Verified tenant `9e49de1d-f4e2-4f84-91e0-443b1635217f`, host `172.23.0.5`, database `D:\db\LykesTest\OPERATIONS.FDB`. Other tenants have different database targets. The test integration worker remains disabled. The cleanup also exposed a reset dependency on native delete triggers; API release `20261125-test-reset-1` explicitly removes manifest membership, route stops and driver assignments. Web remains `20261125-equipment-types-2`; settings are unchanged.

## Cause and scope

The active dataset contained 3,089 orders, 2,389 with issues, and 249 equipment records, 245 missing weight/cubic capacity. The original 500 DEMO500 synthetic orders were all warning-free and unassigned. The earlier partial repair left incomplete imports and copied fleet data alongside the valid demos. The equipment-type release did not reset these records. The original Crown workbook lacked street-address columns; its records could not become real geocoded deliveries without additional source data.

## Backups and changes

Completed a full Firebird service backup to `D:\db\LykesTest\AuroraTest-before-reset-20261125.fbk`. PostgreSQL custom-format backup and native row snapshots are in `/opt/aurora-auth/private/aurora-test-reset-20261125/`. Native headers, stops and equipment were compared against their snapshots inside the mutation transaction before proceeding.

Archived 2,589 non-demo orders and 35 old manifests using cancellation timestamps. Soft-deleted 249 old equipment records and 236 old drivers; native history and recovery backups remain. Cleared only Aurora Test's stale planning manifests/drafts, route plans, workflow state and unsent sync work.

Kept the original 500 explicitly synthetic orders, PROs 99010001–99010500: 250 each at TPA and MCO, with November 25 appointments. Their synthetic map locations remain labeled as test locations, not verified real deliveries.

Created 40 synthetic units and 40 synthetic drivers, 20 each per terminal. Each terminal has two units of all ten saved equipment types. Template capacity, dimensions, distance costs and routing profiles are saved; equipment hourly cost is explicitly zero and every unit has a default test driver. Units use `TEST-TPA-01A` through `TEST-TPA-10B`, and corresponding MCO names, with sorted template indices. Driver numbers use `DRV-`. Native TRK units represent complete dispatchable rigs, including trailer-type templates. Optimization still uses as-needed equipment types independently of physical inventory.

## Verification

Live application services verified 500 orders, 40 equipment records and 40 drivers, with zero order, equipment or planning warnings, and exactly two compatible units per type at each terminal.

Actual PTV optimization, native manifest creation with a required type and no physical assignment, and persisted Dispatch driver/unit assignment passed using two demo orders at each terminal. The initial cleanup left four membership rows and two route stops after their manifest headers were deleted, so the native readiness query withheld those orders. Removed only those known verification leftovers with a guarded, backed-up transaction. Corrected `WriteResetManifests` to remove its owned operational children explicitly while retaining history and ownership checks. Added a disposable Firebird regression without delete triggers, including rollback, return-to-planning, unrelated-manifest preservation and retry checks; all 23 ResetChecks passed. The candidate API repeats the actual optimization/manifest/assignment/cleanup flow before publishing and verifies all 500 orders ready, zero warnings, zero active manifests and zero open drafts.

Maintenance helpers: `artifacts/AuroraTestReset` and `artifacts/run-test-reset.py`. Server receipts: `archive-complete.json`, `created.jsonl`, `smoke-complete.json`, `verified.json`, `reset-release.json`. API rollback backup: `/opt/aurora-auth/backups/20261125-test-reset-1`. The existing web, PostgreSQL and FreightOps containers and other tenant databases were not changed.
