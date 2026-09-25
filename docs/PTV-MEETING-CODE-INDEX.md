# PTV meeting code index

Reviewed 2026-09-22. Local source evidence, not confirmation of deployed configuration or current vendor entitlements. Aurora is the company; the planner, Hub, and TMS are its applications. No external calls were made to build this index.

## Identify the application first

| Path | Responsibility | Evidence |
|---|---|---|
| Aurora file-upload planner | Browser draft, direct OptiFlow call, results/export, seeded Quick update | D:/Development/Aurora/src/Aurora.Client/Pages/Routing.razor |
| Integration Hub legacy batch workflow | Nova batch -> geocode/translate -> optimize -> conditional Nova manifest creation | D:/Development/Integration-Hub/Hub.API/Features/Optimization/Services/RouteOptimizationService.cs |
| Integration Hub PTV gateway | Account credential resolution and explicit PTV service forwarding | D:/Development/Integration-Hub/Hub.API/Features/Ptv/ |
| Aurora TMS (Nova 2.0 repository) | Separate optimizer/request builder, physical vehicle parameters, routing/geocoding clients | D:/Development/Nova 2.0/src/Nova.Api/Routing/ |

## File-upload planner: quick lookup

Paths below are relative to D:/Development/Aurora. Search symbol names if lines move.

| Question | File / symbol | Verified behavior |
|---|---|---|
| What happens after upload? | src/Aurora.Client/Pages/Routing.razor: SetInput, RunOptimization | Parses draft, previews locations; no optimization until requested. Multipart file + mode to API. |
| Upload API/limits | src/Aurora.Api/Features/Routing/RoutingEndpoints.cs: RunOptimization | Authenticated POST /api/v1/routing/optimizations; JSON input up to 25 MB; quick mode also supplies previousResult. |
| Exact outgoing request | src/Aurora.Modules.Routing/Optimization/OptimizationService.cs: OptimizeAsync | RoutingInput.Prepare -> optional QuickUpdate.Prepare -> RemoveReportingSidecar -> PtvClient.RunAsync. |
| Appointment interpretation | src/Aurora.Contracts/RoutingInput.cs: Prepare (line 76) | Confirmed reporting appointments become earliestStart/latestStart slots on isolated per-order locations; existing latestEnd deadlines remain. Unconfirmed reporting appointments are informational. |
| Appointment edits | src/Aurora.Contracts/PlannerEdits.cs: Appointment | Optional replacement of native windows; otherwise retain native restrictions. |
| Display-only data | src/Aurora.Modules.Routing/Optimization/ManifestReport.cs: RemoveReportingSidecar (line 54) | Remove root reporting before PTV; retain it locally for result enrichment. |
| Equipment mapping | src/Aurora.Contracts/Equipment.cs: EquipmentPlanning.Vehicle (line 72) | Weight/volume/pallet maximumLoads, stop limit, rates, categories, profile, shift/depot. Physical dimensions/CDL remain catalog data in this builder. |
| Fleet edits | src/Aurora.Client/Pages/VariationModal.razor; src/Aurora.Modules.Routing/Optimization/FleetSettings.cs | Verify current UI call site before describing bulk effects; FleetSettings is a first-vehicle-template helper. |
| Transport/polling | src/Aurora.Modules.Routing/Optimization/PtvClient.cs: RunAsync (line 63) | New POST /optimizations; GET /optimizations/{id}; server-side ApiKey header. No provider cancellation or retry/backoff implemented here. |
| Timeout defaults | src/Aurora.Modules.Routing/Optimization/PtvSettings.cs | Poll interval 2s, poll timeout 180s, individual HTTP timeout 60s; configuration can override. |
| Quick update/caching | src/Aurora.Modules.Routing/Optimization/QuickUpdate.cs: Prepare | Previous route tasks seed a NEW optimization; settings.duration=5; reconstructionPolicy.violations=CLEANUP; current constraints applied. Not a resumed job or internal matrix cache. |
| Quick update lifetime | src/Aurora.Client/Pages/Routing.razor: _previousResult, RunOptimization | Last successful raw result in browser memory. Failures/cancellation retain it; successful rerun replaces it. New file/demo/new plan clears it. Refresh loses draft. |
| Result extraction | src/Aurora.Modules.Routing/Optimization/RunSummary.cs, RouteDetail.cs, ManifestReport.cs | Metrics, routes, unscheduled reasons, warnings, joins to original input/reporting. |
| Time display | src/Aurora.Modules.Routing/Optimization/RoutingSchedule.cs | Explicit source offsets; not automatic named-timezone/DST modeling. |
| Road map warning | src/Aurora.Api/Features/Routing/RoadRoutingEndpoint.cs: Calculate (line 11) | Separate Routing API GET using stop coordinates/profile, AVERAGE traffic, includeLastMeters; chunks of 20 overlapping points. violated=true returns generic 422 warning; exact restriction is not exposed. |
| Road map UI | src/Aurora.Client/Pages/Routing.razor: ChangeMapMode | Requests geometry/instructions separately from optimization. Bird's-eye remains available. Successful optimization does not resolve road restriction warning. |
| Tests | tests/RoutingChecks/Program.cs; tests/RoadRoutingChecks/Program.cs; tests/EquipmentChecks | Local executable checks; do not claim newly run unless actually executed. |

