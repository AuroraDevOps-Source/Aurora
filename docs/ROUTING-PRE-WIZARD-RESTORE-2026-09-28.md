# Pre-wizard planner restored — September 28, 2026

Restored the Route Optimization layout from commit `87a5aaf` in `Routing.razor`: fleet/settings and order preview map, followed by Manifest / Summary / Left off tabs alongside the route map, exports, and scenario comparison.

Orders → Route Optimization continues to create a selected-order draft and opens `/aurora/planning?draft=...`. The planner loads that draft instead of offering file import or demo replacement. Existing authenticated planning sessions, refresh recovery, stop/review, and Finish/Create manifests remain connected. Quick runs use five seconds; full runs use the configured duration.

All pre-restoration uncommitted work, including untracked files, is saved locally in the Git stash named `Before restoring pre-wizard planner 2026-09-28`. Supporting Orders, layout, and local-environment work remains in the working tree; the newer planner UI has been replaced.

Validation: client and preview build succeeded without warnings; 17 order workspace checks, 31 planning session checks, and 43 map checks passed. Browser verification selected only TEST_NORTH in Orders, opened its draft with one order and a populated preview map, ran simulated optimization, stopped it, and confirmed the original manifest/results tabs and route map. No live PTV run or manifest save was submitted.
