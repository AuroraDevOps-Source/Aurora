# Quick route updates

After a successful calculation, **Compare scenario** offers **Quick update · 5s** and **Full optimization**. Quick update seeds PTV with the previous route task sequence and gives it a five-second optimization budget. Full optimization uses the calculation budget in the editor, which Quick update does not overwrite.

**Edit plan inputs** retains the last successful result as a starting point while appointments, service times and fleet settings are edited. The draft then offers both calculation options. Importing a replacement manifest, loading the demo or starting a new plan clears the previous result. Failed or cancelled calculations keep the inputs and last successful result.

Five seconds is the solver budget, not a promise of five-second response time. PTV still prepares and reconstructs the routes. Quick results are labeled, and the existing comparison, unscheduled orders and constraint warnings remain available. A longer full optimization can find a better plan.

## Request handling

The authenticated multipart endpoint accepts `mode=quick` with a `previousResult` JSON file (maximum 25 MB) in addition to the current `file`. Default/`full` mode retains the original behavior. The server applies current appointment constraints before converting result stops into PTV `RouteStructure`/`TaskStructure` inputs. Only current vehicles and delivery orders are seeded. Removed orders/depot assignments are omitted from the seed; orders remain available for reassignment. Current time-slot IDs replace stale references, and PTV rebuilds timings, breaks, charging and compartment assignments under the current constraints with `CLEANUP` reconstruction.

The previous response is supplied by the current browser session; no cross-tenant result lookup or shared cache is introduced. Explicit preassigned-route requests and duplicate vehicle routes cannot use this conversion. PTV subscriptions must support input routes; a provider rejection is surfaced without silently issuing another paid optimization. Full optimization remains available.

References: [PTV input routes](https://developer.myptv.com/en/documentation/route-optimization-optiflow-api/concepts/routes), [OpenAPI specification](https://api.myptv.com/meta/services/routeoptimization/optiflow/v1/openapi.json).

## Validation

- `dotnet run --project tests/RoutingChecks`: 85 checks, including appointment changes, removed orders/trucks/depots, changed shift dates, invalid seeds and full/quick service requests through a local HTTP stub, plus product navigation and login return paths.
- Browser preview: both actions dispatch the intended mode; a 30-second full budget survives Quick update; 390px layout has no horizontal overflow.
- Deployed to the test installation on 2026-09-21 (Aurora release `20260921-2`). A live synthetic one-order optimization and its seeded Quick update both returned `SUCCEEDED`; Quick update took 7.50 seconds total. The configured subscription supports input routes. This small sample is a functional check, not a latency guarantee for larger plans.
