# Aurora orders and manifests — September 24, 2026

The native product header now says Aurora and opens `/aurora/orders`. The separate Aurora TMS product is unchanged. A left sidebar links Orders and Manifest. Orders has import, inclusive date/time and status filters, individual/all-visible selection, and a Route Optimization action. Date filters start empty because the test data is dated September 1, 2026. Display/filter times use browser local time; database timestamps use UTC. Orders without appointment dates use the imported fleet shift start.

Per Bryan's clarification only `04-appointment-crunch.json` from the desktop was imported: 100 orders and the original six-truck settings. The baseline file was not imported. Imports are tenant-scoped and idempotent by name; the original PTV request is retained so future imports can keep their own fleet and location constraints. Future import API: admin-only POST `/api/v1/aurora/imports`. This initial UI plans orders within one import at a time.

## Selection, optimization and Finish

Selected orders create a server-side draft belonging to the tenant/user. The wizard opens in a separate browser window (or via the fallback link when popups are blocked). Fleet and appointment changes apply to that draft. Session storage keys are separate per draft. Refresh reconnects to the completed/running session. Closing before Finish does not manifest orders.

Finish uses the server-stored completed PTV result, not a result supplied by the browser. It creates one manifest per populated vehicle route, retaining stop sequence, customer/address details, arrival/departure, costs and route data. Scheduled orders become Manifested; left-off orders remain Available. Saved manifests appear in the Manifest section with expandable order details. Parent windows refresh on focus or a manifest-save notification.

The database transaction locks the draft and scheduled orders, guards against overlapping plans, and makes Finish idempotent. Tenant RLS covers every new table. Draft access and result binding additionally enforce the planning user's ownership. Optimization recovery records expire after 48 hours; saved manifests and order records persist in PostgreSQL. No dispatch to the external Aurora TMS is performed.

## Verification and deployment

Migration 0012 adds five tables without changing existing shipment/product data. It was tested first in an isolated PostgreSQL database, which was removed after success. 23 order-workspace checks passed, including actual desktop-file import, date/status filtering, RLS, ownership, conflict rollback and Finish retry behavior. 31 session, 85 existing routing, 32 map and 16 login checks passed. Release publishing succeeded.

Release 20260924-1 applied the migration and imported the 100 orders on the test server. The protected pre-migration backup is `/opt/aurora-auth/private/equipment-pre-20260924-1-20260924-055040.dump`. Login, embedded-session protections and Aurora/FreightOps/Hub authenticated APIs passed.

Live browser verification checked the renamed header, sidebar, 100-order grid, date/status filters, selected-order handoff, all wizard steps, refresh recovery and Finish. One real PTV run for synthetic order LTL0041 succeeded, and Finish created its manifest on truck TRL-5302. This test manifest is retained for review; 99 orders remain Available. The in-app browser suppressed automatic popups, so its fallback URL was opened in a separate tab for verification.

Release 20260924-3 includes the final button sizing and an explicit route parameter so the sidebar reliably switches between Orders and Manifest, reusing the verified API. No further migration is needed. Previous release 20260923-1 remains an application rollback target; retain the additive tables and data when rolling back.

Final release archive SHA256: `9bdf8aa0650a51954b1a873bda2ebc02bb2d1c0ef88c3cf556acbb9dc4bdce43`.
