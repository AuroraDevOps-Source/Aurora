# Aurora / FreightOps test deployment — 2026-09-13

## Live installation

- Aurora: https://user.aurorasoftware.com/ — new isolated Docker installation on test host `172.23.0.13`, SSH port `61223`.
- FreightOps: https://freightops.aurorasoftware.net/ — universal login linked to existing `BBATTS` account.
- Hub target: https://integration.novafreightops.com/ — separately managed IIS site; not the retired test Hub container.
- Existing DNS was left unchanged. Caddy now routes the Aurora hostname to its API and web containers.
- Aurora release: `20260914-5`, `/opt/aurora-auth/releases/20260914-5` (username sign-in, retaining the integrated routing workspace and login autofill).
- Aurora database, signing/encryption certificates, and data-protection keys persist outside the containers. Private settings live under `/opt/aurora-auth/private` (root-only).
- The PTV key was reused directly from the existing test Route Desk configuration, without putting it in source or browser configuration.
- Login details are in `deploy/private/staging-login.txt`, restricted to Bryan and SYSTEM and excluded from source control. The staging account has the same Aurora subject UUID as the requested FreightOps link.

## Verified

- Public HTTPS, discovery, signing keys, Aurora cookie login, PKCE code exchange, standard user-info endpoint, and licensed-product list.
- Browser login reaches Route Optimization and shows Integration Hub / FreightOps / Route Optimization in the header.
- FreightOps profile endpoint returns `BBatts`; the browser opens its populated dashboard without another password prompt.
- Real PTV synthetic test: one vehicle, one stop; SUCCEEDED, one scheduled order, zero unscheduled orders, zero violations. No real customer manifest was submitted or written back to Nova.
- Hub authenticated dashboard API returns JSON successfully. After Bryan republished and restarted IIS, the browser test at 19:14 ET passed: clicking Integration Hub in Aurora traversed `/sso` and opened the signed-in Integration Hub Dashboard without another password prompt. The earlier missing `/sso` frontend issue is resolved.

The shared header now stays visible over all three sections. FreightOps and Integration Hub run inside workspace iframes; Route Optimization remains a native Aurora page. Browser verification at 19:27 ET confirmed both signed-in dashboards inside their frames, with the address bar remaining on `user.aurorasoftware.com/workspace/...`, and switching back to Route Optimization worked.

The earlier full-page fallback has been removed. Its actual blocker was the issuer's SameSite=Lax cookie being withheld during the cross-site iframe authorization redirect. Staging now explicitly sets `Auth__AllowEmbeddedLogin=true`, issuing Secure, HttpOnly, SameSite=None cookies. The parent workspace calls `/api/auth/prepare-embedded-session` to reissue an existing authenticated session with these attributes while retaining its identity and original expiration. This avoids requiring existing users to sign out and back in. See [Microsoft's SameSite guidance for iframes](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0).

Compensating protections: Aurora's web and API responses restrict framing with `Content-Security-Policy: frame-ancestors 'self'`; session-changing `/api/auth` requests reject untrusted browser Origins. Automated checks verified trusted session preparation returns 200 with all three cookie attributes, anonymous preparation returns 401, and untrusted-Origin login/logout/session preparation each return 403. Product token signature, tenant, role and account-link validation remain unchanged.

## FreightOps release

- Backend and daemon: `ghcr.io/auroradevops-source/freightops-{api,daemon}:sha-066cad581e6a272a4972cd5b2ef3c914ce9f04fb`.
- Dashboard: `ghcr.io/auroradevops-source/freightops-nginx:sha-38391963b792e4f282962e394794dd3284c64d0a`.
- Both GitHub image workflows completed successfully before these images were deployed.
- Applied `AuroraUserLink.sql` in `fo_auth`; linked `BBATTS` to `01a08138-7cb9-7a10-8bd8-450a7bfd755e`.
- API configuration: `Aurora__Authority=https://user.aurorasoftware.com`, `Aurora__TenantId=11111111-1111-1111-1111-111111111111`.
- Dashboard configuration: `VITE_API_HOST=https://freightops.aurorasoftware.net`, `VITE_AURORA_AUTHORITY=https://user.aurorasoftware.com`, `VITE_AURORA_CLIENT_ID=freightops-spa`.
- The server base compose file now forwards both Aurora dashboard variables. Its mounted nginx configuration prevents caching `/config.js`; the client also uses a versioned config URL.
- The real profile route is `/api/Authenticate/me`, not `/api/Users/me`.
- Discovery and token packages must have matching versions. The isolated `scripts/oidc-probe` reproduced zero keys with Protocols.OpenIdConnect 7.1.2 + Tokens 8.0.2; aligning both to 8.0.2 restored key discovery. See the [IdentityModel issue](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/2510).

## Aurora source and repeat deployment

`D:\Development\Aurora` was not a Git repository. Source fixes remain in this directory: production API origin, persistent non-development certificates/keys, missing authentication JavaScript, OIDC user-info endpoint, post-login product-header loading, labels/order, and authenticated iframe session preparation with framing/origin protections.

Archive: `artifacts/aurora-release-20260914-5.tar.gz`.
SHA-256: `3B67E72C2E16CB63127A68DBA8EE3352CCD1D26AF875F91FB01449BE235DB77E`.

The archive contains only API/client/migration binaries and deployment templates, not private server settings. Upload and extract to the matching release directory, then run `scripts/install-staging.py` on the test host. This installer is staging-specific: it preserves existing generated credentials and keys and re-seeds the demo tenant's product URLs. Do not reuse its demo seeding for a production tenant.

For an existing installation, use `python3 /opt/aurora-auth/deploy-aurora-release.py 20260914-5 --embedded` after uploading/extracting the release. This builds and updates only the API/web containers, backs up their configuration, and verifies public readiness. It does not migrate, seed or touch databases. These Aurora updates required no FreightOps or Hub code changes and no additional IIS publish.

`scripts/staging-remote.py` uses the credentials in the supplied operations document and strict SSH host-key verification. `scripts/verify-sso.py` runs on staging and reports results without printing passwords or bearer tokens. Its optional `--ptv` flag makes one synthetic PTV optimization call; `--embedded` checks session preparation, cookie attributes, and rejection of untrusted session-changing requests.

## Aurora / Hub visual alignment

Release `20260913-7` adds an explicit light MudBlazor theme and matches Hub's `#2d3748` navigation, `#f0f2f5` page background, Segoe UI typography, white rounded cards and blue active accents. It updates the sign-in card and routing upload/results/variation styles while keeping the horizontal three-product navigation and embedded workspaces.

The shared header is horizontally scrollable on small screens without widening the page; its redundant spacer was removed after the 390px browser check. Routing report layout now correctly stacks at the existing 1100px breakpoint. Map positioning and routing behavior are preserved.

Both CSS links have a `20260913-hub-theme` version query. Browser verification caught a cached old stylesheet despite the new client assembly; the explicit version ensures the updated styles load through Cloudflare's four-hour asset cache. Increment this version with future CSS releases unless content-fingerprinted filenames replace it.

Verification: Release publish succeeded; final public readiness passed. Automated login, all three PKCE/API flows, existing user links and iframe session/origin protections passed after the theme deployment. Desktop browser checks confirmed the new palette and typography, the sign-in card, and signed-in Hub/FreightOps dashboards beneath the persistent header. A 390px routing check confirmed no page-level horizontal overflow. This theme-only update reuses the verified API binary from release `20260913-4`; no database migrations, seeding, product source edits or Hub/IIS publish were performed. No additional paid PTV optimization was needed for the styling update.

For a theme rollback that retains the verified iframe fix, use release `20260913-4` with `--embedded`. Releases 5 and 6 were intermediate theme/cache/responsive checks; use release 7 for the final theme.

## Routing update — 2026-09-14

Release `20260914-2` adds per-manifest map filtering with persistent red unscheduled markers, individual route costs/miles/durations, appointment import/templates and confirmed-window constraints, schedule details with explicit source offsets, pre-run fleet settings, synthetic test data and expanded exports. See `docs/ROUTING-2026-09-14.md` for the input contract, verification and remaining scope.

The API and client were both rebuilt and deployed using the existing update helper with `--embedded`. Archive SHA-256 was verified on staging before extraction. Public readiness and automated login, all three PKCE/API flows and iframe/origin protections passed. The live PTV two-truck test scheduled the two simultaneous appointments and left the before-shift order unscheduled. The one-truck variation scheduled one order, left two off and reported zero violations. Browser export checks verified appointment fields and source-offset schedules; local checks passed (36 routing and 12 map checks).

The update changed only Aurora API/web containers. Database volumes, secrets, user links, DNS, FreightOps and Hub/IIS deployments were unchanged. Routing CSS and map JavaScript use `20260914-routing` cache versions; the shared Hub theme remains `20260913-hub-theme`. For a pre-Routing-update rollback preserving theme/iframes, deploy `20260913-7 --embedded`; do not run the staging installer or re-seed the tenant for rollback.

## Login autofill update — 2026-09-14

Release `20260914-3` updates only the client, reusing the API/migration binaries from `20260914-2`. The login has a native form, stable labeled username/password fields, `autocomplete="username"` / `autocomplete="current-password"`, and a submit button. A small form-reading helper submits the current DOM values, including browser autofill that did not trigger typing events. The helper does not persist or log credentials. The form continues to wait for an explicit submit; no automatic login or Remember Me option was added, per Bryan's demo requirement.

The existing nonpersistent eight-hour cookie, credentials validation, lockout, SSO and iframe/origin protections are unchanged. Public readiness, login cookie attributes and all three PKCE/API checks passed after deployment. `node tests/login-checks.mjs` passed 13 focused checks; the map checks also passed. The browser showed existing saved credentials filled in with the login page still visible; manual submission was tested. Saving a password remains a choice in the user's own browser profile, not something the site can force.

The app CSS and new login helper use the `20260914-login-autofill` cache version; routing CSS/map versions remain unchanged. Use `20260914-2 --embedded` for a rollback of just this autofill release. No Git operations, database changes, secret changes or paid PTV calls were performed.

