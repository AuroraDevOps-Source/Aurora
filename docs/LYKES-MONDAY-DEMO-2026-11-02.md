# Lykes Monday demo — November 2, 2026, 8:00 AM Eastern

Verified live October 30, 2026, 12:47 UTC. This is a one-shipment pilot, not a source-wide production rollout.

## Walkthrough

1. Sign in to https://user.aurorasoftware.com and use **Lykes Test**.
2. Open Orders and search **1862**. It came from `D:\db\lykestest\operations.fdb` on `172.23.0.5:3050`.
3. Open https://lykes.freightopsconnect.com and find PRO **1862**, terminal **TPA**. Saved dashboard credentials were verified through its normal authentication API.
4. Show one freight line, one piece and one barcode, 285 lb, 40.88 cubic feet per piece. Line identity `3bfe841d-1896-9725-7fe3-ffd36ef8dcbf`; barcode `1793363594-a4e885e0` remained stable during the pilot.
5. The FreightOps shipper was changed through its native application API to **BLUESKY MAST INC - SYNC DEMO**. Aurora imported that change; Nova retained **BLUESKY MAST INC**.
6. Open the saved Monday route: https://user.aurorasoftware.com/aurora/manifests/c9e9c0fd-864c-46b5-a317-992e200e9a2b . Manifest **MAN-02221E40**, truck **DEMO-TPA-01**, **Demo Driver**. View its route map and stops.

Optimization scheduled the shipment with no unassigned orders or constraint violations. Estimated distance 81.81 km; estimated cost 146.26 using synthetic demo rates. Departure Monday 08:02:09, delivery 09:00–09:05, return 10:02:41, all Eastern standard time (-05:00). The shipment's local appointment is Monday 09:00–17:00.

The route is stored as an Aurora manifest, so its result survives the optimization session's 48-hour retention. It is **not dispatched**, and no native FreightOps or Nova manifest was created. Don't present this as native dispatch integration.

## Enabled scope and safeguards

- Nova → Aurora → FreightOps; FreightOps → Aurora confirmed live.
- Nova reverse business writes remain disabled. The worker skips every Nova push, and the mapper rejects business writes for all entity kinds in the enabled read-only Lykes profile.
- Native pilot allowlist contains only PB_ID **1862**. Other orders/manifests and unrelated native fleet are excluded; terminal/customer reference data is imported. Expanding this list requires preparing and validating the additional freight.
- Aurora local workspace mode remains on, so legacy UI endpoints cannot bypass the local workspace to write directly to Nova.
- At final verification FreightOps had zero pending, working, held or field-conflict records. Nova had eight pending outbound records that are intentionally not processed while read-only. Do not enable Nova writes to clear that queue.
- The scheduled worker runs every 30 seconds. Change-capture tables, acknowledgement records and events are integration bookkeeping in Nova; these are separate from business writes.
- No Delphi, rating implementation or Cooke deployment changes were made.

## Authorized test-data preparation

Before activating sync, PRO 1862's freight line was given SKU `AURORA-TEST-1862-1` and UOM `PIECE`, set Ready to Route (native status 110), and given an October 30 09:00–17:00 test appointment. Its rating amounts stayed 45.91, freight stayed one piece / 285 lb / 40.88 ft³. Header/stop capture and line capture were installed; no stable-ID column or Delphi patch was required.

All 1,784 original Nova lines had blank SKUs and packaging units. The pilot uses explicit test data; it does **not** implement a general synthesized-SKU fallback. Missing or ambiguous freight still fails validation.

Aurora's later geocoding, Monday appointment, demo driver/truck and route changes remained local or synchronized to FreightOps. A final read-only native audit verified zero reverse-write receipts, unchanged 45.91 rating, original shipper and the original October 30 appointment.

The original TPA address did not meet the geocoder's minimum match quality. Both demo applications clearly label the test depot; it uses the shipment's successfully geocoded pickup address, **2080 Wild Acres Rd, Largo FL 33771**. This is demo data, not a verified real TPA terminal location. Delivery coordinates were geocoded from **7710 N 30th Street, Tampa FL 33610**. Nova's terminal address was not changed.

## Published software and configuration

