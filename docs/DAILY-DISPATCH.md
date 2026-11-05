# Daily dispatch board

Open **Daily dispatch** in Aurora's sidebar (`/aurora/dispatch`). The authenticated workspace selects local or native Nova data using the existing workspace-mode endpoint.

The board provides terminal/date/search filters; unassigned, planned and dispatched lanes; completed-work and alert filters; driver/equipment assignments; a map; and links into existing order, planning and manifest editors. It does not change operational records. Use Refresh to obtain a new snapshot; the timestamp identifies when it loaded.

## Interpretation

- **Late:** an unfinished order/stop's appointment end has passed. Nova appointment times are compared in the source time zone; local workspace appointment times use browser local time. A scheduled date alone is not treated as a missed appointment.
- **Blocked:** explicit assignment, data-quality or dispatch-validation issues. Nova consolidation holds are also surfaced.
- **Carry-over:** earlier unfinished work, included by default. This requests progress review and does not assert vehicle inactivity. Undated work stays visible and appears in Needs attention.
- **Planned:** pre-dispatch manifests and unassigned orders reserved by an open optimization draft. These are counted as work items, not solely route counts.
- **Map:** verified coordinates and straight connections between ordered stops, not live telemetry or road routing. Selecting a card isolates its locations. Missing coordinates stay visible in the board and are reflected in map coverage.
- **Resources:** directory status plus matching assignments within the terminal/date/search scope. Lack of an assignment does not establish dispatch availability. Existing manifest validation remains authoritative.

Local work uses the tenant-scoped TMS reads. Native work reads paginated Nova records and authoritative dispatch documents, with a maximum of four concurrent manifest reads. Local snapshots supply only draft reservations in native mode. Completed native stops are excluded from late calculations; prior completed manifests are excluded from carry-over. Native records without appointments are undated. An order with any native manifest association is excluded from the unassigned lane.

The native order/equipment scans stop at 10,000 records and show an incompleteness warning. Large native workspaces can have substantial initial load times. Core-source failures suppress the board rather than presenting zero work; optional fleet/reservation failures are identified separately. No new backend cache or polling is introduced.

## Verification

`dotnet run --project tests/DispatchBoardChecks` exercises date boundaries, source-zone comparisons, completed work, draft reservations, pagination, assignment issues, map stops and failed-source behavior against synthetic HTTP responses.

`dotnet run --project tests/DispatchBoardPreview --urls http://localhost:5287` runs a standalone, read-only synthetic UI preview. It needs no production database or sign-in. Its editor links are destinations in the full app, not preview pages.

## Live validation and deployment — November 3, 2026

Validated read-only against the signed-in tenant's saved Nova connection (`172.23.0.5`, `lykestest/operations.fdb`) using the existing protected credentials and tenant context. The complete loader read 766 orders and 480 resources in 1.35 seconds across 34 requests, with no source warnings. Five open manifests were verified. No Nova business data was changed.

Live validation found two VOID orders misclassified by their display label and 667 records without a known Nova status. The board now excludes completed/void orders by native status code (with a display-label fallback) and flags unknown statuses as explicit review blockers. The underlying unknown-status data was left untouched. After correction the all-terminal snapshot contained 701 unassigned items and five planned manifests. Counts are a point-in-time test result.

All 30 regression checks passed. Release publishes completed; the API still reports pre-existing NuGet advisories for the transitive System.Security.Cryptography.Pkcs 9.0.0 dependency. Release `20261103-10` deployed API and web to `https://user.aurorasoftware.com/aurora/dispatch`, preserving API settings, sync configuration, and the database/FreightOps containers. Authenticated checks verified every order page, all five open manifest dispatch/stop reads, supporting sources, the dispatch page, stylesheet, and release marker. The initial deployment attempt rolled back on a TLS connection reset during verification; retry succeeded. Backup: `/opt/aurora-auth/backups/20261103-10`.

`tests/DispatchBoardLiveChecks` is an explicit read-only harness: run its published DLL with `read-only` inside the existing API container and supply tenant/user UUIDs as JSON on stdin. It resolves the existing saved connection through the tenant-scoped factory and data-protection key ring; it never prints credentials. Do not substitute a different source or enable mutation checks.

