# Aurora route review — September 24, 2026

Deployed test release `20260924-5` to user.aurorasoftware.com. Client changes only; reused API binaries from release 4. No database migration or order/manifest reset.

- Three steps: Orders & trucks (includes appointments), Optimize, Review routes.
- Reduced padding and gaps. Daily truck availability and appointment exceptions share one setup screen.
- Wizard sends a fixed 60-second search duration; no search-time input or short quick-run override.
- Polling remains every five seconds. A completed or stopped run stays on Optimize with an explicit Review routes action rather than jumping ahead.
- Route picker selects one truck's stops and map. Removed result exports, Adjust and rerun, Summary and Left off tabs.
- Unassigned PROs and reasons remain visible in a disclosure before Finish. Finish still creates one manifest per populated vehicle route.
- Manifest workspace opens a single saved truck route with its map and ordered stops; no redundant Routes sidebar entry.
- Road-routing behavior is unchanged; saved manifest maps explicitly use bird's-eye lines.

Validation: release client publish succeeded without warnings, 85 routing checks and 31 planning-session checks passed. Browser verified combined setup, changing progress values, stop/completion staying on Optimize, explicit Review routes, and live saved-manifest map/stops. Synthetic local progress harness does not model populated route results.

Archive SHA256: `7646f2ae8abf34a537ae4f328314755988b0e0148d144f5b2834c2ba192e5c61`.
Previous release 20260924-4 is available for rollback (same schema/API).

Live PTV smoke check: submitted the built-in three-order synthetic sample with the fixed 60-second budget, observed live progress, stopped at about 25 seconds, and retrieved the final plan at about 30 seconds. It stayed on Optimize until Review routes was clicked. Two truck routes and one unassigned order were shown; switching to TEST_TRUCK_2 changed the map and stop list to TEST_NORTH. No workspace orders or manifests were created by this smoke check.

Follow-up release 20260924-7 restores separate setup tabs at Bryan's request: Trucks → Appointments → Optimize → Review. Appointment rows retain inline expansion/editing on their own tab. Next/back controls follow the four-step order. Standard deployment automatically resets demo orders/plans and preserves fleet/identity (see TEST-DATA-RESET.md). Archive SHA256: 1c4ecf100a242ea555ba6f2b38f3e16ac6fab279702d91cec31b6e68fa4240a7.

Release 20260924-8: compact viewport-height Trucks and Appointments setup. Selected-order drafts omit the redundant delivery-file card. Full-width matching rows expand via their main button, with truck availability a separate accessible toggle. Compact appointment windows appear beside the order name. Back/Next stay outside internally scrolling lists. Standalone file imports remain available. Archive SHA256: efeaf35b4e43bc8546d8ff2bb9f9be5fcbb6e2346f5658c70345538a4af4ab40.

Release 8 live verification: selected all 100 restored orders and opened a QA draft without optimizing or creating manifests. At 1280x720, both Trucks (six units, first expanded) and Appointments (100 orders, first expanded) had document height exactly 720px and footer bottom 708px. List content scrolls independently; navigation remains visible. Viewport override reset after verification. Reset backup: /opt/aurora-auth/private/orders-pre-reset-20260924-064958.

Release 20260924-9: after successful Finish, notify the parent workspace, focus the opener, and close the wizard. If browser policy blocks closing a directly opened tab, redirect it to /aurora/manifests. Failed saves keep the wizard open with its existing retry behavior; post-save browser failures redirect rather than report a false save failure. Four mocked JavaScript checks cover closable/nonclosable windows with available/blocked local storage. Release publish passed. Saved routes remain accessible under Aurora → Manifest; publication still resets demo manifests by request.

Release 20260924-11 removes the four oversized monitor statistics cards and uses the existing compact history table. Optimize inherits compact header/step styles; the panel fits its content. Estimated cost remains clearly labeled in the progress table. Simulated browser run verified updates and the Stop action. Finish continues to set included orders to Routed in the same database transaction as manifest creation; unassigned orders remain ReadyToRoute. Archive SHA256: ecc2d265e48190bc90b24e85d402e4c865218c9354a2cf3203dc51dd1fb6391c.

Release 20260924-12 changes the Orders From default to the current month’s first day at midnight; To remains today at 23:59:59. The existing OnParametersSetAsync → Refresh path applies these filters on initial load, without requiring Apply filters. Browser-local values are still converted to UTC for the API.

Release 12 also starts optimization directly from Appointments → Continue to optimization; no second Start button. Entering Optimize directly starts a first run if no completed session exists. Existing in-progress guards prevent duplicate submission; completed runs stay available for review. Browser simulated run verified direct transition to progress. Final archive SHA256: 8c15660a6704471c4da9cb03c0774dbeb6816cdcbd87af835dd1c45e6797f9a1.