- Aurora API: `aurora-auth-api:20261030-5`, image `sha256:3dac30a8d58adfaf6eb09c9a18cbde8f449e16e7b6d692cabf75aff4c8cc2827`.
- Aurora web: `aurora-auth-web:20261030-1`, unchanged during sync rollout; includes 2×2 Connections layout.
- Lykes API on `172.23.0.11`: `freightops-api-lykes:aurora-planning-20261030-v2`, image `sha256:3a198e5fff9df1d73759ede27b77ca3dc784f4828137e7a34042998ca1d4a56d`.
- Lykes API base is the tested sync-enabled FreightOps commit `2b90aa0bd592f5db3d0a8c908111ed7d50865198`. A small core DLL layer adds support for Aurora planning metadata and equivalent null/empty optional text. It does not set FreightOps execution state from Aurora routing state.
- Only Lykes `freightops-api` was recreated; only Lykes's local proxy was reloaded. Deployment checks verified other project container identities, including Cooke, were unchanged. Dashboard/daemon were not upgraded.
- Lykes config: `/srv/freightops/lykes/compose.json`, `application.env`. Machine-sync token hash configured there; bearer credential stored privately on Aurora. No secrets in this document.
- Aurora container resolves `lykes.freightopsconnect.com` to private TLS proxy `172.23.0.12` using a scoped `extra_hosts` entry. Public-IP hairpin access from the Aurora server timed out; TLS hostname/certificate validation remains enabled.
- `Synchronization:Enabled=true`, FreightOps runtime enabled, both connection rows enabled. Lykes `NativeReadOnly=true`, `WritesEnabled=false`, `OrderLifecycleEnabled=false`, `PilotOrderIds=[1862]`.
- Mapping: terminal crosswalk AUS/DFW/IAH/AMA/LBB/SAT/MIA/FLL/MCO/JAX/TPA; keyword `1` freight, `MIN` and `FSC` charges; `PIECE` → `Piece`; pickup freight rows; scalar cube ft³ per piece. These are sufficient for the pilot, not all historical records.

## Fixes and validation

- Read-only guard now covers masters as well as orders/manifests; background Nova push is skipped.
- Pilot filter keeps unrelated invalid native fleet/orders from blocking the selected shipment.
- Aurora order-editor saves preserve integration-only line metadata. Customer code reads retain the stored code when there is no linked local customer.
- FreightOps preserves Aurora geocoding/scheduling metadata and Ready/Routed planning labels without manufacturing native execution states. Optional blank text is equivalent whether represented as null or empty.
- 282 local Nova capture/mapping checks passed.
- 134 real local FreightOps endpoint/database/production-mapping checks passed, including editor metadata preservation and geocoding/scheduling round trips.
- API Release publish and FreightOps core build passed. Existing dependency audit warnings remain; this work did not upgrade those dependencies.
- Live authenticated APIs verified the saved route and native FreightOps edit returning to Aurora. Runtime flags and native read-only audit passed.

Evidence: `artifacts/lykes-readonly-guard-tests.log`, `artifacts/lykes-planning-sync-tests.log`, `artifacts/sync-completion/endpoints.log`, API publish logs. Server result: `/opt/aurora-auth/releases/lykes-monday-final-verification.json`. Saved draft/session/result/route JSON files in the same releases directory support the walkthrough; credentials are never included in those result files.

The FreightOps core source change and Aurora changes remain in the working trees. A future FreightOps publish must include the core change; publishing the old unpatched image would restore the rejection of planning metadata.

## Fresh reverse-direction recheck

On October 30 at 13:01 UTC, changed PRO 1862 through the authenticated Lykes FreightOps application API to shipper name BLUESKY MAST INC - REVERSE CHECK. Aurora received the new value, and FreightOps settled with zero pending/working/held/conflicts. Native read-only audit again confirmed original Nova shipper, rating 45.91, original appointment, and zero reverse-write receipts. Nova outbound queue intentionally increased while business writes stayed disabled. Restored the documented SYNC DEMO label through FreightOps for Monday's walkthrough.

Aurora API/web updated to 20261030-6 to restore the application navigation bar. FreightOps opens the URL configured in Connections; future saves keep the launcher destination aligned. Live API URL/access checks and 17 local settings checks passed. Browser loaded the new application navigation container but required sign-in after its existing session expired; visual verification after sign-in remains outstanding.