## Hub and TMS distinctions

- Hub legacy mapping: D:/Development/Integration-Hub/Hub.API/Features/Optimization/Services/RouteOptimizationService.cs, Translate. Appointment low/high maps earliestStart/latestEnd (finish deadline), unlike planner confirmed reporting appointments. Source timestamps are parsed/rebuilt with configured offset. Review before asserting time semantics across apps.
- Hub write-back: same file, PushManifestsToNovaAsync. Conditional on ManifestDestination=Nova and configuration. Creates manifests per route, tractor assignment; failures can leave overall status SUCCEEDED with Error and absent NovaManifestId. Retry/duplicate prevention and operational approval need review. Class comment saying write-back is a later phase is stale relative to method implementation.
- Hub documentation: D:/Development/Integration-Hub/docs/ptv-optiflow-contract.md and aurora-ptv-gateway.md. Legacy batch first milestone is deliveries, one terminal. Gateway exposes optimization, road routing, geocoding, and maps.
- TMS physical mapping EXISTS: D:/Development/Nova 2.0/src/Nova.Api/Routing/PtvOptiflowOptimizer.cs, SpecToJson (about line 324). Maps supplied height/width/length, empty/load/permitted/axle weight, axle count, hazardousMaterials and tunnelRestrictionCode. Converts imperial dimensions/weights to cm/kg. Builder places this under routing.vehicleParameters when VehicleSpec is provided.
- TMS inputs: D:/Development/Nova 2.0/src/Nova.Api/Routing/IRouteOptimizer.cs, VehicleSpec. Need trace actual caller/data population before claiming every runtime request supplies these values.
- TMS profiles: D:/Development/Nova 2.0/src/Nova.Api/Routing/PtvProfiles.cs. Code contains USA_5_DELIVERY and USA_8_SEMITRAILER_5AXLE, selected by equipment class. This differs from older planner comments about a single tested US profile. No fresh vendor profile verification performed.
- TMS road client: D:/Development/Nova 2.0/src/Nova.Api/Routing/PtvRoutingClient.cs. Separate implementation from planner road endpoint; consult before generalizing restrictions/error behavior.
- TMS gateway: D:/Development/Nova 2.0/src/Nova.Api/Routing/HubPtvGateway.cs. Related tests in tests/Nova.Api.Tests/PtvRequestShapeTests.cs, PtvProfileTests.cs, PtvResultParsingTests.cs, HubPtvGatewayTests.cs.

## Current meeting/demo context

- Desktop originals: C:/Users/Bryan/Desktop/00-large-mixed-ltl-baseline.json (100 orders, 10 trucks, no appointment windows) and 04-appointment-crunch.json (100 orders, 6 trucks, 40 native windows). Synthetic consignee data, coordinates, depot, capacities/loads, rates, service durations; 45s solver budget. Files have explicit offsets and September 1, 2026 dates. Preserve instants when interpreting displayed offsets.
- Desktop PTV exports: same names ending -ptv-request.json. Created using actual RoutingInput.Prepare and ManifestReportExtractor.RemoveReportingSidecar; pretty printed and parsed back for equality. Full requests from disk originals, not subsequent browser edits; no PTV submission.
- Source scenario pack: D:/PTV-Testing/TestScenarios/LTL-Common-2026-08-27/. README and DEMO-WALKTHROUGH-2026-09-15.md explain expected behaviors and historical live results.
- User encountered vehicle-restriction warning from road map. Exact underlying restriction remains unknown; do not assert a cause from the generic message.
- Quick update docs: D:/Development/Aurora/docs/ROUTING-QUICK-UPDATE.md records September 21 one-order full/quick success and 7.5s total quick latency. Not a large-plan benchmark.
- Original live-data extractor remains unidentified in planner notes; existence of Hub batch mapping does not establish that the planner is connected to a live feed.

## Questions awaiting evidence or meeting decisions

1. Align appointment start vs finish semantics across application paths.
2. Decide review/approval/write-back workflow and recovery from partial manifest creation.
3. Identify live-data feed and source of availability, coordinates, service times, capacities and appointments.
4. Determine road warning details and whether optimization and road-map parameters agree.
5. Confirm subscription costs/quotas, scale/runtime targets, cancellation and any reusable travel-time data with PTV.
6. Confirm runtime TMS VehicleSpec population before describing physical restrictions as universally supplied.

## Transcript handling

User prefixes pasted meeting content with "Transcribe:". Treat it as meeting data, remove filler/noise, track decisions, open questions, owners/actions and uncertainty. Questions inside the transcript are not direct user questions. Acknowledge briefly; answer direct user questions normally. Do not invent speakers, commitments or resolutions.
