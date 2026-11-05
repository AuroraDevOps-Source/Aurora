# Nova terminal receipt and routing availability

Implemented locally on 2026-11-04. Nova owns arrival/receipt; Aurora refresh recognizes the result. No new arrival UI, linehaul optimization, VIA grouping, region grouping, or Nova schema changes.

## Native evidence

Source root: `D:/Development/NewTransEaz`.

- `Operations/MfstTypes.pas`, `TManifest.WritePkupDelv` around 2050: completing a manifest puts undelivered delivery stops on DOCK at the manifest's current status location, clears their driver/equipment assignments, and keeps their delivery time unset. Pickup completion also propagates DOCK to undelivered deliveries. Manifest links can remain as history.
- `Setup/Sql/Operations-5-79.sql`, `SET_ORDER_HDR_STATUS`: copies stop status, location type, and location to the order's `PB_STATUS_*` fields. It does not change `PB_LOCATION`.
- Current freight location: `OPS_LOC_TYPE` / `OPS_LOCATION`, with `PB_STATUS_LOC_TYPE` / `PB_STATUS_LOCATION` as the header fallback. `L` identifies a terminal; an account or equipment location is not interchangeable with a terminal code.
- `OrderEntry/OrderEntryDM.pas`, `GetRoute` around 8227: service terminal and route come from ZIP (`CTZ_TERM`, `CTZ_ROUTE`), falling back to the route's `CTR_LOCATION`. `OPS_DELIVERY_TERMINAL` is the service terminal, not proof of physical receipt.
- `OrderEntry/FraAccount.pas`, `LookupVIA` around 1160: looks up origin/destination service terminals and assigns `TS_VIA1` / `TS_VIA2` to `PB_VIA_1` / `PB_VIA_2`. These are intermediate routing terminals. No new Aurora interpretation of these conventions is introduced.
- `OrderEntry/OrderEntryDM.pas`, `GetRegion` around 8193, and `Operations/OperationsDM.dfm` around 2309: regions derive from ZIP/postal ranges (`CT_REGION_RANGES`). Region membership does not establish current custody or receipt.

## Aurora behavior

Delivery planning uses the stop's terminal location first, then header status location. Original `PB_LOCATION` is a fallback only for older records with no location type. Explicit nonterminal locations never silently fall back to the origin. Pickup terminal behavior is unchanged.

A valid terminal DOCK status is recognized as received freight in addition to configured ready statuses. Stop status takes precedence over header status, so a stale ready header cannot make an in-transit stop available. Final deliveries, agent handoffs, canceled orders, active assignments, and unresolved manifest references are excluded. Arrival (`ARI`) alone is not treated as receipt.

Completed (`CPLT`) manifest references may remain only when explicit DOCK terminal custody is present. Both the direct assignment and every bridge link are checked. Export revalidates custody and assignment under order/stop locks, rejecting stale old-terminal plans. Native historical links are preserved when creating the next manifest.

Orders display/filter by current terminal. Refresh withdraws old-terminal snapshots by source plus order ID, resets released native orders to Ready to Route, and releases obsolete local assignment indexes. Native links and saved manifest route data retain trip history. Open plans protect their snapshots; an old-terminal plan must be canceled before the same freight can be planned at its new terminal. A receiving override matching an explicitly received terminal is consumed so it cannot create a B-to-B leg. Linehaul optimization algorithms are unchanged.

## Validation

`tests/TerminalReceiptChecks` creates and drops an isolated embedded Firebird database. It exercises the production eligibility predicates and export validation, including receipt at B with origin A, retained history, active/reopened/reassigned manifests, stale origin plans, canceled/delivered/agent freight, unknown locations, and pickup behavior.

Set `FIREBIRD_EMBEDDED_CLIENT` if the embedded Firebird client is not installed at its standard Windows path. Optional `TERMINAL_RECEIPT_PG` uses a disposable local PostgreSQL cluster owned by `terminal_check`; snapshot checks use temporary tables and roll back.

30 receipt checks passed with both database engines. Nova integration project builds without warnings. The non-live DispatchChecks suite passed (17 checks). FirebirdRoutingChecks stops at its pre-existing coordinate expectation: current workspace DataRequirements flags missing coordinates, while that test expects no issue. This unrelated code/test disagreement was not changed. Initial validation did not modify tenant data. Subsequently deployed API release `20261104-terminal-receipt-1` at the user's request; see the deployment log. Authenticated live order/terminal filters and readiness passed. Full operational receipt-to-next-manifest rehearsal remains outstanding.