## Integration Hub SSO repair (October 30)
The production Hub API on 172.23.0.14 (IIS site IHubAppSite, pool IHubApp) had a blank Aurora Authority while the browser had SSO enabled. Set site appsettings.json Aurora.Authority to https://user.aurorasoftware.com, retained hub-api audience and HTTPS metadata validation, and recycled only IHubApp. Preserved the original settings as appsettings.json.pre-lykes-sso-20261030. Existing signed-in Aurora tenant 11111111-1111-1111-1111-111111111111 was linked to the stale Hub demo client 9. Transactionally moved that link to existing active Lykes client 3; the existing user link was retained. No other client or Cooke mappings changed, and no Nova settings changed. Fresh PKCE token previously failed with invalid issuer, now returns HTTP 200 for /api/dashboard/summary. Browser Retry successfully loads the Lykes Nova TMS to Uline dashboard. Hub's current client renders the sidebar tenant label from token claims, so the Lykes label is still absent for Aurora tokens; server-side client resolution and dashboard filtering use the correct Lykes client.

## Hub tenant label and Nova browser preparation
Hub API now exposes authenticated /api/auth/tenant using only validated server-side ClientId/ClientName claims. Sidebar uses this for Aurora sessions and retains the existing ClientName display for native Hub sign-in. Published API DLL and complete verified client assets to IHubAppSite; preserved site configuration, mappings and existing dependencies. Build/PublishGuard passed (126 compressed siblings, 60 boot entries), 26 existing authentication tests passed, and production SSO endpoint returns client 3 / Lykes. New browser tab required sign-in, so visual verification remains pending.
GoGlobal is on AG01 / 172.23.0.5:491 with NOVA published. Its active GoGlobal.crt certificate expired September 25, 2026. Existing Cert.pem/Key.pem are a verified matching Let's Encrypt pair for ag1.aurorasoftware.com, expiring December 27, 2026. Four Menu5 processes are present. No certificate or GoGlobal service changes have been made; approval is pending for possible user interruption. Nova 2.0 launcher removal and new /workspace/nova route are implemented locally; no automatic GoGlobal SSO is claimed.

## NV1 host and certificate resolved
Public nv1.aurorasoftware.com:491 is confirmed as NOVA01 / 172.23.0.2, not AG01 or NOVA02. Its existing replacement certificate covers nv1/nv2/nv3, matches its private key and validates to trusted roots through December 27, 2026. A protected server.crt/server.key pair is staged under C:\ProgramData\GraphOn\GO-Global\ks\nv1-maintenance-20261030 on NOVA01. Active certificate/key/configuration hashes and GGAPS PID 3496 remained unchanged; no service restart or user logoff occurred. See NV1-MAINTENANCE-2026-10-30.md for the updated activation and rollback sequence. Earlier unknown-host and missing-certificate notes are superseded.

## FreightOps manifest publishing — October 30

The Monday route is now a native Lykes FreightOps delivery manifest: **MAN-02221E40**, TPA, November 2, truck **DEMO-TPA-01**, driver reference **Driver: Demo Driver** (FreightOps Tag), PRO **1862**, one piece / 285 lb / 40.88 ft³, delivery stop 1. Both native PRO membership and the manifest piece queue were committed in one transaction. Status remains Planning; no loading, delivery, departure or Nova business writes were manufactured. Aurora received the durable success receipt; submitting twice produced one target receipt/manifest.

### MVP behavior and boundaries

- New **Publish to FreightOps** and **Refresh delivery status** controls on Aurora's manifest screen. Explicit per-tenant feature switches default off on both sides; enabled only for Lykes.
- Delivery plans only, outbound, 1–200 mapped enrolled shipments from one mapped origin terminal. Shipments must already be synchronized without unresolved changes. Native PRO identity/revision comes from Aurora's connection-specific mapping, not guessed from display identifiers.
- Durable request and idempotent target receipt protect uncertain retries. Existing native manifest numbers are never silently adopted; stale revisions, already assigned/processed freight, native execution changes and incompatible stop/address grouping fail for review.
- The first MVP is a handoff: after submission Aurora locks plan edits, local deletion, membership changes and reoptimization. Manage assignment/execution in FreightOps. Native manifest lifecycle changes are not yet synchronized back into Aurora; do not describe this as complete bidirectional manifest synchronization. Shipment sync remains enabled.
- Driver is a descriptive native Tag reference, not a newly provisioned FreightOps driver account. Truck maps to SystemUnit; native date, origin terminal, delivery stops, notes and complete planning JSON are retained.
- Additive AuroraRoutePlanned event supplies planning date/metadata without changing native execution state. Lykes API and daemon share its new core assembly. Older machine events missing a user header are now handled by native status-history subscriptions; new sync events record the actor as Aurora sync. Existing human actor values remain unchanged.

### Verification and deployment