Reference: [Chromium password-form guidance](https://www.chromium.org/developers/design-documents/form-styles-that-chromium-understands/).

## Integrated routing workspace — 2026-09-14

Release `20260914-4` replaces the isolated upload screen with a Hub-styled planning workspace: delivery import, fleet counts/settings, optional appointment tools, an actual-location map preview and imported-order preview. A single primary Optimize routes action starts calculation. Sample data and raw JSON downloads are secondary disclosures. The results retain existing manifests, comparison, exports and map filtering, with clearer New plan / Compare scenario actions.

The preview draws only coordinates present in the input, labels itself as unoptimized and flags orders without valid map coordinates. Invalid replacement parsing does not discard the existing draft. New plan disposes the results map and resets to an empty preview. This is still file-based planning, not saved dispatch or live shipment integration.

Client Release build/publish succeeded with no build warnings. Local checks passed: 43 routing/workspace, 20 map and 13 login checks. The archive SHA-256 matched on staging before extraction. Public readiness, all three authenticated PKCE/API flows, nonpersistent login cookies and iframe/origin protections passed after deployment.

Browser verification covered empty and loaded previews (three delivery markers plus one depot, no route lines), applying fleet settings without a job, the primary optimization action, scenario editor/cancel, map filtering with the red left-off marker retained, and a clean New plan reset. One real PTV run using only the three fictional demo deliveries returned SUCCEEDED, two routes, two scheduled orders and one impossible before-shift appointment left off. Desktop and 390px layouts had no page-level horizontal overflow; the temporary viewport override was reset afterward.

Routing CSS and map JavaScript now use `20260914-workspace` cache versions. This client-only update reuses release 3's API/migration/deployment binaries. No Git operations, database migrations, seeding, secret changes, live shipment writes or Hub/IIS publish were performed. Roll back only this workspace redesign with `20260914-3 --embedded`; do not run the staging installer for rollback.

## Requested demo login change — 2026-09-14

At Bryan's explicit request, the existing linked staging account was renamed to `bbatts` and given the requested temporary six-digit demo password. The password is recorded only in the protected staging login/bootstrap files, not source. This is a weak demo-only credential and must be replaced before real use.

Release `20260914-5` changes the login field from email-only HTML validation to a text username field, with matching labels/errors. The stable field IDs/names, password-manager autocomplete, explicit submit, nonpersistent session and API password policy are unchanged. The API/deploy/migration binaries are reused from release 4; the archive hash matched on staging before extraction.

The one-account `scripts/staging-account` maintenance utility used real Identity manager APIs in a serializable transaction, with a password-policy exception confined to the offline staging tool. It checked the exact subject, email and tenant, rejected username collisions and verified unchanged role/membership sets. The existing subject `01a08138-7cb9-7a10-8bd8-450a7bfd755e`, original email, one membership and three role links remain intact. Security/concurrency stamps were updated by Identity, and failed-login state was reset. No other accounts or product databases were edited.

Protected backups: `/opt/aurora-auth/private/account-pre-demo-login-20260914-165720.json` and timestamped `bootstrap.json.pre-demo-login-*` / `migrations.env.pre-demo-login-*`. Bootstrap credentials and the local protected login file were synchronized. Bootstrap now retains `admin_email` separately from `admin_username`; future explicit seed configuration still locates this same account by its original email. Do not run the staging installer for an ordinary update or credential rollback.

Verification: 16 focused login checks passed. Public login with the new credentials, all three PKCE/API flows, FreightOps account linkage and iframe/origin protections passed. Client release publish succeeded. No Git operations, seeding, paid PTV calls or Hub/IIS deployment were performed. Rolling back to release 4 would restore email-only browser validation and prevent entering `bbatts`; use release 5 or a newer username-capable client with this account name.

## Hub IIS handoff

Completed by Bryan; public browser and authenticated API verification passed on 2026-09-13 at 19:14 ET. The steps below are retained for future publishes.

Source commit: `bf435ff1932ecbc090249d350e41cc89c74f1f57` on `Integration-Hub/master` (pushed).
Package directory: `artifacts/hub-iis`; zip: `artifacts/Integration-Hub-IIS-bf435ff.zip`.

1. Back up the existing IIS application files and preserve its environment-specific `web.config`, appsettings, secrets, uploads, and logs. Record the exact app pool and directory first.
2. Stop only the Hub application pool, copy the publish output (including the entire `wwwroot`), preserve/merge existing server-specific configuration, then start that pool. Do not reset all IIS sites.
3. Confirm the effective API setting `Aurora:Authority` is `https://user.aurorasoftware.com`; keep other `HUB_SECRETS` values unchanged. Client `wwwroot/appsettings.json` already has the public authority and `hub-spa` client ID.
4. Existing Hub database migration `0023_aurora_sso_links.sql` and user/client links must be present. The live authenticated API already passed; do not blindly reassign links with `DB Scripts/provision-aurora-sso-links.sql`.
5. Verify `/sso` reaches the signed-in Hub dashboard through Aurora. If the old UI persists, verify complete `wwwroot` copying and cache revalidation; do not mix `.wasm` files, compressed siblings, or boot manifests from different releases.

The publish was built using the installed .NET 10 SDK targeting .NET 8. The repository's publish guard was invoked separately because its nested SDK resolution requests unavailable SDK 8.0.423: all 126 compressed assets and 60 boot-manifest entries passed.

## Backups and rollback

- Caddy backup: `/opt/integration-hub/Caddyfile.pre-aurora-20260913-183200`.
- FreightOps config backups: `.env.pre-universal-login-20260913-180428` and timestamped `.pre-sso-*` compose/nginx backups in `/opt/freightops/FreightOps`.
- Pre-task FreightOps backend/daemon tag: `sha-de1b80a740f65ccd319d992ee947b70ffd2284f7`; dashboard: `sha-6ec22582a96a4aa7640852564ccee30548a4617b`.
- To roll back application code, select the recorded immutable images and recreate only affected services. Do not remove database volumes or restore an old database as part of an application rollback. The nullable Aurora link column can remain.
- Aurora has no pre-task installation. Release `20260913-2` is the verified full-page-navigation rollback if the embedding update must be reverted. Release `20260913-3` was an intermediate build and is not a rollback target. Preserve the database/private keys when changing API/web releases; `.env.pre-*` and `private/api.env.pre-*` backups preserve the exact previous configuration.

## Existing dependency warnings

## Equipment planner update — 2026-09-15

Aurora release `20260915-1` is deployed and healthy at `https://user.aurorasoftware.com/routing`. Migration 0010 added tenant-scoped equipment tables with seeding disabled. A pre-migration database backup and previous release `20260914-5` are retained. Equipment persistence, two synthetic PTV runs, deployed browser edits, and Aurora/FreightOps/Hub SSO checks passed. See `docs/ROUTING-EQUIPMENT-2026-09-15.md` for archive hash, backup name, and exact results.

### Previously recorded dependency warnings

## Login version update — 2026-09-15

Release `20260915-2` adds the release version below the login button. Build the client with `-p:ReleaseVersion=<release-id>`; builds without that property display `Development`. The browser verified `Version 20260915-2` on the deployed login page. Release build, 16 login checks, public readiness, and all three product SSO/API checks passed. No database migration was needed. Previous release `20260915-1` remains available for rollback.

Archive SHA256: `a3298485e9e4633cd2b97ef60691be3666769c7dbc29b0c5c08155d112fc1f7e`.

FreightOps build reports a critical advisory for existing Marten 8.34.2 (GHSA-vmw2-qwm8-x84c). Hub reports a moderate MailKit 4.3.0 advisory. These were not introduced or upgraded by this deployment and need separate remediation.

## Aurora update — 2026-10-12, 09:13 ET

Published release `20261012-1` to https://user.aurorasoftware.com (existing `/opt/aurora-auth` installation). Login displays **Version 1.0.0**. Includes the current Nova-backed workspace, user administration, menu icons, and blue swirl favicon.

- Base Git commit: `4b6013ca5b826b27bf3cf995736928c24a7e2849`, plus local working-tree changes at build time. The release archive is the authoritative snapshot; concurrent later workspace edits are not guaranteed included.
- Archive: `artifacts/aurora-release-20261012-1.tar.gz`; SHA256 `0a2b670bffbd487d3b5d8c9ebbb29f80e9c8a53b75fa4d7a97bbf178288c26a0` (verified after upload).
- Images: `aurora-auth-api:20261012-1` (`sha256:4dc17583751e9d4b5ff75f1eedd8223a930770a94a5ae30a64d8b4f5369e8efe`) and `aurora-auth-web:20261012-1` (`sha256:90103460b0fc6a5a19c5e7ee2362c1b5f54206287c7d270139b8487c9a6a8ae0`).
- Previous release: `20260924-12`. Rollback image tags: `aurora-auth-api:rollback-20261012-1`, `aurora-auth-web:rollback-20261012-1`. Restore the backed-up `.env` and recreate only API/web to roll back binaries.
- Protected backup: `/opt/aurora-auth/backups/20261012-1` (database dump, configuration, previous image IDs). Dump catalog validated. Migrations 0014–0020 rehearsed on a restored disposable database, then applied with `Seed__DevData=false`. Rehearsal database removed afterward.
- Used release-local `deploy/deploy.py`; did not run the older reset-staging-orders deployment helper. Existing data, credentials, keys, and other applications preserved.
- Verification passed: readiness, existing administrator login, PKCE exchange, administrator authorization, Users listing/editability, Nova configuration status, favicon, and browser-rendered Version 1.0.0 with no console errors. Routing (120), client authentication/session, API error handling, and fleet mapping checks passed locally. Live Nova record checks were not run because the required test configuration was absent.
- The staging administrator has no Nova connection configured. Configure it through Users; local database connection settings were not copied. Nova-backed screens require that setup.
- Migration runner emitted a missing optional Kerberos library message; both rehearsed and live migrations nevertheless completed successfully with PostgreSQL password authentication.

## Database catalog and password policy — 2026-10-12, 09:37 ET

Published release `20261012-2` to the existing Aurora test installation, retaining display version 1.0.0.

- Added Admin > Databases with encrypted credentials, connection testing, revision checks, and deletion protection while assigned. Users now select a database; old per-user connection request fields were removed.
- Applied migration 0021 after backup and rehearsal on a disposable restored database. Existing saved connections were copied into the catalog and linked to users, then the old JSON column was removed. The test administrator's Nova connection remained configured.
- Login passwords remain nonempty but no longer require 12 characters, digits, symbols, or mixed case. Existing passwords were unchanged.
- Optimization now always displays all batches and sends no batch filter or manual line-haul destination. The Batch dropdown was removed. Server error bodies are displayed instead of raw HTTP exception messages.
- API/web images: `aurora-auth-api:20261012-2` (`sha256:0ef5fef4ddd65623633b0c1752a630757edf1b2c81ae0806cfc678195623ba90`), `aurora-auth-web:20261012-2` (`sha256:92761e7432b6c8bc0dd3738ff55d4de14d3f3a6de9b465ec6a370aa7daa8545a`).
- Archive SHA256: `7f3d1f3629c96a431053dc4b882bb3ad52317d57d87e0d884e8ba8f33e6a07d1`. Full build-time working-tree snapshot; other concurrent workspace changes may also be included.
- Backup and rollback image metadata: `/opt/aurora-auth/backups/20261012-2`; previous release `20261012-1`. Rolling back to that release requires reconstructing `user_tenant.nova_connection` from the assigned catalog connection before restoring the old binaries; see release-local `deploy/deploy.py` rollback handler. Do not simply switch old binaries after this schema change.
- Validation: 29 local administration integration checks passed (short password creation/reset, assignment/reassignment, encrypted credentials, stale edits, assigned database deletion protection, tenant isolation, non-admin read/write restrictions, revoked/deleted user handling). Temporary fixtures were deleted; no Firebird writes. Local browser verified Databases, Users dropdown, and absent Batch selector. Public readiness, login, PKCE, admin permission, Users and Databases APIs passed after deployment.

## Aurora update � 2026-10-12, 09:56 ET

Published release `20261012-3` at Bryan's explicit request, retaining Version 1.0.0. Includes the grouped database editor, removal of help text and coordinate inputs, automatic address lookup, and address-based master-list warnings. Published the current working-tree build.

- Archive SHA256: `9e75b60151be60d571ac51b4b0e1cb2f62c9d354a5e0616fb48c12eff54934b0`, verified after upload.
- API image: `sha256:063d9e84bd4e30c9096208a6275285db8c847937757c2c972364deeb093d9820`; web: `sha256:409a6152d7a179ec6544c7983823c11a295ccbeae633adde5a073868c286e4be`.
- Backup: `/opt/aurora-auth/backups/20261012-3`; rollback release `20261012-2`. Database dump catalog validated. No schema changes, migration, seeding or data reset.
- Release publish and address regression checks passed. Public readiness, administrator login, PKCE, Users/Databases APIs, favicon, and Nova transportation nodes passed. MHT returned no address warning.

## Delete controls and fleet warnings � 2026-10-12

Published release `20261012-4` at Bryan's explicit request. Includes Pages section-folder organization, truck/node deletion restricted to aurora:Admin and aurora:PowerUser, confirmation controls, revision checks and dependency checks, address-based fleet-list warnings, and 16px spacing below fleet filters. Trucks use Nova EH_DELETE_DATE; unreferenced nodes are removed. No schema changes or data reset.

- Archive SHA256: `ce3f7146fb596803299dca86fddd40254964635b657cb45dfe13a0f957a7388d`.
- API image: `sha256:5cfc34accb4124a6bb9363b6c61c471eeda7033ac533692f153d32c215a3df65`; web image: `sha256:e638e4884ab38f081748d749b9ee150fd6331b603144d0c095fc873061d2a625`.
- Backup: `/opt/aurora-auth/backups/20261012-4`; rollback release `20261012-3`. Database backup validated before updating only API/web.
- Passed 18 deletion checks (role authorization, stale revisions, referenced node, rollback-only truck deletion, disposable node deletion and cleanup), 16 routing checks, and 16 login checks. Published login, PKCE, administration APIs, Nova node/fleet reads, and stale-revision delete rejection passed. All ten vehicles remain; all nine TEST trucks return no warnings. Local API/client also rebuilt and restarted.

## Truck editor and stylesheet refresh � 2026-10-12

Final release `20261012-5` includes an existing-truck terminal dropdown, removal of `(Nova units)` labels, and a versioned isolated stylesheet URL to refresh cached pre-folder-move styles. Builds and published verification passed; existing ten vehicles preserved.

- Archive SHA256: `55aeae61f916c1152b0631080d18327306514fcd25b70311cc323da4a1521584`.
- API: `sha256:0d9986ddc081cfb6bb2021eae78cddc282e57f76e67b23412563ab3629301e0b`; web: `sha256:9986a0d72df4838628c82b190094a16012b6204f87a4122e058e602ade2dbc8f`.
- Backup: `/opt/aurora-auth/backups/20261012-5`; previous release `20261012-4`. No migrations or seeding.

## Optimization node selector � 2026-10-12

Published release `20261012-6` at Bryan's explicit request. Optimization now lists eligible node types regardless of saved coordinates, so nodes remain selectable for viewing their orders. API reused from release 5; no database schema changes or seeding.

- Archive SHA256: `a115ae2cb9b882164e65639011628d704d006383ea5cddcce9251d61e523caad`, verified on server.
- API image: `sha256:b5e72451f1e956b3712e5a91d0089afbe1b940559e5e2454f342285884e84ca0`; web: `sha256:1341272bd6c8d194bfb3fe015eb161f43afb9ed3f9f8e3ae04e7a98e933b7d7e`.
- Backup and rollback metadata: `/opt/aurora-auth/backups/20261012-6`; previous release `20261012-5`.
- Build, public readiness, login/PKCE, administrator APIs, Nova nodes/fleet, and stale-delete protection checks passed.

## Vehicle operating rates — 2026-10-12

Published release `20261012-7` at Bryan's request. Vehicle editors now expose Nova's existing `EH_TOT_FIXED_COST_HR` (cost per hour) and `EH_TOT_VAR_COST_MI` (cost per mile). Blank rates produce grid alerts and exclude trucks from planning; explicit zero is valid. Requests convert per-mile rates to per-kilometer rates by dividing by 1.609344. Removed automatic zero defaults from request generation.

- 126 routing checks passed, including blank/zero validation, eligibility, manual rates and distance conversion. API/client Release publish passed.
- Published login/PKCE and live fleet checks passed. Verified 10 records; filled blank rates with zero on 9, preserving existing rates. A live blank/save/restore check confirmed the warning, then restored original explicit rates. Nine cargo trucks are planning eligible; PW remains a tractor.
- Archive SHA256: `f21e91be29deeb3451474646ecf8312bc0d3174ca2126ad2d5304595919b8519` (clean archive excludes local saved plans).
- API image: `sha256:9ff6407a6ef7d6083173ade03985a8d1a4f298b2752b528b9c273f828dc175ef`; web: `sha256:d02c276b60af637d9098cb7b95efc3926401739cf14e984da962bd26807a31cf`.
- Backup/rollback: `/opt/aurora-auth/backups/20261012-7`; prior release `20261012-6`. Original truck records saved as `truck-rates-before.json` in the backup. No schema changes or order resets.

## Live result selection and simple activity log — 2026-10-12

Published `20261012-8` at Bryan's request. Optimization opens a modal listing retained result snapshots. Accept stops the search and selects the exact server-stored snapshot, including earlier results; completed searches wait for acceptance. Selection survives reconnects and is used for manifest creation. Activity log displays time, user, and plain actions, omits routine reads/refreshes, and offers a confirmed Clear log action. Clear uses a per-user, tenant-isolated cutoff; audit rows are retained and new activity remains visible.

- 43 planning-session checks and 14 activity-log checks passed. Release builds passed.
- Applied additive migrations 0022 and 0023 with seeding disabled. Backup/rollback: `/opt/aurora-auth/backups/20261012-8`; prior release 20261012-7.
- Verified live synthetic optimization, candidate acceptance, unchanged accepted snapshot on subsequent polls, plain log descriptions, and published log/confirmation UI. No business orders or manifests were created by the synthetic test.
- Transactional database checks verified clear cutoff, visibility of subsequent activity, and isolation from other users, then rolled back without clearing Bryan's log.
- Archive SHA256: `701b4b1d8d2e27c589f416f06a7929e9ff711b167f056448954f6c1d55f862e4`.
- API image: `sha256:59075521e4b6392eb481ac1ce9fa37e039f98f755364673e0d9fad33e95142b8`; web: `sha256:7d591eba251cb34f3d1bda3629bc8faeedbc93f7103bf60457aa6c0bce543b0a`.

## Unified manifests and status history — 2026-10-12

Published release `20261012-11` to https://user.aurorasoftware.com at Bryan's request (release 10 was an intermediate deployment).

- Removed Dispatch navigation and redirected its old URL to Manifests. All statuses and manifest-date filters are available in the unified list. Click a manifest number to open assignment, status, route orders, map, and native status history.
- Added Assigned / Dispatched / In transit / Closed transitions with actual departure/close times, revision checks, idempotent retries, native order/equipment updates, and reconciliation checks before close. No real manifest was advanced during testing.
- History shows old/new status, event/recorded times, and Nova operator initials. Application log retains full username and searchable business references: manifest numbers, order/PRO numbers, and truck unit numbers. Fixed legacy encoding when displaying existing dispatch references.
- Includes the empty optimization-results grid, removal of Stop searching, and removal of the requested explanatory text.
- API/client Release builds passed. 53 transactional dispatch/status checks and 38 application-log checks passed. Live readiness, existing login/PKCE, eight native manifest/history reads, lifecycle filters, and historical manifest-number lookup passed. Browser verification confirmed removed Dispatch navigation, history on manifest 11455, and required actual departure input; unsaved verification edits were canceled.
- No migrations, seeding, business-order resets, or credential changes. Backup/rollback: `/opt/aurora-auth/backups/20261012-11`; earlier release 9 is also retained.
- Archive: `artifacts/aurora-release-20261012-11.tar.gz`; SHA256 `40bc93e087990b77ecba60ff24755ad87c92f11390192e3b6df2435d1d9103bc` (verified after upload).
- API image: `sha256:a7c11cdb974bbc780944c907dbf1b3485e8aabae343b20dfb18f9c66902b9ed2`; web: `sha256:4f7b70d66191feba3f5d7af08db6d11ccbfd57ea8cc4646b320fe1bbb8ae8a2b`.
- Individual delivery/exception capture and print/driver-device handoff remain separate features. Closing currently requires delivery or terminal-receipt records in Nova.


## Planner usability updates � 2026-10-12

Published release `20261012-13` (following intermediate release 12) to https://user.aurorasoftware.com.

- Removed route Include controls; Create Manifest uses all nonempty routes. Results stays hidden until optimization is started or an existing session is resumed.
- Removed visible UTC offset fields/text in planner inputs, fleet management, grids, map times, and formatted CSV exports. Existing offsets remain in internal scheduling values.
- Added collapsible sidebar with labeled icon links and navigation-session preference. Enlarged optimization overview/results maps and added map resize observation.
- Order numbers in Overview and stop/PRO numbers in Results center the map and open the corresponding marker, scoped to the selected truck.
- Release client publish and JavaScript syntax checks passed. Map-focus checks passed for shared coordinates, truck-specific selection, unknown orders, and disposal. Live readiness, login/PKCE, eight manifest/history reads, lifecycle filters and app-log references passed.
- Backup: `/opt/aurora-auth/backups/20261012-13`. No migrations or business-data resets.
- Archive SHA256: `a9e970442c5ccc9aae91892a921acc7b49c8e91227a41594b0e924e56c956ef1` (verified remotely).
- API image: `sha256:04578e6f87170c9484fa4774f29a72cbc5dbfdff0b3ed53d5fa1bad5100afd2d`; web image: `sha256:f066c5e38da27f53ceb0674d167058e3b8e67e0c42ec2feab7bd4b7ac71591e6`.
- Investigated separate stops for same-address Nova orders: BuildRequest currently assigns each order its own delivery location ID. Shared-address grouping remains unimplemented; no grouping behavior was changed in this release.
- Live browser verification confirmed the sidebar collapses to 58 pixels, preserves collapse across navigation, and expands again.

Republished the verified release 13 build as release 20261012-14 at Bryan's request. Backup: /opt/aurora-auth/backups/20261012-14. Readiness, existing login/PKCE, ten manifest/history reads, lifecycle filters and searchable app-log references passed. No source or business-data changes.


## Explicit order decisions and re-optimization — 2026-10-13

Published release `20261013-1` to https://user.aurorasoftware.com at Bryan's request.

- Added per-order Automatic assignment, Leave out of this plan, and Pin to truck controls in route results and Left off, plus an Order decisions panel for restoring exclusions.
- Decisions persist in saved session inputs. Request preparation omits excluded deliveries and applies hard order/vehicle constraints for pins; previous route seeds cannot reintroduce exclusions or old truck assignments. Original orders remain available for undo.
- Pending edits disable manifest creation until a new plan is accepted. Unsatisfied pins are surfaced and block manifest creation in both client and API. Unavailable pinned trucks and all-excluded plans receive validation errors.
- Fixed numeric calculation-budget parsing so re-optimization uses the selected budget.
- Validation: 142 routing checks and 48 planning-session checks passed; API/client Release publishes succeeded. Live synthetic PTV runs verified moving an order to another truck, excluding another order, session restoration, then restoring the exclusion while retaining the pin. Existing login/PKCE, ten manifest/history reads, lifecycle filters, and log references passed.
- No schema migration, reset, or business manifest creation. Backup/rollback: `/opt/aurora-auth/backups/20261013-1`.
- Archive SHA256: `4c3234be6ea6f7f417a41bb1821c4dbd9a142d60cc82ead204f18389c828a9ff` (verified remotely).
- API image: `sha256:828e3e31667bd1897067e72a1393857fb1daeb34dd0e3dbaf1481bf4daa1e0da`; web image: `sha256:af2b4fab168d11c79d4d481060ae00e060eb8534c14a1a818c7b8db9100296d6`.


## Automatic least-mile plan selection — 2026-10-13

Published client release `20261013-2` to https://user.aurorasoftware.com.

- Normal optimization waits for SUCCEEDED, selects the stored candidate with the lowest unrounded distance, accepts it through the existing API, and opens the routes. No comparison dialog or manual acceptance is shown.
- Original results dialog and manual selection remain behind the planning URL query parameter `debugOptimization=true`. Failed acceptance stays recoverable via Reconnect; previously accepted sessions keep their accepted result.
- Added deterministic distance selection and tests for cost independence, ordering, ties, invalid distances, and empty candidates. All 147 routing checks passed and the Release client publish succeeded.
- Client-only deployment retained the running API. A concurrent authentication change made the attempted API build fail on MapAccountPasswordEndpoints. Isolated client build sources under artifacts/least-miles-source omit the unfinished password/invitation UI; shared working files are untouched by that isolation. The packaged client comes from release-20261013-2/client-isolated.
- Live readiness, administrator login/PKCE, ten manifest/history reads, lifecycle filters, and log references passed. No migrations or business-data changes.
- Backup: `/opt/aurora-auth/backups/20261013-2`. Archive SHA256: `ceeda6ecda3ec4d7e12d95640ad1986e3dd7efe1888bdd11e8dbfcf4843b2b57`. Web image: `sha256:d6b93c8137a78d429526337d13349274f0cbda2033d36d8f44ae2ca6bd6202be`.


## Inline order actions and operational summary — 2026-10-13

Published client release `20261013-3`.

- Removed Order decisions panel. Routed orders have Remove from plan beside the PRO. Left off has Add to truck with an on-demand picker; removed orders are labeled Removed by planner and offer Include again. Existing explicit assignment constraints are retained.
- Left off is reconstructed from original plan inputs and result assignments so removed orders retain their details after re-optimization and can be restored. Left off counts include these orders. Pending changes require re-optimization.
- Summary now contains operational totals and a per-truck table; narrative notes, request diagnostics, stop audit, and HTTP status text are omitted. Distances display in miles. Provider names are removed from displayed labels and sanitized in routing errors, reasons, and map popups.
- 151 routing checks passed. Isolated Release client build passed. Browser testing with synthetic data verified automatic acceptance without a dialog, removal, Left off labeling, Include again, truck-picker selection, and summary layout. No business orders were modified.
- Client-only deployment retained the API and excluded concurrent authentication work, using the previous isolated client snapshot plus this task's files. Live login/PKCE, ten manifest/history reads, lifecycle filters, and log checks passed.
- Backup: `/opt/aurora-auth/backups/20261013-3`. Archive SHA256: `a339784b7097ddcd3d412d97fdf18a7539da909ababa8f6dd9697ea9439bcf8a`. Web image: `sha256:26f8547c30b0c08b2fd69ceccea79a5b650b50966db9b19f4b8d49ec9c910591`.

## Compact order controls and adjustable map — 2026-10-13

Published client release `20261013-4`.

- Routed orders now have a small red remove button at the far right; Left off uses a truck-icon button to open the existing assignment picker. Action cells remain visible while the table scrolls.
- Added a draggable divider between results and map, keyboard resizing, session persistence, and double-click reset. Narrow screens retain stacked panels; map resize observer adjusts tiles.
- Left-off rows and red map markers share sequential numbers. Numbering is assigned before missing-coordinate filtering. Shared locations display the corresponding row numbers together; clicking a row number opens its marker popup.
- Isolated Release client build and splitter checks passed. Synthetic browser verification covered left-off numbering/focus, truck picker, divider width changes (377 to 743 pixels), reset, and compact buttons.
- Client-only deployment retained the running API and excluded concurrent authentication work. Live readiness, login/PKCE, ten manifest/history reads, lifecycle filters, and log checks passed. No migrations or business-order edits.
- Backup: `/opt/aurora-auth/backups/20261013-4`. Archive SHA256: `51a54f385c1ed0780a48ac2cfb9a7ef0dde5be69e6c9c1ca48ca32b449bc2441`. Web image: `sha256:166a1d8018e816e0277e9b0e53e46f15dec32043442013af454526a513e8d9f1`.

## Leaner Results and manifest capacity — 2026-10-13

Published client release `20261013-5`.

- Added the existing optimizer-reported load utilization to each manifest header, formatted as a whole percentage (for example, 50% capacity).
- Removed the top status/totals cards and previous-run comparison strip from Results. Overall totals remain in Summary; scheduling-conflict and summary-error alerts remain visible.
- Isolated Release client build passed. Browser preview confirmed 0.5 renders as 50% and Results begins directly with the manifest/map panels after the heading.
- Client-only publish retained API and excluded concurrent authentication work. Readiness, login/PKCE, manifest dispatch/status/history reads (10), and lifecycle filters passed. The legacy log fixture assertion did not pass: search for manifest 11455 returned zero records, so its exact reference label could not be checked. No API or log behavior was changed by this release.
- Backup: `/opt/aurora-auth/backups/20261013-5`. Archive SHA256: `b732af8636c56bc4cdaa3859ddb83a08af79eac8a53666099fece86e9e935833`. Web image: `sha256:abb4f1d50f3c961c222e0f194fd95002536085d930f118c897bcc3e08c00b332`.

## Manual-assignment overtime and manifest tabs — 2026-10-13

Published release `20261013-6`.

- Manual order assignments extend the selected truck's planned return by 24 hours in the prepared solver request and remove that truck's maximum route-duration constraint. The API requires a latest return time; this is a bounded overtime allowance. Other trucks, capacity, appointments, driving-duration limits, and breaks are unchanged. Multiple manual orders do not accumulate additional days. Removing the last pin restores ordinary limits on the next preparation.
- Original truck settings stay in reporting metadata (stripped before provider submission), preserving the original manifest hours budget. The manifest header shows whole minutes over shift, including late returns and route-duration overruns. Removed the redundant Show on map header button.
- Native manifest editing now has Details, Orders, and History tabs. Details contains assignment/status and map; Orders contains the existing order list/detail links; History contains status history. Unsaved assignment fields survive tab switches; maps are disposed and redrawn when returning to Details.
- 169 routing checks passed against both working and isolated release sources. Checks cover full/seeded overtime preparation, positive capacity constraints, appointment preservation, unaffected trucks, undo, multiple pins, original budget, and overtime calculations. Release builds passed. Synthetic browser checks verified all tabs, preserved unsaved driver selection, map return, the 42-minute overtime warning, and absence of Show on map.
- API package retains the published release-20261013-1 application and dependencies, replacing only Aurora.Contracts and Aurora.Modules.Routing assemblies built against isolated contracts. Routing module source had no post-release changes before this task. Isolated client excludes concurrent authentication changes. No schema migrations or business manifest/order edits.
- Live readiness, login/PKCE, dispatch/status/history reads (10), log-search endpoint, and lifecycle filters passed. Legacy manifest 11455 log-reference fixture skipped because search returns no records.
- Backup: `/opt/aurora-auth/backups/20261013-6`. Archive SHA256: `36dbee6b29f86c32977e44de50836eb6eef5cec2c4745b77cbed0f0be6c653bc`. API image: `sha256:9f5a112689b706c4d24fe98d20e57da5d9d31bcc99b3004a35c76bcec61c2e9f`. Web image: `sha256:7b81d2eb4c2f45339d37bbcd9ec938b1cc98ec6c76508aa1f072bda6b4e2c777`.

## Latest full workspace release — 2026-10-14

Published `20261014-1` to https://user.aurorasoftware.com at the user's request.

- Built the latest full workspace API and client, including account-password and invitation-email work previously excluded from isolated releases. All prior routing, overtime, compact controls, and manifest tabs are included.
- Release API build passed with no warnings/errors; both publishes succeeded. Routing (169), planning-session (48), invitation/password (31), client-authentication, login (16), and divider checks passed. Invitation tests use mocks; no emails were sent. AdminUserChecks could not run without FIREBIRD_CHECK_CONFIG; live administrator login and user listing passed instead (not a replacement for DB mutation coverage).
- Created and validated the database backup and retained rollback images. Preserved existing data/configuration, performed no migrations or resets. Excluded local App_Data and Development appsettings from the package.
- Live readiness, administrator login/PKCE, admin user listing, manifest dispatch/status/history reads (10), log search, and lifecycle filters passed. Historical manifest 11455 reference fixture skipped because it has no matching records.
- Existing server has no RESEND_API_KEY; public origin is configured. Invitation email remains unavailable until the key is configured. No credentials or authentication policies were changed during deployment.
- Backup: `/opt/aurora-auth/backups/20261014-1`; previous release `20261013-6`. SHA256: `31f74ab3d088270bdc3dfa530f921ad38f2cab18c3ed55c55e411bd583413f24`. API image: `sha256:5388700ac1b41d817f25246946c148bb8fa5e5c8c51a5a9ca0c6b78e4f020142`. Web image: `sha256:5fff28427951c32b30c63fc60dc2862f9362e492fe581ef900391a0667b7d99a`.

## Scoped manual additions — 2026-10-14

Published `20261014-2` at the user's request.

- Single-truck assignment is one click; misleading Allow reassignment label replaced with Undo.
- Manual additions retain other unscheduled orders as left-off decisions in the original request. Prepared runs contain existing manifest orders plus explicit additions; held orders remain visible and can be added later.
- API/client Release publishes passed. 179 routing checks and 48 planning-session checks passed, including full/seeded scoped additions, repeated edits, later additions, exclusions, and preserved original input.
- Live readiness, administrator login/PKCE, manifest dispatch/status/history reads (10), log search, and lifecycle filters passed. Historical manifest reference fixture skipped because it has no matching records. No migrations, resets, or business-data edits.
- Backup: `/opt/aurora-auth/backups/20261014-2`; previous release `20261014-1`. Archive SHA256: `b6eac6626d163fa6e81c71e3badfcbaa6a68d8c4cf4e577e646f3498a3c0c113`. API image: `sha256:fe6212677cbbd8102651c43e226716328fdae25326c6300fa9f28243125043dd`. Web image: `sha256:90692e1a5e9c624c447f5a8ea546344125fb61a9bc24eeed3c6822b32b0c8781`.

## Admin testing reset — 2026-10-14

Published `20261014-3` at the user's request.

- Removed Admin Import, its page/contracts/store/sample, replacement endpoints, and legacy order-import API entry point. Added admin-only Reset preview and confirmed execution endpoints plus Admin Reset screen.
- Reset preserves setup/source orders/pre-existing Nova manifests. Company-tagged Aurora manifests are deleted through native triggers, order assignment/execution state is reset, local drafts/manifests/route plans are cleared, test workflow state is cleared with revision increments, and associated encrypted planning sessions are removed. Audit history remains. New manually created manifests now carry company provenance; older unmarked manifests are conservatively preserved.
- Checks: 25 native/authorization reset checks (all native synthetic writes rolled back); 26 local PostgreSQL/authorization checks using isolated fixture database; 50 session checks; 36 freight unit checks; 17 workspace unit checks. Corrected an older workspace fleet fixture to specify zero mileage cost. Simulated browser checks verified disabled confirmation, enabling after RESET, and success feedback. API/client Release publishes passed. No production/test-server reset was executed.
- Read-only live Reset preview passed: 21 manifests and 24 drafts. Retired import endpoints return 404/405. Readiness, login/PKCE, dispatch/history/status reads and lifecycle filters passed. Historical log fixture had no records and was skipped.
- No migration required. Existing data/configuration backed up and preserved. Backup `/opt/aurora-auth/backups/20261014-3`, prior release `20261014-2`. Archive SHA256 `a7d0c4bb330a222480f022274a9ab517419b698c1c831db5d9cec76339544ce9`. API image `sha256:6b72d342c420a974fbfdc3df38963ef0079cc8a5c7d969e3d2d7cf7a9fce231f`; web image `sha256:eb8374266e142a80ff9ccebc049e937930aa5930a1e50b4c0ca5f6c8b904d293`.

## Orders-based route planning — 2026-10-14

Published `20261014-4` at the user's request.

- Orders now starts planning with checkbox and Shift-click selection, Ready to route filtering, and resumable open plans. Removed the Optimization sidebar entry and replaced planning tabs with a setup screen followed by map/manifest review. Truck/hour and appointment editors open over results; advanced settings are collapsed.
- Manifest assignment warnings appear on the edit screen when changing status. Includes removal of redundant optimization-grid labels.
- Full API/client Release publishes passed. Prior local validation passed 179 routing checks and synthetic browser checks covering range selection, paging, draft resume, schedule edits, optimization, and truck-setting save/cancel.
- Live readiness, login/PKCE, manifest dispatch/status/history reads, lifecycle filters, Ready-to-route Orders (50 rows), and open-draft reads passed. Admin Reset preview was read-only; retired import endpoints remain removed. Historical log fixture skipped because no matching records exist.
- Database backup validated and rollback images retained. No migrations or resets. Local App_Data and Development settings (including compressed variants) excluded from the uploaded archive.
- Backup `/opt/aurora-auth/backups/20261014-4`; prior release `20261014-3`. Archive SHA256 `a180cbe717b0712d9440f87087cdeeea3c312c9616bd900fc31308134582d049`. API image `sha256:4a9eb91c63cd782ef276c696f71380019a624ecfa72299a618819140f8908213`; web image `sha256:60d37d1a8c3664c1c7e4d19b6c653d85606040381306e49e125484547dc0e0f7`.

## Manifest details and interface cleanup — 2026-10-14

Published `20261014-5` at the user's request.

- Manifest Details now shows date/terminal and saved route estimates, compact assignment/status fields, dispatcher notes, and the map underneath at every screen size. Missing saved estimates display a dash. Estimates use the manifest timezone and retained optimized route.
- Native notes save independently of assignment and status, enforce 4,000 characters and optimistic concurrency, reject closed/canceled manifests, and retain the existing export receipt for retry recovery. No schema migration required.
- Includes pending UI work: removed top password shortcut; 10-second planning/replanning budget; removed Advanced settings; compact right-aligned buttons; Appointments caption; Escape for older overlays; Terminals labels; Orders terminal dropdown code and name. Fleet Management naming was discussed but not changed.
- API/client Release publishes succeeded. 66 dispatch checks passed, including native note persistence, stale revisions, limits, canceled-manifest rejection, and receipt retention. All synthetic native records and trigger effects rolled back. Browser preview verified notes save/reload, map underneath, and no horizontal overflow at 1280 pixels. Earlier routing checks passed 179 cases.
- Live readiness, login/PKCE, dispatch/history reads, lifecycle filters, terminal code/name options, new manifest detail/notes fields, Orders and open drafts passed. One of the checked manifests had a saved optimization summary. Historical log fixture absent and skipped. Reset was read-only; no server reset or business edits performed.
- Backup `/opt/aurora-auth/backups/20261014-5`; prior release `20261014-4`. Archive SHA256 `c45f17de946cffc1d59004d71914640e01157e6989a4f0a63afb1ded0b6d91b7`. API image `sha256:bbe6b59fbabfd12ad9a8d4787fca874ee0e3237b0e24b4d807043c97e7ac6e3a`; web image `sha256:b3a09a94672606229df0dc896383084f02d5fe8b0db1a6fb98edb982844aa9ce`. Local App_Data and Development settings excluded.

## Requested republish — 2026-10-14

Republished the latest full workspace as `20261014-6` at the user's request. API/client Release publishes succeeded. Live readiness, administrator login/PKCE, Orders, open plans, terminal labels, manifest details/notes, dispatch/history and lifecycle checks passed. Historical log fixture absent and skipped. Database backup validated; no migrations, resets or business-data edits. Local data and Development settings excluded.

Backup `/opt/aurora-auth/backups/20261014-6`; prior release `20261014-5`. Archive SHA256 `d89111b7f34a0412e433b5054d0a8dd02426f2506ed89a9225717ef4c6c5064d`. API image `sha256:10ce44b768a6001bc274f092aec37b31282907f7535fb76d8fce9fbaeab1bfc4`; web image `sha256:9c2d145e3d32f00dee45d8df82f172816d3c7e7ee08a7d4eaac656d5c4f39cd3`.

## Initial route schedule and Trucks popup — 2026-10-14

Published `20261014-7` at the user's request.

- Plan routes on Orders now opens a compact Route schedule dialog for date, departure, and return. Continue validates the schedule, rechecks availability, and creates the selected-order draft with those values. Cancel/Escape leaves selection intact without creating a draft. Resuming an existing plan skips this dialog.
- Removed shared schedule controls from setup and the Results truck popup. Results button and popup title now say Trucks; individual truck controls remain. Includes pending Equipment sidebar label, adjacent Appointments/Optimize buttons, and Save caption.
- API/client Release publishes succeeded. Synthetic browser validation covered range selection, Escape/Cancel, equal-time rejection, Oct 16 / 07:00–19:00 propagation, resume without schedule dialog, successful optimization, and absence of schedule fields from Trucks. Screenshots saved in artifacts.
- Live readiness, administrator login/PKCE, Orders/open plans, terminal labels, manifest details/notes, dispatch/history and lifecycle checks passed. Historical log fixture absent and skipped. Backup validated; no migrations, resets, or business-data edits. Local data and Development settings excluded.
- Backup `/opt/aurora-auth/backups/20261014-7`; prior release `20261014-6`. Archive SHA256 `462af1c57f96d957e9c9e062a92f40f4796f36819579e0a90acf4ff252c28fa7`. API image `sha256:9041d74aef3f6d365ce95ecbdb0250753644a8a372c68672fe8bc8e47c525a07`; web image `sha256:c2918bf20f398da74d38cad1c17182eb949cb1fb38b422a46e51c75693a8f70f`.

## Optimization progress and results cleanup — 2026-10-14

Published `20261014-8` at the user's request.

- Optimize and Re-optimize show an Optimizing dialog with the existing animated Aurora arrows. Results manifests start collapsed; expanding one selects its route on the map, and collapsing it restores all routes. Removed the redundant Truck routes heading and repeated plan caption.
- API/client Release publishes passed. Local synthetic browser checks verified both progress flows, completion, collapsed defaults, and expansion/map selection. Live readiness, administrator login/PKCE, Orders/open plans, terminal labels, manifest details/notes, dispatch/history and lifecycle checks passed. Historical log fixture absent and skipped.
- Database backup validated and rollback images retained. No migrations, resets, or business-data edits. Local App_Data and Development settings excluded.
- Backup `/opt/aurora-auth/backups/20261014-8`; prior release `20261014-7`. Archive SHA256 `bee2c6f2a926466ade00720580f1e50475a4d2d908492142e48ce4b1c2f1dfc5`. API image `sha256:1018e23c182d95ea408e24de3625e8ad8284406b5f3dd57c79a891da0a918ee7`; web image `sha256:955125f6d6a67747dd708e7acb8fae2b8e27d1d7c663e995653572106cbfb2fc`.

## 2026-10-15 — 20261015-1: automatic transfer and consolidation plans

Published API and web to https://user.aurorasoftware.com on explicit user request after review.

- Orders > Plan routes groups local deliveries and terminal transfers atomically. Receiving terminals marked Consolidation generate consolidation plans; other terminal transfers generate line-haul plans. Saved transfer destinations and kinds survive re-optimization.
- Terminals > edit > Routing stores source-scoped consolidation designation, service area, routing role and Route Via. Revision conflicts and routing cycles are rejected. Native delivery-terminal data supplies explicit order destinations.
- Transfer manifests retain their kind in the hidden export receipt, show origin/destination and use terminal endpoints on the map. Customer destinations remain intact. Receipt handling remains the existing Nova workflow.
- Additive migration 0024_terminal_routing rehearsed against a restored backup, then applied successfully with seeding disabled. Existing data preserved.
- Validation: 192 RoutingChecks, 23 OrderWorkspaceChecks, 34 native FirebirdRoutingChecks passed. Native export checks rolled back. Browser preview confirmed separate General, Consolidation and Line haul plans. Release publishes succeeded.
- Server smoke checks passed: login/PKCE, Orders/open plans, native manifests/history/details, terminal routing read, reset read-only preview, retired import endpoints. Legacy manifest-reference fixture absent and skipped.
- Backup/rollback records: /opt/aurora-auth/backups/20261015-1; migration rehearsal database aurora_deploy_20261015_1 retained.
- Archive SHA256: a16e12bff29c1bfd7f0c6abd4e3c8b3a742affa6b53f7303288e08a386a8d787
- API image: aurora-auth-api:20261015-1 sha256:b1aa8e0e9ae58d5a5eacbc0b4f38e65431de42687aa0bfb9737b5cb166a2b129
- Web image: aurora-auth-web:20261015-1 sha256:ac5fe81fcfb14f3f872a9aeafa54f6242fc05fd1bdf9c747d30a17c294e10116
- Evidence: artifacts/publish-20261015-1.log and artifacts/consolidation-plans-20261015.png.

## 2026-10-15 — 20261015-2: terminal routing on the main edit form

Published on explicit user request to https://user.aurorasoftware.com. Removed separate Routing popup. Consolidation checkbox, routing role, Route Via and service area now appear on the main add/edit terminal form and use its Save/Cancel controls. Native save revision is retained if the subsequent routing save fails, with a clear partial-save error for retry. Routing validation runs before saving native details.

Validation: Client build and Release publish succeeded. Browser preview verified the inline layout, checking consolidation, Save/reopen persistence, and invalid service-area keyboard entry blocking Save while retaining the form. Server login/PKCE, Orders/open plans, manifest details/history and terminal-routing read smoke checks passed. No schema migration. API reused release 20261015-1 published binaries.

Backup: /opt/aurora-auth/backups/20261015-2. Archive SHA256 bf2de0f89ff5b90dfd11da28f485308ba740d5f45bf8946a8dbbaaebf22115a7.
API image sha256:36c489b45b33234b846e73f07d8b875d1682f858e05bfdbf8191b08fe91f64a6.
Web image sha256:d451b4be5a4817e09335f4ef950c696077512b8047cfff54e275e26c5672482e.
Evidence: artifacts/publish-20261015-2.log; artifacts/terminal-inline-20261015.png.

## 2026-10-15 — 20261015-3: Nova desktop launch sign-in

Published to https://user.aurorasoftware.com on explicit user request.

- Added opt-in Nova user ID mapping in Admin > Users and display of the assigned Aurora database UUID. Blank mappings preserve existing behavior. Mappings are unique per tenant/database and restricted to the user's default tenant.
- Added /nova-launch fragment landing page and POST /api/auth/nova-launch. The client erases the fragment before loading third-party scripts, removes stale Aurora OIDC user state, and starts the normal PKCE sign-in after ticket verification. Invalid/unavailable launches go to login with a retry message.
- Firebird tickets use 256-bit random lowercase hex secrets and SHA-256 hashes, a maximum 60-second lifetime, and atomic DELETE RETURNING consumption. Aurora checks mapping, product access, lockout and two-factor requirements; no persistent cookie is created. Requests are rate-limited and responses use no-store. Database credentials and tickets are not logged.
- Migration 0025_nova_launch rehearsed on a restored backup and applied with seeding disabled. Scoped SECURITY DEFINER lookups allow only configured pre-auth database/membership lookup without relaxing table RLS. No Nova database schema or business records were changed.
- Validation: 10 disposable embedded Firebird checks (including replay, expiry, future issuance, excessive lifetime and concurrent redemption); 9 restored PostgreSQL mapping/listing checks with fixtures rolled back; JavaScript fragment/account cleanup checks; 16 existing login checks; client auth/session checks; API/client/migration Release publishes. Local PostgreSQL used port 15432 because Windows refused binding 55432. Its migration journal was updated after applying the migration as aurora_owner.
- Live checks passed: readiness, administrator login/PKCE, Orders/open plans, manifests/history/details, terminal routing, mapping administration, invalid/unknown launch rejection without cookies, origin rejection, launch page/helper deployment. No full successful Nova launch is claimed: Steve must implement trusted ticket issuance and the Nova button, then enable mappings for joint testing.
- Handoff: docs/email-steve-nova-launch.txt (draft only; not sent), docs/nova-launch-firebird.sql (Nova-side schema; not applied). Requires restricted ticket issuance that verifies Nova identity; shared unrestricted DB writes must not be treated as authenticated users. Closing Nova does not revoke an existing Aurora session.
- Backup: /opt/aurora-auth/backups/20261015-3; previous release 20261015-2; rehearsal database aurora_deploy_20261015_3 retained. Existing data/configuration preserved; local App_Data and Development settings excluded.
- Archive SHA256: e31f9b6746d88c74bbb07c7dc8a0100330a0dfac4af3981c5ebe0e13fd8b0337.
- API image: sha256:eb17ad94677ad6030113e65b95ed56562b178ff8e3ba94c7763f850f715213d3.
- Web image: sha256:6390685612e9351a2d99f80e185b6be1551e2e8e2e4925ab1ff9f28bb57b92ae.
- Evidence: artifacts/publish-20261015-3.log. Existing transitive Pkcs/Xml advisory warnings appeared in the new test project restore; dependency versions were not changed. The migrator's existing optional Kerberos library warning did not prevent rehearsal or live migration success.

## 2026-10-15 — TEST Firebird SRP connection

At the user's request, repaired Aurora's connection to 172.23.0.3:D:\DB\TEST\OPERATIONS.FDB. Server is Firebird 5.0.3. AuthServer already allows Srp256, Srp, Legacy_Auth; the existing SYSDBA credential connected through Legacy_Auth, unsupported by Aurora's managed driver. UserManager lists Srp256 first (not a user-manager plugin), so provisioning explicitly selected USING PLUGIN Srp.

Created dedicated AURORA_TEST_APP SRP account with a random password. Granted TEST database table DML, view SELECT, sequence/exception USAGE and standalone procedure EXECUTE, without grant option, server admin or schema-management privileges. Ticket table is excluded from general DML and gets SELECT/DELETE only if present. No existing accounts, Firebird configuration, business records, or services changed. No server restart required. Recovery credential is DPAPI protected in ignored deploy/private/nova-test-app-password.xml; deployed credentials use Aurora Data Protection.

Updated existing Nova TEST catalog record 9d2af9bf-121e-4b0a-924a-58abf170292e through the administrator API. Managed-driver read, deployed pre-save connection test, and post-save test using the encrypted stored credential all passed. User assignments remain unchanged. No republish required. Updated Steve's draft and the ticket-schema handoff grant to name AURORA_TEST_APP; no email sent. Nova ticket issuance/table installation and user mapping remain outstanding.

## 2026-10-15 — Nova TEST launch ticket table

On explicit user request, created AURORA_LAUNCH_TICKET and its expiry index in 172.23.0.3:D:\DB\TEST\OPERATIONS.FDB. Table existence was checked first. Applied schema and grants transactionally with AUTODDL OFF. AURORA_TEST_APP has SELECT/DELETE; no INSERT/UPDATE was granted.

Verified with one synthetic unlinked ticket: Aurora credentials can read and consume a valid ticket using the launch expiry predicates, the consumed row is absent, and INSERT is denied. The synthetic row was removed by redemption; the denied-insert test rolled back. No Nova business records changed. Steve's email now states the TEST database setup is complete and gives only the Nova integration contract. Account mappings and a full Nova-issued browser launch remain to be tested. No email sent or application republish needed.

## 2026-10-15 — 20261015-4: Admin menu request-failure fallback

Investigated missing Admin menu reported by Bryan. Live bbatts account remains Administrator with WayTest assignment; both administrator/log-access endpoints return true. MainLayout previously hid all administration navigation on any permission-request exception. Updated it to initialize navigation from authenticated role claims and retain that fallback on transport/time-out failures; successful permission responses still override claims and all destination APIs retain authorization. The exact browser failure was not reproduced.

Client Release publish and ClientAuthChecks passed. Reused 20261015-3 API/migration binaries. Published and verified live login/PKCE, admin/log permission endpoints, Users mapping, Orders/open plans, manifests, terminal routing, and Nova launch rejection checks. No schema or account changes. Backup /opt/aurora-auth/backups/20261015-4; previous release 20261015-3.

Archive SHA256: 0abdd5e708a28cfb7df288e2961640b0f8d95cdbfd984445d85e202dd797b6e9.
API image: sha256:ccd3131c447dd47cf6e2ac77c3e57161e9b7bb85dc4d4942ed7e506ea785863d.
Web image: sha256:22a819f588587c6e1742bebf4ceb5d5f86bf73dbce7eb34bacbf5e9a0a08aa34.
Evidence: artifacts/publish-20261015-4.log.

## 2026-10-15 — 20261015-5: Nova credential-to-browser handoff

Published to https://user.aurorasoftware.com on user authorization. This supersedes the Firebird-ticket handoff for Steve: Nova POSTs userName/password to /api/auth/nova/token and opens the returned launchUrl verbatim. No database UUID, Firebird ticket write, or hashing is required in Nova. Codes expire after 60 seconds, are stored only as SHA-256 hashes in Aurora PostgreSQL, and are consumed atomically once by /api/auth/nova/redeem. The fragment is removed before application startup. Credentials are checked through Identity with lockout; MFA, disabled opt-in, elevated roles, removed access and changed security stamps prevent the handoff. Pending codes do not bypass these checks. Session cookies are nonpersistent; endpoints are rate-limited and no-store.

Created nova-test (1874862d-e4fc-4966-9104-a6a0272e1fc8), User only, Nova API sign-in enabled, attached to Nova TEST database 9d2af9bf-121e-4b0a-924a-58abf170292e (172.23.0.3:D:\DB\TEST\OPERATIONS.FDB). Invitation email explicitly suppressed. Random account credential is DPAPI protected in ignored deploy/private/nova-api-test-credential.xml; distinct from the Firebird credential. Shared account activity identifies nova-test, not individual Nova operators. Existing Firebird ticket table remains unused by this flow. Nova source was not changed. Per latest user instruction, email text is provided in chat only; no email sent or draft file created.

Migration 0026 rehearsed against a restored backup and then applied live with seeding disabled. Backup /opt/aurora-auth/backups/20261015-5; previous release 20261015-4. Validation: 30 credential/code API checks, 10 embedded Firebird ticket checks, 9 PostgreSQL mapping/list checks, fragment tests, 16 login checks and 26 AppLog checks passed; API/client/migration Release publishes succeeded. Live smoke tests passed, including existing administrator permissions and business reads. Live nova-test credential issuance, secure browser-cookie redemption, replay rejection, PKCE exchange, userinfo identity, administrator denial and native TEST terminals read all passed. No Nova business writes were performed by verification.

Archive SHA256: 4fdeddf803bda2702604f1f8999f5340af586ee81631d3e55cc3f5723c5cef3d.
API image: sha256:5ad5cd6afd479626e983d9ee984a14f2154a5aab822fd411f944cf8188bee01b.
Web image: sha256:906b6b76240aecf3d0c62ed765fb7fcc216e87b0eda244c8b918078a1d382541.
Evidence: artifacts/publish-20261015-5.log; artifacts/create-verify-nova-api-account.py; artifacts/nova-api-account.json. Existing optional Kerberos library warning did not prevent migration success.

## 2026-10-16 — 20261016-1: Chris's optimization feedback

Published to https://user.aurorasoftware.com on explicit user request. Added native order alias searches with or without a hyphen, visible planning-availability explanations and counts, native manifested/delivered diagnostics, drag handles and dropdown delivery reassignment with automatic reoptimization, load utilization clarification, optional address-line guidance, consolidation terminal explanation, and guidance to maintain driver profiles in Nova. Versioned isolated CSS and map JavaScript as 20261016-feedback.

API and client Release publishes passed. All 204 routing checks and focused delivery-drag/splitter JavaScript checks passed. Prior browser preview verified dropdown and pointer-drag reassignment with simulated route/KPI recalculation. The older map-checks fixture remains incompatible with existing ResizeObserver/marker behavior; it was not changed for this release.

Live verification passed: readiness, existing administrator login and PKCE, permissions, Orders/open plans, manifest dispatch/status/history/details, terminal routing, Nova launch contracts, order searches with both FB- and fb aliases, and versioned drag assets. No migrations, seeding or data reset. Previous release 20261015-5; backup /opt/aurora-auth/backups/20261016-1. Local App_Data and Development configuration (including compressed variants) were excluded from the archive.

API image sha256:12e3885ff042552bfe20cfbd92b6790554c190d0ab711eb3f612d573160f0608.
Web image sha256:ba0c2362c6ed467d0c425a757d3c64c5ff7a8822f506823f254a084e781c0ec4.
Evidence: artifacts/publish-20261016-1.log.
Archive SHA256: 4a10e092263f16ec3803f837b5d777f1b60f9684f10f1f3a3d39f865b44efcce.



## 2026-10-22 — 20261022-2: Orders retry and terminal wording

Published on user request. Planning snapshot refresh, ready-order reads and open-plan reads retry HTTP 408/502/503/504, connection failures and timeouts up to twice with 1- and 2-second delays. Plan creation, cancellation and export writes are not automatically retried. Exhausted failures still leave selection unavailable and display an error.

Replaced user-facing Transportation Nodes wording with Terminals in errors, action labels and activity log nouns. Removed the two requested explanatory paragraphs from terminal editing. Existing workspace changes were preserved.

Validation: 16 focused retry checks and 23 Firebird routing checks passed; API/client Release publishes passed. Deployed login/PKCE, Orders, account lookup, routing metadata, assets and three consecutive planning refresh/order/draft sequences passed. No migrations, seeding or data reset. Backup: /opt/aurora-auth/backups/20261022-2. Previous release: 20261021-2. Archive SHA256: 20e8a23e828b133550ba247440fac0bec22620b3a947e43b879b4b98f2568f87.

The earlier client-only 20261022-1 attempt automatically rolled back after its health-check request received HTTP 403 without the required User-Agent. The combined release used the established verification User-Agent and passed. Retry behavior was verified with simulated failures; no deliberate live outage was introduced.


## 2026-10-22 — 20261022-3: Hide outbound routing

Removed TerminalRouteRules from both terminal editors at user request pending workflow review. Existing Nova direct-to-consignee and explicit receiving-terminal transfers are unchanged. Saved rule data and backend contracts remain intact for future review. Client Release publish passed; live readiness, login/PKCE, Orders, routing metadata and three planning loads passed. Client-only deployment retained the running API container; no migrations or data reset. Rollback: /opt/aurora-auth/backups/20261022-3. Archive SHA256: 8bddc05bf40cfca4895a088ca0e515b81c7e0920278c62fb5cfbc19bb9a92b6c.


## 2026-10-22 — 20261022-4: Remove routing role and service area controls

Removed Routing role and Direct service area from both terminal editors and the corresponding legacy list columns, including unused client-side area feedback. Consolidation remains available. Stored configuration is preserved; Nova planning behavior is unchanged. Client Release build and deployed readiness, login/PKCE, Orders and three planning refresh checks passed. API container unchanged; no migrations or data reset. Rollback: /opt/aurora-auth/backups/20261022-4. Archive SHA256: e7b646c09a2820788509cd98ac8b8eb43a5266f34edeca49f154381e7afa9cfd.


## 2026-10-22 - 20261022-5: Terminal editor

Client-only release: Delete label, state dropdown, code/name heading, Contact label, removal of Identification & assignment legend. API remained 20261022-2.

## 2026-10-22 - 20261022-7: Replan open manifests and optimization locks

Published API/client at user request. Planning combines selected orders with eligible open manifests for the same origin, date and destination. Unlocked assigned manifests can change trucks and membership. Save manifests reuses numbers or creates additional manifests; surplus manifests remain empty and unassigned. Successful save releases previous dispatch assignments and stores new planned equipment for reassignment/release. Removed terminal consolidation marking and the unpublished explicit consolidation workflow.

Lock from optimization persists in native notes alongside export receipts and dispatcher notes. Locked, dispatched, started and completed manifests are excluded; their booked trucks are unavailable to other plans. Native revisions are rechecked under locks before replacing membership. All existing orders must remain scheduled. Cancellation/failure before commit preserves manifests; retries recover a native commit when the subsequent PostgreSQL save or response fails.

Validation: 37 disposable Firebird/PostgreSQL integration checks, 233 routing, 25 workspace, 17 dispatch and 16 planning retry checks (328 total). Integration covers moving trucks, merged/split membership, creating manifests, rollback after replacement starts, stale/lock/dispatch conflicts, vehicle booking, and a simulated PostgreSQL failure after native commit. API/client Release publishes passed. Live login/PKCE, Orders metadata, three planning loads, date-scoped manifest lock metadata/details and unauthenticated lock denial passed. No live PTV optimization was invoked by smoke testing. No migrations, seeding or data reset. Disposable test databases removed.

Release 20261022-6 automatically rolled back when an all-history manifest verification query exceeded 30 seconds; login and Orders had passed. Release 20261022-7 uses the same application build with verification scoped to the normal date range and passed. Prior images used 20261022-5 tags (API content still 20261022-2). Backup and rollback records: /opt/aurora-auth/backups/20261022-7. Local App_Data and Development configuration, including compressed variants, excluded from archives.

Archive SHA256: 473b9bdcd187c39a3a1cfa7412b3e3922788b7794784b23ee29d0348093fe2e6.
API image: sha256:c051ecbfca626f1e9d1d9becdf20ee6fd55818cde30bbf479ad87ea392623353.
Web image: sha256:cddfbdf8bf3530b3d171255b09d2dbae8d0ac12b1a7b7b5d4520c92876d75922.
Evidence: artifacts/replan-integration-checks.log; artifacts/publish-20261022-7.log.


2026-10-22 release 20261022-8: Customer consolidation by consignee/address/origin, capacity templates, held loads across days and explicit release; PTV location aggregation, cube estimates, handling times, multi-day rest controls and truck reservations. Migration 0029 applied without seeding. 354 automated checks passed; synthetic PTV request succeeded. Live auth, Orders refresh, manifest dispatch details and customer settings verified. Backup /opt/aurora-auth/backups/20261022-8. Archive SHA256 95f5031e63b665a07b7066d1e44c0be71f4e7b05ce9371589b759c80abca06ee. Nine-compartment loading remains unsupported/unconfirmed by vendor.

2026-10-22 release 20261022-9: Customer Planning form spacing and responsive field grid. API reused from release 8. Live verification passed again. Backup /opt/aurora-auth/backups/20261022-9. Client/deploy archive SHA256 3d543492dd93d19448e3f5b7dcefb6593c7ce9d502c82b22d47e9258b14745b4; release assembly reuses API from release 8.

2026-10-22 release 20261022-10: Versioned scoped stylesheet URL so existing browser sessions receive the customer form layout. API/client binaries reused from release 9, index.html updated. Full live verification passed. Backup /opt/aurora-auth/backups/20261022-10. Delta archive SHA256 60e551f67ed656c4d007d5b54b3eab6d186b5833b7564b7c8ec08dd75efc703d.

Release 20261022-10 correction: browser startup verification caught unprocessed source HTML in cache-only delta. Restored generated release-9 HTML with stylesheet version change, rebuilt web image and recreated container. Added deployment check for generated import map/script names. Corrected delta archive SHA256 d550c2984f3782e91ce5b7d1677ebf24d3a550e1ba9c6ce2402cfe1ed1050278; supersedes prior release-10 checksum. Web image sha256:8d51e8deb0f03a6b52cc3e45e9689459b4698a747d1f235a1cf7cf5559c169ba.

2026-10-22 release 20261022-11: User editor Save caption, removed Nova launch explanatory text and corrected checkbox spacing. Terminal latitude/longitude restored in Address group with an icon lookup action, specific failure reasons and explicit coordinate save support. API/client Release publishes and address lookup checks passed. Live readiness, login/PKCE, planning, manifest metadata and terminal lookup failure response/styles passed. No migrations or data reset. Backup /opt/aurora-auth/backups/20261022-11. Archive SHA256 4f6465aa174362d70b88da438e35ff242150d506906e90a5d3e375e5093ee46f. Evidence: artifacts/publish-20261022-11.log.

2026-10-22 release 20261022-12: Order pickup/delivery address coordinates and icon lookup, manual coordinate persistence, silent lookup success, narrower left-aligned data alerts with right-aligned Close, and Route/Depart/Return schedule layout. API/client Release publishes passed. Live readiness, login/PKCE, planning, terminal/order lookup failure responses and alert styles passed. No migrations or data reset. Backup /opt/aurora-auth/backups/20261022-12. Archive SHA256 62ea0465b4de4f3a2e3cd04f07b39affdfcde6c8cbdabc3f43639b68b65aa86b. Evidence: artifacts/publish-20261022-12.log.

2026-10-22 release 20261022-13: Planning address errors show the visible PRO number with internal-ID fallback and distinguish pickup/delivery. Structured order and terminal error links open the exact editor using an edit query parameter, including navigation from the route schedule dialog. API/client Release publishes and address-error regression checks passed. Live readiness, auth, planning, lookup endpoints, editor destinations and record access passed. No migrations or data reset. Backup /opt/aurora-auth/backups/20261022-13. Archive SHA256 12790eaca2fec144e072a63521f60b8e2787b6a1a139d8dca97f4eae20f524d4. Evidence: artifacts/publish-20261022-13.log.

2026-10-23 release 20261023-1: Accept US interpolated address matches scoring at least 90 only when house number, normalized street, city, state and ZIP agree; reject conflicting locations and street/area-only results. Includes linked Manifest Number column and removal of manifest assignment warning. API/client Release builds and address regression checks passed. Live Hurricane lookup returned coordinates without saving records; auth, planning, manifest association SQL and direct manifest access passed. No migrations or data reset. Backup /opt/aurora-auth/backups/20261023-1. Archive SHA256 ae8ac9586e65e42ee48f105105d63bda549277dbc0e466d2a2deefe77dc37252. Evidence: artifacts/publish-20261023-1.log.

2026-10-23 release 20261023-2: Drivers menu and native Nova driver CRUD; assigned-driver planning with shifts, employment/license dates, manifest reservations and explicit equipment-only versus driver-included cost basis. Separate hourly labor is added once; planned drivers persist to manifests and dispatch. API/client Release publishes passed. Local driver, replanning, routing and workspace checks passed. Live readiness, login/PKCE, Orders/planning reads, manifest dispatch, lookup endpoints, driver directory/native fields, editor route and styles passed. No migrations, seeding or data reset. Backup /opt/aurora-auth/backups/20261023-2; prior release 20261023-1. Archive SHA256 34bb8e2633b9289329f5fedb7b202bbb8aa9f7a9359b1c6d38637107527dff2e. Evidence: artifacts/publish-20261023-2.log.

2026-10-23 release 20261023-3: Moves driver/truck selection and per-run hours into optimization; driver editor keeps labor cost and optional default hours. Native truck assignment is preserved but no longer required or edited. Server validates choices, recalculates labor, and exports the completed session's driver snapshot and per-plan hours. Removes terminal in-use deletion guards while preserving referenced records and revision/role checks. API/client Release publishes passed; 27 driver, 53 planning session, 38 manifest replanning, 25 workspace and 24 deletion checks passed locally. Live readiness/auth, Orders, manifest details, lookup endpoints, native driver directory and optimization editor styles passed. No live deletion was performed by verification. No migrations or data reset. Backup /opt/aurora-auth/backups/20261023-3; prior release 20261023-2. Archive SHA256 d85cf4edac621e578a6d4cb00824ed6dda7dc1762c23b7c7827414eb74e3d1f5. Evidence: artifacts/publish-20261023-3.log.

2026-10-23 release 20261023-4: Removes default hours and explanatory text from the driver editor and list; retains hourly labor cost. Optimization initializes driver hours from the route schedule. Driver saves no longer validate or write Nova's default shift fields; existing values are preserved. API/client Release publishes and 27 driver checks passed. Live readiness/auth, Orders, manifest details, lookup endpoints, driver directory and planning assets passed. No migrations or data reset. Backup /opt/aurora-auth/backups/20261023-4; prior release 20261023-3. Archive SHA256 9aa2b7aea65f8367468ab5904f8692686e4a49d0dbf090d4009ed1a67a121394. Evidence: artifacts/publish-20261023-4.log.

2026-10-23 release 20261023-5: Removes equipment cost basis selection; optimization always adds driver labor to equipment costs, including older drafts. Adds Unknown Terminal filtering, removes unknown-terminal sync warning banners and uses plain hyphens in terminal labels. API/client Release publishes and 27 driver checks passed. Live readiness/auth, Orders planning loads, manifests, lookup endpoints, driver directory and client assets passed; both containers verified running release 20261023-5. No migrations or data reset. Backup /opt/aurora-auth/backups/20261023-5; prior release 20261023-4. Archive SHA256 f59139fa0c53aabee921d39d40413eaf4a30869610dcc9056698de12882c68ab. Evidence: artifacts/publish-20261023-5.log.

2026-10-23 release 20261023-6: Equipment routing-profile dropdown for US pickup, delivery/box truck and five-axle tractor/trailer. Dimension fields hidden while existing Nova values are preserved; capacity and costs remain editable. Profiles persist in the tenant-isolated equipment settings table keyed by source and native equipment ID, with revision checks, and are applied during server fleet reconstruction. Explicitly configured equipment represents a complete rig; capacity/cost eligibility remains required. API/client Release publishes and 249 routing checks passed. Three synthetic PTV runs accepted all listed profiles. Live profile save/reload (preserving the current profile), invalid/stale rejection, native record preservation, fleet mapping, auth and existing smoke checks passed. No schema migrations or data reset. Backup /opt/aurora-auth/backups/20261023-6; prior release 20261023-5. Archive SHA256 2c7218dd74a26f325b1267a0a82062df207791553a397fe48626f4b90cf03aea. Evidence: artifacts/equipment-profile-routing-checks.log, artifacts/equipment-profile-provider-checks.log, artifacts/publish-20261023-6.log.

2026-10-23 release 20261023-7: Equipment editor moves Routing beside Capacity, hides Schedule & breaks, shortens the delete caption to Delete, and uses Per Hour, Per Mile and Cubic Capacity labels. Existing native dimensions/schedule values preserved. API/client Release publishes passed; live label assertions, equipment profile persistence and existing authenticated smoke checks passed. Backup /opt/aurora-auth/backups/20261023-7; prior release 20261023-6. No migrations or data reset. Archive SHA256 2a993219fa47a7fc9f4071efab7d7f98aa1f090f9bbd48a599e1e2a0d1d82b78. Evidence: artifacts/publish-20261023-7.log.

2026-10-26 release 20261026-2: Published tenant-owned Aurora workspace storage, independent background Nova synchronization, and the route timeline. Tenant 11111111-1111-1111-1111-111111111111 is assigned to WayTest (catalog 01a08138-7cb9-7a10-8bd8-450a7bfd755e). Synchronization runs every 30 seconds with transactional change capture, outbox receipts, retries, source isolation, and conflict review. Applied additive migrations 0030-0039 and installed WayTest capture schema; no data reset or development seed. Initial live import completed at 2026-10-26 15:48:21 UTC; subsequent exchanges verified with zero pending writes or conflicts. The live history window keeps open work and recent closed work rather than importing all historical records.

Validation: 115 synchronization checks, 20 native capture checks, 9 timeline checks, all 8,343 read projections and repeat-import checks on a disposable clone, and 12 native create/edit/readback checks rolled back without committed business changes. Published login/PKCE, local orders/masters/equipment/manifests, planning configuration, legacy native endpoint blocking, and timeline stylesheet checks passed. Browser sign-in, WayTest sync status, MHT order selection, and starting/canceling a temporary local planning draft passed. Existing user drafts were retained. Corrected a nested-URL login script path and Npgsql array materialization in local routing settings discovered during publication.

Backup: /opt/aurora-auth/backups/20261026-2 (database, environment, rollback images). Final package SHA256: 7bf8c8e5d2cbe987a7ac61880f7c3af619431e93b484b721b283e391ac555fe6. API image sha256:e5bbcc7d94cebbf7247909c789d09bdedba2540872249788f51d82837c063952; web image sha256:30652ccc20e16bc874e08f8a7254da65569ed0da1ee3ab851a9ed5e5ff396418. Evidence: artifacts/publish-final-result.txt, artifacts/sync-final-import-7-result.txt, artifacts/sync-push-final-result.txt, artifacts/sync-core-final-result.txt. The first deployment verification rolled back on the routing-settings error; the corrected release subsequently passed and is active.

2026-10-27 client release 20261027-1: Restored the Orders terminal column and multi-selection (including select-all and Shift-click ranges). Hid the Freight workflow entry point while preserving its editor. Orders grid now fills the available viewport and uses one horizontally scrollable filter/action toolbar. Removed Synchronization from sidebar navigation while retaining its page/URL. Release publish and live authenticated checks passed; browser confirmed the full-height grid, toolbar, terminal values and hidden controls. Client archive SHA256 17636913243325daf79b7045d5e0e6e44556d0af2f2db123f3216a5fa60925c9. Backup /opt/aurora-auth/backups/20261027-1. API container retained without migrations or data changes.

2026-10-27 client release 20261027-2: Optimize routes now transfers the exact selected source/order keys and terminal to optimization setup, rechecks current availability and preselects only those orders. Prevents empty, mixed-terminal/source and already-assigned selections. Focused selection handoff checks and Release publish passed. Live readiness, login/PKCE, local records and retained synchronization route checks passed. Client archive SHA256 5185ffbe5612da761228446507f575f969bc688130d1518b1c676fe4c0c05ded. Backup /opt/aurora-auth/backups/20261027-2. API container retained without migrations or data changes.
`nBrowser verification for 20261027-2 passed: filter MHT, Shift-select exactly ten orders, click Optimize routes, verify MHT remains selected and Start (10) is enabled with exactly the ten selected rows. No planning draft was created. Screenshot: artifacts/orders-selection-handoff-20261027.png.

2026-10-27 client release 20261027-3: Optimize routes on Orders now opens a compact Route schedule dialog with Ship date, Depart, Return and optional Return date. Continue creates a draft from the captured exact order selection and opens the planner directly; Cancel retains the selection. Schedule validation and structured server errors remain inside the popup. The separate optimization selection page is bypassed in this flow. Release publish and deployment readiness/authenticated read checks passed. No browser testing or browser control was performed, per user instruction. API container and operational data retained. Archive SHA256 ba8dc940b3539bfd0abec9e45a7d95ca8953f5349db2b48ea9be38445a83767d. Rollback backup /opt/aurora-auth/backups/20261027-3.

## Client republish 2026-10-27 — 20261027-4
- Published order editor section shading, right-aligned Save, open-plan Resume/Cancel and release controls, and retired duplicate optimization setup page (redirect to Orders).
- Release build and OrderPlanningSelectionChecks passed; authenticated deployment checks passed for login, local records, draft listing, styles and retained synchronization route. No browser testing.
- API container remains 20261026-2; application data preserved. Backup: /opt/aurora-auth/backups/20261027-4.
- Archive SHA256: b2451c7334ece4aa6037a60e0f9c0be9cbd4c58a88557ce5954e733640a609cd.

## Client and API republish 2026-10-27 — 20261027-5
- Published current-day Orders/Manifests filters, Enter-to-search, tab-title encoding fix, automatic address lookup on Save with coordinates in party sections, default equipment drivers, initial truck popup and removal of route explanatory paragraphs.
- Release builds passed (API retained existing package vulnerability warnings); 31 driver checks passed before packaging. Public readiness and authenticated login, local data, styles, synchronization and default-driver contract checks passed after deployment. No browser testing.
- Both client/API updated. No migrations or data resets. Database dump verified and rollback images retained at /opt/aurora-auth/backups/20261027-5.
- Archive SHA256: bee02cda29acc4efc92ae9c17477854a442b9310b9a67f925a07b1c24c6a9633.

## Aurora MVP publication 2026-10-28 - 20261028-1

Published API/client and Aurora migrations 0040-0044 after a restored-database rehearsal and verified offline backup. Native-owned freight MVP has no Delphi dependency. Local Delphi edits reversed. New Lykes/FreightOps synchronization and Nova reverse-write flags remain disabled; existing WayTest sync retained and healthy. No FreightOps or Nova deployment, Firebird migration, seeding or data reset.

273 Nova checks, 130 endpoint/database/host checks, 14 tenant-settings checks and order-selection regression suite passed. Release publishes and final authenticated live checks passed. One held outbox record was confirmed in the pre-release backup. Recovery backup: /opt/aurora-auth/backups/20261028-1-attempt2. Old binaries alone are not a safe rollback after tenant-settings schema conversion.

See docs/LYKES-MAPPING-V2-2026-10-27.md, MVP revision and Publication result sections, for exact scope, archive/image hashes, rehearsal/verification corrections, evidence and the FreightOps test-server handoff. Live verification log: artifacts/mvp-live-verification.log. FreightOps publication remains Bryan's step.

## Lykes Test workspace reset - 2026-10-29

At Bryan's explicit request, replaced the existing Aurora Demo Co/WayTest workspace with an empty Lykes Test workspace under the same tenant ID, retaining application logins, memberships, roles and product access. No new backup was made, as requested. Stopped the API, transactionally deleted tenant-scoped orders, manifests, drafts, fleet/masters, routing settings and sync history, cleared stored integration settings with a new Nova reference, removed two tenant-owned saved planning sessions, and restarted the API. Native Nova and FreightOps databases were not modified. Existing prior backups were not deleted.

Synchronization__Enabled=false and all FreightOps/Lykes write/enable switches remain false; NativeReadOnly=true. User must reopen Settings and enter the Lykes Nova and FreightOps connection credentials; the previously rejected form submission did not persist them. Authenticated login/PKCE, Lykes Test company name, empty orders/manifests/drafts, cleared connections and disabled sync verified. Evidence: artifacts/reset-lykes-workspace-result.log and artifacts/verify-lykes-reset-result.log. Do not run the old WayTest verification or enable old connection settings after this reset.

2026-10-30: Published web-only release 20261030-1 for Settings Connections 2x2 grid (Nova/FreightOps, DispatchTrack/ZEnventory), responsive single column below 600px. Release build passed. Public HTML stylesheet version and grid CSS verified; API readiness 200. API container unchanged on 20261028-1. compose.yml web image now uses WEB_RELEASE_TAG with RELEASE_TAG fallback; .env WEB_RELEASE_TAG=20261030-1. No database or synchronization changes. Web image sha256:08853219e1372c6fe763c32a70386e9bfecf78edbb50a654ca528f3e800b07c4.

2026-10-30 Lykes pilot: Aurora API 20261030-5; web remains 20261030-1. Lykes-only FreightOps image freightops-api-lykes:aurora-planning-20261030-v2. Nova -> Aurora -> FreightOps PRO 1862 verified; native FreightOps shipper edit returned to Aurora. Saved Monday manifest MAN-02221E40 with DEMO-TPA-01 / Demo Driver. Nova reverse business writes disabled and verified zero receipts; rating unchanged. Cooke unchanged. Full scope, setup data, image hashes, validation and walkthrough: docs/LYKES-MONDAY-DEMO-2026-11-02.md.

## 2026-10-30 app navigation restoration (20261030-6)
Published Aurora API and web 20261030-6. Restored licensed application header buttons. TenantSettingsService.Save updates the tenant's existing FreightOps launcher URL in the same transaction as Connections; entitlements remain unchanged. Applied the existing URL alignment only to Lykes. General backfill script 0045 is included for future migration runs, not applied globally here. Local API/client Release publishes and 17 tenant settings checks passed. Live authenticated product URL equals Lykes Connections URL; sync remains settled, Nova business writes off, saved Monday route intact. No FreightOps/Cooke deployment touched.

## Aurora republished 2026-10-30 — release 20261030-7

API and web published successfully. Local Release builds and 249 routing checks had passed. Archive SHA256: 1E3E254DB461086046939A947D7DDDE500B928D709C30885B10DC5F31995B8F8. Live readiness, administrator login and PKCE passed. Product API returns Aurora, FreightOps, Integration Hub and Nova; auroratms is absent. Lykes Nova has access but a null URL, intentionally leaving browser launch unavailable until certificate activation and embedding verification. FreightOps URL matches Connections. Database container was not recreated.

Live Monday verification passed: PRO 1862 pilot, FreightOps queue settled, saved MAN-02221E40 route intact, Nova business writes disabled. No GoGlobal service restart or active certificate change; no Cooke configuration changes.

## Lykes manifest publishing 2026-10-30
Aurora API/web 20261030-9 adds explicit replay-safe FreightOps delivery-plan publishing and locks local edits after handoff. Both target and host feature switches are opt-in, enabled only for Lykes. Published MAN-02221E40 / PRO 1862 to native FreightOps Planning with truck, date, driver Tag reference, native membership and piece queue. Nova business writes remain disabled.

Lykes API and daemon updated to aurora-manifests-20261030-v2; the daemon receives the additive planning event type and tolerant legacy event-actor reads. Native status subscriptions caught up without editing stored historical events. Cooke containers unchanged; GoGlobal untouched.

Validation: 158 endpoint/database checks, 249 routing checks, 49 adapter HTTP checks, Release builds. Final Aurora archive SHA256 9993cb6f79ccb6d2da486c835fef31c891ab4f1ffd3985ae9be5993adc82ea06. See docs/LYKES-MONDAY-DEMO-2026-11-02.md for boundaries and deployment evidence. This is a planning handoff, not reverse manifest lifecycle sync.

## 2026-11-02 — Lykes demo readiness

Published Aurora API `20261102-1` (Docker image `sha256:699a253e0203daff826df994cb200354a5c546b4f976ec6edd019b8dc577b264`), web unchanged. Change excludes FreightOps-submitted manifests from automatic inclusion in fresh planning drafts; existing write/commit safeguards remain. No staging reset was run. Expanded Lykes import allowlist to native PB_ID 1862 and 1874–1885; created explicitly synthetic test shipments and demo fleet. Nova NativeReadOnly=true/WritesEnabled=false remain pending Steve's Delphi stable-identity implementation and mapping reconciliation.

Applied nullable `OPS_STOP_REC.OPS_ETA_TIME TIMESTAMP` only to verified Lykes TEST Firebird database; no population rule, rating change or Delphi change. 285 Nova checks, 249 routing checks, Release publish, live eight-order/two-route publication and four-order/unbooked-truck planning rehearsal passed. Native FreightOps manifests MAN-119444A3 and MAN-F013760C verified; no API/daemon errors or restarts observed there. Final FreightOps queues empty and PRO 99002009 reverse edit received by Aurora. Detailed instructions and boundaries are in docs/LYKES-MONDAY-DEMO-2026-11-02.md; Steve email draft and requirements in docs/LYKES-STEVE-DELPHI-HANDOFF-2026-11-02.md.

## Aurora republished 2026-11-02 — release 20261102-2

Published API and client from commit fe4dd6c56462d9c55bbc4736cbee7506860966ab, including Orders > Import orders beside Optimize routes and the Integration Hub conversion preview endpoint. No migrations, resets, sync-setting changes or other product deployments. Existing API environment matched exactly; database and FreightOps containers were not recreated.

Release builds and document-import checks passed. Live readiness, administrator login/PKCE, orders, terminals, drafts, synchronization status, authenticated import empty-file validation and client release marker passed. No paid conversion or orders were created during verification. Tenant document conversion URL/credential environment settings are not configured; provision these before using conversion. Hub was not deployed.

Backup: /opt/aurora-auth/backups/20261102-2. Archive SHA256: ab158998ed31b7369e60f8683c923fea743b87fbe2afa136a47cae4f190b650a. API image: sha256:02ad3230b65568de8f1039222c05daa12893debd660a6af01edc109300c0124b. Web image: sha256:51f0cc0ee9a80c39318deb3264d12d77c3cd5a9dfdf6a6e9f1a7aef9531f6f3b. Evidence: artifacts/publish-import-20261102-2.log.

## Aurora republished 2026-11-02 — release 20261102-3

Published Tag-to-PRO normalization and duplicate import checks. Alpha Tag prefix selects the row's terminal; numeric portion becomes canonical Nova PRO. BOL is extracted/reviewed separately from shipment Reference, HAWB and MAWB, with no fallback from another identifier. Non-BOL references are retained as labeled notes. Per-row terminal matching is required. Duplicate preflight queries all tenant Aurora sources (including deleted records) and the saved Nova source directly; import rechecks before save, and Aurora create repeats its local check under the existing transaction lock. Existing sync code/settings are unchanged.

40 import checks passed, including seven manually reviewed sample identities, blank-BOL isolation, invalid Tag rejection and a disposable PostgreSQL duplicate/tenant-isolation test. API/client Release builds passed. Live readiness, login/PKCE, workspace reads, import validation, release marker and Aurora/Nova duplicate preflight passed. Sample PROs were checked read-only; no sample orders or paid AI conversions were created. Tenant document-conversion environment settings remain unconfigured. No migrations, resets or other product deployments; database/FreightOps containers and API settings retained.

Backup: /opt/aurora-auth/backups/20261102-3. Archive SHA256: b0145ab94ea42e1783b4fdd647897861ba6488190b5f8c98c5181677f5b184f6. API image: sha256:426ad941ee97fa0d2342ab1a9edb3901aa3b8a4309ef263fcc74d11952701257. Web image: sha256:8f1221bfaeb640debe04e953b0ccbd12a31a41bf5d7105fa88a4ff010aa96bdf. Evidence: artifacts/publish-import-20261102-3.log.


### 2026-11-02 - Aurora 20261102-4

Published bidirectional FreightOps planning updates and removed the submitted-plan lock. Lykes FreightOps API/daemon now use aurora-bidirectional-20261102-v2, including a truck-event replay correction. Final live FreightOps queue has no pending/held records or conflicts. Nova native writes remain disabled. Validation: 170 endpoint checks, 50 adapter checks, 239 core tests. Added 50 requested synthetic TPA optimization orders, 99003001-99003050 (search TEST50). Details and release limitations are in docs/LYKES-MAPPING-V2-2026-10-27.md.


### 2026-11-02 - Aurora 20261102-5

Published freight-line description mapping with Lykes FreightOps image aurora-line-descriptions-20261102. Descriptions now populate native SKU details and return to Aurora; eligible description changes preserve line identity and barcodes. 177 endpoint/database checks and 285 Nova checks passed. Existing native write/handling guards remain unchanged; no historical mass-backfill. See docs/LYKES-MAPPING-V2-2026-10-27.md.

## Nova freight defaults - 2026-11-02

Published Aurora 20261102-6. Backup: /opt/aurora-auth/backups/20261102-6. Authenticated verification passed for orders, imports validation, duplicate preflight, release marker, all 50 test orders and unlocked planning manifests. Live FreightOps sync: no error, pending 0, working 0, held 0, field conflicts 0. Nova outbound pending 216 with writes disabled. Integration Hub conversion tenant environment settings remain absent; document conversion was not exercised by this release.

## 2026-11-03 — Daily dispatch board, release 20261103-10

Deployed the daily dispatch board at /aurora/dispatch for local and native workspaces: date/terminal/search filters, unassigned/planned/dispatched work, late/blocker alerts, resources, route maps, and existing editor links. Live read-only Nova validation against Lykes Test found and fixed VOID-label classification and unflagged unknown statuses. All 30 dispatch-board regression checks passed. Full loader: 766 native orders, 480 resources, 34 reads, 1.35 seconds, zero source warnings. Five open manifests verified. No business data was changed during validation.

API and client Release publishes completed (existing Pkcs 9.0.0 NuGet advisories remain). API replaced 20261103-9; web replaced 20261103-4. Runtime environment matched the previous API exactly; database and FreightOps containers were not recreated. No migrations, seeding, resets, or sync-setting changes. First attempt rolled back after an external TLS connection reset during authenticated verification; the same release was retried successfully. Authenticated order pagination, dispatch documents, stop completion fields, planning reservations, resources, page assets and release marker passed in 4.95 seconds.

Backup/rollback: /opt/aurora-auth/backups/20261103-10. Archive SHA256: ceb2b345fc0a681e298293eff2c6182483be0d48cd1734333510ace080439c1f. API image: sha256:55f00a903ecf3f60303f89e7711ba89998a36e4dfb8c258098ee516c8a155694. Web image: sha256:f6ec6b4300d48634a1fbbd841c379c0f97023509f445c1a1a389e8e07f37d733. Evidence: artifacts/dispatch-live-validation.log and artifacts/publish-dispatch-20261103-10-retry.log. The read-only verifier was subsequently given bounded transport retries; application binaries and archive are unchanged.

## 2026-11-03 — Dispatch copy removal, web release 20261103-11

Removed the Operations / Daily Dispatch eyebrow, Keep the day moving headline, and promotional description at user request. Retained the workspace page title and compact snapshot/Refresh controls. Client Release publish passed; deployed page, versioned stylesheet, release marker and readiness checks passed. Web-only deployment; API container remained unchanged at 20261103-10. Backup: /opt/aurora-auth/backups/20261103-11. Archive SHA256: 19ec5ba4c10969b37227486c2bdf5acca03a4361bca96c81b49d327003269210. Evidence: artifacts/publish-dispatch-20261103-11.log.

## 2026-11-03 — Dispatch metrics and Orders toolbar, web releases 20261103-12/13

Dispatch metrics now stack the number above the bold label and omit descriptions. Explicit column layout overrides shared button styling. Live DOM verification confirmed all six metric cards are 98px high, labels use font-weight 700, numbers are above labels, and there are no description elements.

Orders toolbar buttons now share the 30px control height. The MudMenu Add activator uses matching geometry and its wrapper aligns to the toolbar bottom instead of centering beside field labels. Client Release publishes and deployed release-marker, stylesheet, page, readiness and unchanged-API checks passed. Final web release: 20261103-13; API remains unchanged. Backups: /opt/aurora-auth/backups/20261103-12 and /opt/aurora-auth/backups/20261103-13. Final archive SHA256: b7e993b46af7f3af52ea87f0fad7b3076f6ba3cf53d578c69e40f2e59d4b000d. Evidence: artifacts/publish-toolbar-20261103-13.log.
Live browser verification for 20261103-13: Search / Refresh, Add, Plan routes, and Update consignee each measured exactly 30px high with identical top and bottom coordinates. Original user tab and its selected orders were preserved.

## 2026-11-03 — Dispatch navigation label, web release 20261103-14

Changed the sidebar label, tooltip and accessible name from Daily dispatch to Dispatch. Route and page heading are unchanged. Client Release publish and deployed release-marker, stylesheet, dispatch page and readiness checks passed; API container unchanged. Backup: /opt/aurora-auth/backups/20261103-14. Archive SHA256: c9024032bf142c6210aff584a411b3535a00cce640b658669b5e4debb436adaa. Evidence: artifacts/publish-dispatch-label-20261103-14.log.

## 2026-11-03 invitation email configuration

Deployed API-only release `20261103-invitation-2` with sender corrected to `alerts@aurorasoftware.com` and the user-provided Resend key installed in the private API environment. Public invitation origin is `https://user.aurorasoftware.com`. Only the API DLL/PDB differed from the prior published API; website and other service containers were preserved. No migrations or data resets.

All 31 invitation/password checks passed. Live readiness, administrator login/PKCE, retained demo orders, planning, fleet, drivers, unchanged website marker, and runtime environment checks passed. No invitation was sent during deployment verification. Backup: `/opt/aurora-auth/backups/20261103-invitation-2`. First attempt automatically rolled back because the prior verifier hardcoded an older website marker; the successful attempt checked the captured current marker instead.

## 2026-11-03 invitation success text

Deployed client release `20261103-invitation-message-1`. Removed the Resend success message from Send invitation and from the user-created confirmation; invitation failures remain visible. Client Release publish and deployed marker, stylesheet, page, readiness, and unchanged-API checks passed. Backup: `/opt/aurora-auth/backups/20261103-invitation-message-1`.

Republished the current client at user request as 20261103-invitation-message-2. Release publish and live release marker, page, stylesheet, readiness, and unchanged-API checks passed. Backup: /opt/aurora-auth/backups/20261103-invitation-message-2.

## 2026-11-03 global optimization driver cost

Published API and client release `20261103-global-driver-cost-1`. Optimization selects trucks without driver choices or driver shifts. Settings > General now saves an administrator-editable company-wide Driver cost per hour, initially zero. Every workspace optimization restores saved equipment rates and adds the current global labor rate once; browser overrides and legacy driver assignments cannot alter it. Newly created local manifests leave the driver unassigned for dispatch. Truck availability, route hours, and multi-day rest rules remain in use.

Migration 0049 adds the rate and a member-only read function that does not expose connection credentials. Local migration and 27 settings persistence/access checks passed; 35 workspace checks passed, including optimizer payload cost, repeat runs, zero cost, validation, and legacy assignment removal. API/client Release publishes passed. Live readiness, administrator login/PKCE, settings, planning reads, 500 existing demo orders, fleet, drivers, release marker, unchanged runtime secrets, and other-service container checks passed. No paid optimization or operational records were created. Backup: `/opt/aurora-auth/backups/20261103-global-driver-cost-1`. Package SHA256: `e4c774e3716aa112b76dbb485ffb0db078d2c7809f4b6dda1f2f487edfe7cebf`.

## 2026-11-03 Settings save and cost formatting

Published client `20261103-settings-save-2`. Successful settings saves close the dialog; failed saves retain the form and error. The footer displays Cancel followed by Save. Driver cost per hour formats with two decimal places using MoneyInput. Client Release publish and deployed marker, stylesheet, page, readiness, and unchanged-API checks passed. Backups: `/opt/aurora-auth/backups/20261103-settings-save-1` and `/opt/aurora-auth/backups/20261103-settings-save-2`.

## 2026-11-03 — Order import file and pasted text, web release 20261103-order-import-1

Published Add > Import with Upload a file and Paste text choices, a large text area, input size validation and the existing extracted-order review flow. Pasted content uses the existing document conversion endpoint as UTF-8 text. Client Release publish and deployed release marker, page, import stylesheet and readiness checks passed; API container unchanged. Live AI conversion was not exercised. Backup: `/opt/aurora-auth/backups/20261103-order-import-1`. Archive SHA256: `22fa715962dc5f0f2eaaa38aff406727a79a139aad92a8c4b125bd6d010ae45c`. Evidence: `artifacts/publish-order-import-20261103.log`.

## 2026-11-03 — Import help copy removal, web release 20261103-order-import-2

Removed introductory, file/text guidance, pre-create help and review instructions from the import dialog, along with the instructional text-area placeholder and BOL helper caption. Retained controls, validation and progress/results. Client Release publish and live release marker, stylesheet, page and readiness checks passed. API unchanged. Backup: `/opt/aurora-auth/backups/20261103-order-import-2`. Evidence: `artifacts/publish-order-import-20261103-2.log`.


## 2026-11-04 — Native terminal receipt, API release 20261104-terminal-receipt-1

Republished the Aurora API with native current-terminal routing eligibility, terminal DOCK receipt recognition, completed manifest history handling, and stale-terminal export validation. Website remains 20261103-order-import-2. No migrations, resets, sync-setting changes, or other service deployments.

Release publish passed with the existing Pkcs 9.0.0 advisories. Public readiness, administrator login/PKCE, authenticated native Orders (all, Ready to Route, TPA/MCO ready filters, unknown-terminal filter), settings, drafts, fleet and drivers passed. Runtime API environment matched exactly. Website, database and FreightOps containers were retained. Verification did not run a refresh or create operational records; the complete Nova receipt-to-next-manifest UI rehearsal remains outstanding.

Backup: `/opt/aurora-auth/backups/20261104-terminal-receipt-1`. Package SHA256: `e0a1e7a51a29358c5bf09f632bc2bac7aebe95e5db2081fe5ef873da6b133297`. API image: `sha256:7cd8cb49ccc5e62a952cc081f691fad3c2b6e0dbf4cd620f63a4f6cae0585058`. Evidence: `artifacts/publish-terminal-receipt-20261104.log`.

## 2026-11-04 — Optimization and Data Entry navigation

Published web release `20261104-data-entry-1`. Renamed the Aurora product tab to Optimization, added Data Entry with the existing document conversion flow for local and native workspaces, and removed import from the Orders Add control. Client Release publish and live release marker, Data Entry page, stylesheet, readiness, and unchanged-API checks passed. Live document conversion was not exercised.

Backup: `/opt/aurora-auth/backups/20261104-data-entry-1`. Archive SHA256: `3e0a4fbfa3c4a5fab7efca9e16c79f1a6de8e39a269b72ed245206f2bdd96905`. Evidence: `artifacts/publish-data-entry-20261104.log`.

## 2026-11-04 — Tenant editor and direct Data Entry, release 20261104-tenant-editor-1

Published API and client with Admin > Edit Tenant, editable company name, independent tenant-wide top-menu visibility switches, existing driver cost and integration settings. Data Entry now exposes upload/paste and Convert directly; extracted orders open in a review dialog with shaded reference, address, and shipment groups.

Applied additive migration 0050 after a database backup. Release builds passed (existing Pkcs advisories remain); all 38 local tenant settings checks passed before publication. Live readiness, administrator login/PKCE, tenant settings/navigation consistency, company name, native order reads and terminal filters, planning, fleet/drivers, Data Entry page, grouped styles, and release marker passed. Runtime secrets and database/FreightOps containers were preserved. No operational records were created and no live conversion was run.

Backup: `/opt/aurora-auth/backups/20261104-tenant-editor-1`. Archive SHA256: `abddd10e98277a70caf3998745ee1f72d8ad84cce264425c8113bbbdd7220e71`. Evidence: `artifacts/publish-tenant-editor-20261104.log`.

## 2026-11-04 — Import duplicate feedback, web release 20261104-import-duplicate-1

Published the shortened duplicate message (PRO [number] already exists), spacing below its alert, and disabled Create while a duplicate remains in the import. Client Release publish and live marker, Data Entry page, duplicate-alert stylesheet and readiness checks passed; API unchanged. No migration or operational data changes.

Backup: `/opt/aurora-auth/backups/20261104-import-duplicate-1`. Archive SHA256: `6fcecfd1ac090ee042b987ef20e5a06762efbf563c594b2cf26177bfa499d784`. Evidence: `artifacts/publish-import-duplicate-20261104.log`.

## 2026-11-04 — Data Entry redesign and import history, release 20261104-data-entry-redesign-1

Published API/client with side-by-side upload and paste panels, a styled file drop zone, selected-source highlighting, file details/remove, text count/clear, conversion action bar/progress, responsive layout, and tenant-scoped Recent imports. Migration 0051 stores filename, conversion status, extracted counts, and distinct successfully created PROs; no source documents or pasted text are retained by history. History starts with this release.

A database backup preceded the additive migration. Release builds passed with existing Pkcs advisories. Prior local validation passed 48 settings/history checks and 59 import checks; desktop/mobile and synthetic paste-to-review UI checks passed. Live readiness, administrator login/PKCE, settings/menu consistency, native Orders and terminal filters, planning, fleet/drivers, authenticated history, new stylesheet selectors and release marker passed. No live conversion or operational write was used for verification. Existing runtime secrets and database/FreightOps containers were preserved.

Backup: `/opt/aurora-auth/backups/20261104-data-entry-redesign-1`. Archive SHA256: `e93c64e53f33b35105a2e7637379f6ba8d60f75403c105314f1ab3279e1d2ed4`. Evidence: `artifacts/publish-data-entry-redesign-20261104.log`.

## 2026-11-04 — Import independent of optimization, release 20261104-independent-import-1

Published API/client with import planning-status requirements removed, an editable Nova status using the ordinary new-order default, optional shipper/consignee and terminal fields, selected-terminal precedence over Tag prefix, and no automatic native address lookup. Import bypasses general routing/business validation; duplicate, tenancy, native field/storage and reference checks remain. Normal manual-entry validation is unchanged.

Release publishes passed with existing Pkcs advisories. Prior 54 import checks passed. Live readiness, administrator login/PKCE, native order reads/terminal filters, settings, planning, fleet/drivers, history, published assets/release marker, and native status choices passed. Pickup, delivery, and mixed compatibility preflight requests all returned 204 without consulting optimization mappings. No live order creation or paid conversion was used for verification. No migration or operational resets; runtime settings and other services preserved.

Backup: `/opt/aurora-auth/backups/20261104-independent-import-1`. Archive SHA256: `167528267532c7156948dc9569e3d058f746bdc4dcb638a202b7e8a95db47db9`. Evidence: `artifacts/publish-independent-import-20261104.log`.

## 2026-11-04 - Recent import order editor, release 20261104-import-order-editor-2

Published API/client with Order number links in Recent imports. Links open current saved values in the grouped import form; Save changes updates the existing order. Tenant import membership and revision checks protect access and concurrent changes. Native fields outside the form and unedited appointment windows are preserved. Editing remains independent of optimization.

Validation: 64 import checks and 52 tenant/history checks passed. Browser preview verified opening, keyboard editing, saving and reopening. Release builds passed with existing dependency advisories. Live readiness, login/PKCE, native reads, settings/menu, assets, import history, opening an existing native imported order, unrelated-PRO rejection and unauthenticated rejection passed. No live orders were modified for verification; no migration or operational resets. Settings and other services preserved.

The first candidate automatically rolled back after the live editor check exposed a missing tenant connection path in middleware. Release 2 corrected it and passed.

Backup: /opt/aurora-auth/backups/20261104-import-order-editor-2. Archive SHA256: 6b59121853c63e06be97ac0a9f877b8006f4399da9bfc5ecda2dfeb99727f1b4. Evidence: artifacts/publish-import-order-editor-2-20261104.log. Preview: artifacts/import-order-editor-preview.png.

## 2026-11-04 - Crown report import, final release 20261104-crown-import-4

Published deterministic Crown XLSX conversion, full-Tag grouping, large-batch review navigation, chunked duplicate checks, and Nova account address reuse. Migration 0052 raises import-history capacity to 5,000. Original full Tags are preserved in notes when distinct terminal-prefixed Tags share a numeric suffix; the approved twelve shipments use their unique Crown Ship IDs. Repeated driver/charge rows do not multiply shipment pieces or weight. Full customer names remain in notes when the native name field must be shortened. ZIP+4 retains nine digits, omitting hyphens or spaces for Nova storage.

User-authorized import target was verified through authenticated settings as Lykes Test, Nova host 172.23.0.5, database D:\db\lykestest\operations.fdb. The Desktop workbook test2report Crown.xlsx has 2,128 source rows and 1,797 shipments. Created 1,796 orders, preserving preexisting PRO 3696808. All 1,797 PROs verified present. New freight totals: 7,834 pieces and 1,223,632 lb; including the existing order, source totals reconcile to 7,843 pieces and 1,223,789 lb. Read back 67 representative saved orders including all twelve alternate IDs, eight RSW orders, and matched Nova addresses. Forty-five new orders reused a Nova master address.

The missing RSW terminal reference was created with name/code RSW and no invented address or coordinates. Initial null-terminal rows and overlength ZIP formats failed without creating orders, then were retried successfully. Earlier requests rejected at the edge were retried with the same authenticated API client identification. Final import has no unresolved rows. Import batch: b4ae2f2a-1297-41d9-b86a-a51f2ee0c69c. The history created count is 1,796 of 1,797 because one order already existed.

Validation: 80 import/address/Crown checks passed, including every shipment's native text-field lengths. Client build passed; API release publish passed with existing dependency advisories. Published readiness, login/PKCE, native reads, history, editor, assets and access checks passed. Backups for releases 20261104-crown-import-1 through -4 are under /opt/aurora-auth/backups/. Final archive SHA256: f2b6f7e500edc9161f9b5e2d7103228483037a8e4b48e18b995fe334beb77fa0. Evidence: artifacts/crown-final-verification.log, artifacts/crown-tests.log and artifacts/publish-crown-import-4-20261104.log. Private per-order ledger and verification summary are under /opt/aurora-auth/private/crown-import-results.jsonl and crown-import-verification.json. No existing orders were overwritten.

## 2026-11-04 - Danielle TIFF conversion corrections, release 20261104-document-fields-1

Published API/client corrections for independently printed terminal codes, local service dates and appointment windows, filename-derived Lykes PROs, and deterministic freight totals/cube. The user confirmed that single-order LYKES<digits>B_<page> filenames supply the Lykes PRO when no own Tag is printed, including the tester-added nopro prefix. Printed Lykes Tags take precedence; other carriers' identifiers remain in notes. The user also approved inches for unlabeled Pegasus dimensions. Other forms do not inherit that assumption.

Validated both original Desktop/New folder (2) TIFFs through the live authenticated conversion endpoint. LYKES0003714580B_2.TIF returned PRO 3714580, terminal DFW, service November 2 2026 at 07:00 with window through 18:00, 1 piece, 453.2 lb, and 55.56 cubic feet. noproLYKES0003733336B_1.TIF returned PRO 3733336 and local service date November 2. Its source is only page 1 of 2; notes flag the missing second page. Visible line quantities are summed in code (28 pieces), rather than using the model's incorrect earlier sum. Carrier provider PRO remains in notes. No orders were created or overwritten by these tests.

Ninety-six regression checks passed, including actual TIFF extraction results, native save mapping, date/DST/window handling, filename precedence, cube provenance, and Crown workbook regression. Release builds and live readiness, login, native reads, import history/editor/access checks and both full TIFF conversion tests passed. Existing dependency advisories remain. No migration or operational resets.

Backup: /opt/aurora-auth/backups/20261104-document-fields-1. Archive SHA256: 0732097ca92ab3022f57c497452c1c2a0b4e7c7949fac4907aab39090d3d75ff. Evidence: artifacts/publish-document-fields-20261104.log and artifacts/danielle-regression-tests.log. Source images and conversion outputs are task artifacts/private verification files, not a new customer document-retention feature.

## 2026-11-04 — User menu arrow removal, release 20261104-user-menu-1

Published the web client with a text-only username button that opens the Logout menu. Removed the dropdown arrow; logout continues to clear the Identity cookie and OIDC browser session and return to sign-in. API remains 20261104-document-fields-1.

Release publish passed. Live client assembly, HTML and stylesheet hashes match the package; readiness, administrator login/PKCE, logout and the subsequent redirect to sign-in passed. API, database and FreightOps container IDs and server settings were preserved. No migrations or operational data changes.

Backup: /opt/aurora-auth/backups/20261104-user-menu-1. Archive SHA256: a4425dd13ef8ccc4a40f6dc9d6002dd5722c2d59b0ea53e0bafbd1d5fde3d294. Web image: sha256:b6c3c322a70ba7d7a3339e1228d0df5aec7a736321f33217aa5d683eb836ba0b. Evidence: artifacts/publish-user-menu-20261104.log.
