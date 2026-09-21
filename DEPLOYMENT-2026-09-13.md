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