- **158** local end-to-end endpoint/database checks, **249** routing checks, **49** adapter HTTP checks passed; API/client Release publishing and FreightOps core Release build passed. The tests exercise the production mapper, outbox, request store, real endpoint and native database, plus status subscribers reading historical events without user headers. Existing dependency audit warnings were not changed.
- Aurora API/web release **20261030-9** (following initial feature release 20261030-8).
- Lykes API/daemon images **freightops-api-lykes:aurora-manifests-20261030-v2** and **freightops-daemon-lykes:aurora-manifests-20261030-v2**. Only those Lykes containers recreated and the Lykes local proxy reloaded; other project container identities, including Cooke, verified unchanged. No GoGlobal service/certificate changes.
- Native verification: one manifest receipt, both PRO CurrentManifest and CurrentOutboundManifest point to MAN-02221E40, one piece queued, Planning status. Native status-history subscribers caught up to HighWaterMark 23; API and daemon have zero restarts and no recent error markers after compatibility fix.
- Aurora FreightOps queue has zero pending, working, held or field conflicts; Nova writes/lifecycle disabled and NativeReadOnly=true; ten Nova outbound entries remain intentionally unprocessed.
- Evidence: artifacts/manifest-publish-test-summary.log, artifacts/manifest-publish-routing.log, artifacts/manifest-publish-adapter.log; production /opt/aurora-auth/releases/lykes-manifest-publish-verification.json. Lykes rollback files under /srv/freightops/lykes/aurora-manifests-20261030-v2. Do not roll back an event reader to a build that cannot read AuroraRoutePlanned events.

## November 2 expanded demo — current walkthrough

- Added nullable `OPS_STOP_REC.OPS_ETA_TIME TIMESTAMP` to the verified database `172.23.0.5:D:\db\lykestest\operations.fdb`. Existing rows remain null; no ETA population, appointment overwrite, or rating logic change. Mike's guidance is to add storage first, then define final-mile planned delivery population respecting appointment constraints.
- Created 12 clearly labeled synthetic Nova test shipments: PRO **99002001–99002012**, native PB_ID **1874–1885**. Cloned test template 1862 without changing it; copied freight/rating amounts, explicit demo markers, synthetic consignee coordinates/addresses. These are NOT real delivery locations. Pilot now includes these 12 and original 1862; not a source-wide rollout.
- Nova imported all new shipments into Aurora and FreightOps. A setup-only change to informational country fields was rejected as designed; restored the original country values and retried current revisions. No FreightOps mapping/security bypass was made.
- Four demo drivers and trucks, TPA depot, DEMO-BOX equipment, working hours, rates, capacity and appointment windows are configured. TPA depot retains its clearly labeled demo address.
- Eight-order optimization: **8 scheduled, 0 unscheduled, 0 violations, 2 routes**, 152.257 km. Both saved and published to native FreightOps; durable receipts and native manifest/PRO membership verified:
  - **MAN-119444A3**, Aurora `4fbcf449-a835-4f81-8ffd-63188477fd57`, DEMO-TPA-02 / Demo Driver Two, five shipments.
  - **MAN-F013760C**, Aurora `d9407f49-46f9-4f63-a9a1-b43baf198b4b`, DEMO-TPA-03 / Demo Driver Three, three shipments.
- For a live optimization demo, filter **TPA / November 2 / Ready to Route**, select **99002009–99002012**, use **DEMO-TPA-04 / Demo Live Demo Driver**, depart 08:00, return by 18:00. Independently rehearsed: **4 scheduled, 0 unscheduled, 0 violations**, one route, 96.446 km. The rehearsal draft was cancelled afterward so the four orders remain available for a fresh demonstration.
- Found and fixed automatic inclusion of previously submitted FreightOps manifests in new drafts. Existing commit guards were already present; the new filter prevents them entering the optimizer at all. API **20261102-1** published without reset; web unchanged. Live four-order draft verified only the selected four orders and only the unbooked truck. 249 routing checks and API Release build passed. 285 embedded Firebird sync/mapping checks passed including nullable date/time storage.
- FreightOps native API reverse test changes PRO 99002009 shipper to `DEMO - FREIGHTOPS REVERSE VERIFIED`; final verification checks Aurora receives it and FreightOps has no outstanding holds/conflicts.
- **Full Nova business writes are NOT enabled**. User requested expansion, but instructed Steve to own Delphi changes. Stable native line identity preservation and explicit existing-mapping reconciliation remain prerequisites; see `LYKES-STEVE-DELPHI-HANDOFF-2026-11-02.md`. Do not claim a full two-way Nova write demo. No Delphi changes applied; no Cooke changes.
