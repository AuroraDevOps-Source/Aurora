# Single-form route planner — September 27, 2026

The planning screen now keeps truck availability, appointments, optimization controls, and results on one page. The numbered wizard and next/back navigation have been removed. Completed or stopped planning sessions render the map and manifests automatically.

Planning starts in the authenticated Orders workspace: apply date/status/search filters, select individual or all eligible visible orders, then choose Route Optimization. The server creates the selected-order draft and the same window navigates to `/aurora/planning?draft=...`. The shared product header/SSO and Orders/Manifest/Fleet sidebar remain present. Direct visits without a draft return to Orders; the order-file picker and demo-loading workflow were removed. Successful Finish navigates to Manifest instead of closing the window.

The existing draft API still accepts Ready to Ship orders from one planning source per run and requires stored routing data. This change does not add routing/geocoding for manually created orders or combine planning sources.

Map controls support all manifests, one manifest, and unrouted orders alone. Unrouted markers are visible by default and can be toggled alongside any manifest selection. Map bounds include the visible unrouted locations. Each located unrouted order can be focused from its reason list; orders without coordinates remain listed with an explicit notice.

Editing planning inputs retains the prior result for reference, displays an outdated-result notice, and clears eligibility to create manifests until another optimization succeeds. Inputs are disabled during optimization and manifest saving. Re-optimization uses the whole current planning input; selecting a manifest filters the review only.

Validation: client build completed without warnings or errors; map checks and 31 planning-session checks passed. The local PlannerPreview harness now supplies two synthetic manifests and one unrouted order and uses the real map component. Browser checks covered single-form setup, progress/stop, automatic result display, manifest filtering, unrouted toggling and unrouted-only view, and retaining results after input edits. No live PTV submission, production manifest creation, or deployment was performed.

Workspace follow-up: 17 non-database order-workspace checks passed. Browser verification filtered to one order, selected it, and confirmed that only that order reached the planner with sidebar links and no order-file input. Full local SSO/database testing could not start because `ConnectionStrings:Aurora` is not configured. The harness at `/aurora/orders` is explicitly synthetic, does not sign in, and rejects persistent order/manifest changes.
