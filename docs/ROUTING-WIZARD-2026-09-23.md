# Route planning wizard — September 23, 2026

The upload planner now guides dispatchers through four steps: orders and available trucks, appointment/service exceptions, monitored optimization, and maps/results. Existing fleet equipment is the default; detailed fleet editing and the equipment catalog remain secondary tools. Truck availability and shift changes apply to the current plan.

## Optimization loop

The client submits a session once and polls every five seconds, showing scheduled/unscheduled orders, truck count, cost, and recent progress. Dispatchers choose a maximum search duration (5–1200 seconds) or use **Stop and review best plan**. Stop uses PTV's POST `/optimizations/{id}/stop`, then waits for a terminal result; it does not delete the job. There is no automatic inactivity-based stop rule.

Adjust and rerun preserves the last successful result. The dispatcher can seed a new job with prior routes and optionally keep existing truck assignments using hard order/vehicle category constraints. Seed routes alone do not lock stop order. Assignment protection rejects removal of a protected truck. Changed inputs always create a new provider job.

Road geometry remains a separate Routing API operation. Restriction warnings identify the truck/profile and route-point range, retain the optimized plan, and leave bird’s-eye view available. This change does not establish or fix the cause of a provider road restriction.

## API and recovery

Authenticated `/api/v1/routing/sessions` endpoints: POST to start, GET `/{id}` to poll, POST `/{id}/stop` to stop. GET `/{id}?inputs=true` also returns original plan inputs for refresh recovery; routine polling omits those potentially large inputs. Individual input and previous-result strings are limited to 25 MB.

A browser-tab session ID reconnects after refresh. Encrypted server records retain provider IDs, inputs and final results, scoped to tenant and user. A record is saved before submission, and duplicate session IDs cannot issue duplicate provider jobs. An uncertain provider submission remains unresolved rather than being automatically repeated. Transient polling failures allow reconnecting to the existing job.

Storage defaults to `routing-sessions` beneath `Hosting:DataProtectionPath`, or `App_Data` when that setting is absent. Override with `Routing:SessionPath`. Persist both records and data-protection keys across deployment. Records expire logically after 48 hours; old files are cleaned on service startup. Unsubmitted drafts remain in client memory. Recovery is designed for the current single API host: multiple replicas require shared storage and distributed locking before use.

Provider contract checked against https://api.myptv.com/meta/services/routeoptimization/optiflow/v1/openapi.json. Current cost fields are `costs.grossTotal` and route `costs.total`, with legacy fallbacks.

## Validation

Release solution build passes without warnings. Executable checks cover request preparation, hard assignments, progress, stop errors, durable recovery, tenant/user isolation, encryption, input recovery and uncertain submissions. Existing routing, road and JavaScript map checks also pass.

The local `tests/PlannerPreview` `/wizard` harness uses simulated PTV responses and stubbed maps. Browser checks exercised fleet selection, appointment review, progress, refresh recovery, stopping, results, rerun navigation and the mobile layout. No live/paid PTV request was made during local validation. Real optimization progress and road geometry still require a live PTV run.

## Test-server deployment

Release `20260923-1` deployed on September 23, 2026 at https://user.aurorasoftware.com/routing after the Aurora VPN was connected. API and client were published together; archive SHA256 `3e1dacc6f603357522bd2ea9240853e942af4e4dd3e36c62aa948f3fae598cdd` matched before extraction. The existing update helper recreated only API/web containers, preserving databases and other product deployments. No migrations or seeding ran.

Public readiness, login, embedded-session protections, Aurora/FreightOps/Hub PKCE and API checks passed. The authenticated planning-session endpoint returned its expected missing-session response, verifying encrypted storage initialization. Server `Hosting__DataProtectionPath=/keys` uses the persistent volume. The public page returns HTTP 200 with the new wizard stylesheet version. No paid optimization was submitted during deployment verification; the dispatcher can now test live jobs.

Previous release `20260921-2` remains available. Application rollback: `python3 /opt/aurora-auth/releases/20260923-1/scripts/deploy-aurora-release.py 20260921-2 --embedded`. Preserve the keys/session volume and databases.
