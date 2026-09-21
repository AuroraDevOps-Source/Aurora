# Routing planner and equipment catalog

## Planner workflow

- **Equipment catalog** is available before importing an order file. Save reusable types and individual units with terminal and availability.
- **Edit schedule & trucks** supports departure and latest return with date/time pickers and explicit UTC offsets, including overnight shifts. Set the schedule for all available trucks or edit individual trucks.
- Add a quantity of an equipment type for a scenario, or select a specific available unit filtered by terminal. New trucks use a chosen start/return location from the input. Existing trucks can be copied, removed from the run or assigned a different catalog type.
- Capacity, maximum stops and cost assumptions remain editable per truck. Type changes apply the selected type's capacity, costs, routing profile and categories. Equipment type labels persist in the root reporting metadata, which is excluded from PTV requests.
- **Appointments & service time → Edit appointments** searches all imported orders by PRO or destination. Change or clear a reporting appointment, confirmation and service minutes directly.
- Existing native scheduling windows remain constraints by default. The explicit **Replace imported scheduling windows for this order** option isolates that order's location and removes its native time-slot restrictions before applying the new appointment. It does not change other orders at the same original location. This includes removing that order's original opening-hour restrictions, as explained in the form.
- From results, **Edit plan inputs** returns to the current input plan for editing and recalculation. **Compare scenario** retains the existing fleet-comparison flow.

Appointment edits apply to the plan; they do not book appointments or update operational shipment records. Download plan inputs to retain a scenario. Equipment records are stored separately and survive plan resets.

## Equipment storage

Migration `0010_routing_equipment.sql` creates `routing_equipment_type` and `routing_equipment_unit`. Keys include `tenant_id`; units reference types in the same tenant. Both tables enforce PostgreSQL row-level security. Structured record data is stored as JSONB with a last-updated timestamp. Catalog writes follow the routing API's authenticated, tenant-scoped access policy.

Endpoints:

- `GET /api/v1/routing/equipment`
- `PUT /api/v1/routing/equipment/types`
- `PUT /api/v1/routing/equipment/units`
- `POST /api/v1/routing/equipment/import` — transactional, adds missing records without replacing existing records.

Types store payload (lb), volume (ft³), pallet capacity, interior/exterior dimensions (ft), length with power, axles, accessories, door/cargo/truck class, CDL requirement, model, loading position, optimizer categories, profile, stop limit and operating rates. Units store ID/alias, terminal, type, availability, VIN, plate and notes.

Dimensions and CDL are reference fields, not new routing restrictions. The selected optimizer profile controls routing. Categories must match order requirements and do not establish driver certification. Power-only tractors cannot be selected as cargo vehicles. Trailer selections represent powered vehicle configurations; tractor/trailer pairing and driver assignments are not modeled in this change. Availability is catalog availability, not a reservation across simultaneous plans. Concurrent catalog edits use the latest save.

## Spreadsheet samples

Source: `C:/Users/Bryan/Desktop/EQUIPMENT DATABASE FOR TESTING OPTIMIZATION.xlsx`.

The bundled `samples/equipment-catalog.json` contains 14 unique type definitions and 279 unit rows. Identical duplicate type definitions were consolidated. All unit weight and volume values match their associated source types. Sample type IDs start with `TEST-`; unit IDs also include terminal and, where necessary, the source row to distinguish duplicate IDs. Original unit IDs remain aliases. Source rows are retained in notes.

The cargo-van cube values differ from length × width × height. Source values are preserved, with review notes on those types. Missing rates and capacities remain unspecified. Samples are imported only when the user chooses **Add spreadsheet samples**. No sample equipment is automatically seeded into the database.

## Verification and deployment

- Solution Release build.
- `dotnet run --project tests/RoutingChecks -c Release`: 60 checks including equipment mapping, overnight shifts, invalid capacities, power-only rejection, appointments, native-window replacement, original-input preservation and sample integrity.
- `tests/EquipmentChecks`: database persistence/update, non-overwriting import, atomic rollback, tenant isolation and availability. Requires `EQUIPMENT_TEST_CONNECTION` pointing to local PostgreSQL as `aurora_app`; creates and removes only its own fixture tenant.
- `tests/equipment-isolation.sql`: run as `aurora_app`; all fixtures roll back. Checks read isolation, rejected cross-tenant writes and missing-tenant context.
- `tests/PlannerPreview`: interactive local component harness with synthetic orders and in-memory catalog. Run from that directory with `dotnet run --urls http://localhost:5099`. Browser checks covered adding two trucks, appointment changes, catalog saves and capacity edits. A 390px viewport showed no page-level horizontal overflow.

## Test-server deployment

Deployed release `20260915-1` to https://user.aurorasoftware.com/routing on 2026-09-15. Migration 0010 applied successfully to the server database with development seeding disabled. API and client were deployed together; public readiness returned 200.

- Archive SHA256: `fe5e1d0ac5ab1ebd6a5b9a7f5f4e6f042999693b19238e228f3fd9c90b1c2727`.
- Pre-migration database backup: `/opt/aurora-auth/private/equipment-pre-20260915-1-20260915-092052.dump`.
- Prior application release retained: `20260914-5`. Rollback can deploy that release with `--embedded`; the additive equipment tables may remain.
- Explicit test import saved all 14 sample types and 279 units in the test tenant. Availability persisted and was restored, invalid capacity was rejected, and reimport produced no duplicates.
- Two real PTV runs used three synthetic orders. Two trucks scheduled both simultaneous 10:00 appointments; one truck scheduled one. The early appointment was left off. Both runs had zero constraint violations.
- Manifest assertions passed for Ship to, Units, Weight, 20-minute service time, edited 10:00 arrival and an eight-hour 08:30–16:30 shift budget.
- Existing Aurora, FreightOps and Hub authenticated PKCE/API checks passed, including embedded-session and untrusted-origin checks.
- Deployed browser checks passed: catalog loaded 14 types/279 units; selecting TEST-16 BOX applied 6,000 lb/731.25 ft³ capacities; editing departure/return and disabling a truck applied a one-truck plan; editing the north appointment changed the displayed window from 09:00 to 10:00–11:00. A synthetic draft is left open for review.
- Synthetic optimizer results are retained in the server private directory as `equipment-two-truck-test-result.json` and `equipment-one-truck-test-result.json`.
