# NV1 maintenance preparation — October 30, 2026

## Confirmed host and certificate

- **nv1.aurorasoftware.com:491 / 68.169.49.102 reaches NOVA01 / 172.23.0.2:491.** Two temporary certificate-only TLS probes were correlated with two server TCP entries appearing and disappearing when only our sockets opened and closed. No application login was performed. Evidence: `artifacts/nv1-host-mapping.json`.
- NOVA02 (172.23.0.3) and AG01 (172.23.0.5) are different hosts. Do not change either for this work.
- NOVA01 publishes applications `Nova` and `TransDoc`. GO-Global uses TLS port 491 and currently selects `C:\ProgramData\Aurora Software\SSL\GoGlobal.crt`.
- The active wildcard certificate expired September 25, 2026. Public HTTPS verification still rejects it; this is expected until the maintenance change.
- NOVA01 already had a valid replacement in `Cert.pem`/`Key.pem`. It covers **nv1.aurorasoftware.com, nv2.aurorasoftware.com and nv3.aurorasoftware.com**, is issued by Let's Encrypt YR1, and expires **December 27, 2026, 17:20:45 UTC**. Private-key match, DNS-name validation, dates and full trusted chain passed. Evidence: `artifacts/nv1-certificate-readiness.json`.
- AG01's newer certificate covers AG1 only; it is not suitable for NV1.

## Staged, not activated

The correct pair is ready on **NOVA01**:

```text
C:\ProgramData\GraphOn\GO-Global\ks\nv1-maintenance-20261030\server.crt
C:\ProgramData\GraphOn\GO-Global\ks\nv1-maintenance-20261030\server.key
```

The directory permits only SYSTEM and Administrators. Staged copies match their source files. Private keys were never printed or saved on the workstation.

**No service was restarted and no user session was logged off.** Active certificate/key/configuration hashes and GGAPS PID **3496** were unchanged during staging. Only our diagnostic sockets were closed. No certificate selection, scheduled renewal job or database setting was changed. No restart is scheduled or authorized by this document.

## Prepared Aurora release

- Candidate directory: `artifacts/nova-launcher-release`.
- Package: `artifacts/nova-launcher-ready.tar.gz`, with `artifacts/nova-launcher-ready.sha256`.
- Removes Nova 2.0 launcher/routing and its development bootstrap registration. Does not delete the separate Nova 2.0 application or historical migrations.
- Adds `/workspace/nova`, labeled **Nova**, using a tenant-specific URL. Access requires an enabled Nova product and an Aurora role.
- Intended URL: `https://nv1.aurorasoftware.com:491/?useApp=false&app=NOVA`. Verify the published application parameter and browser embedding during the window.
- Windows/GoGlobal authentication remains separate. No automatic GoGlobal SSO, password forwarding or Nova business writes have been added.
- API/client Release builds and migrations build passed. **249 routing checks passed**. Existing API dependency audit warnings remain.
- `activate-nova.py` defaults to HTTPS preflight only. Tested: the expired certificate blocks it before any changes. Activation requires `--activate --host-mapping-confirmed --embedding-verified`; it never changes GoGlobal.

## Approved maintenance-window sequence

1. Confirm the window and notify affected users. Identify all tenants using NOVA01; do not assume a restart affects only Lykes. Do not alter Cooke configuration or sessions without authorization.
2. Record active sessions, service state, selected certificate path and fingerprints. Preserve the existing certificate/key and configuration with restrictive permissions.
3. Revalidate the staged pair and expiration date. On **NOVA01**, use GO-Global Admin Console, Host Options, Security to select the staged `server.crt`. Keep TLS and existing encryption settings. The matching `server.key` is alongside it. Do not overwrite the old certificate files.
4. If required, restart only NOVA01's GoGlobal service during the approved window. Do not restart Nova databases, IIS sites or other servers.
5. Verify `https://nv1.aurorasoftware.com:491/` without certificate exceptions. Test its browser client, published Nova application, authorized Lykes login and correct Nova environment. Test framing from Aurora's origin, keyboard focus and resizing. Printing and clipboard behavior require separate verification.
6. Verify the Aurora package SHA256 and extract to a new release directory on the Aurora server. Run `python3 activate-nova.py` first. Only after the preceding checks pass, use the explicit activation flags above.
7. The script records rollback data, builds API/web images, disables only Lykes's retired `auroratms` entitlement, enables its `nova` URL and checks Aurora readiness. The additive Nova product catalog row grants no other tenant access. It does not run development seeding or unrelated pending migrations.
8. Confirm Nova 2.0 is absent, Nova opens beneath Aurora's header, the Lykes Hub label remains visible, FreightOps navigation works, the Monday route is intact, and Nova sync business-write flags remain disabled.

## Rollback

- GoGlobal: restore NOVA01's original certificate selection/configuration. Original `GoGlobal.crt`/`GoGlobal.key` files remain intact. Use the supported reload procedure during the approved window. The old certificate is expired, so rollback restores the prior state, not valid browser TLS. Never disable validation.
- Aurora: activation saves `rollback/environment` and `rollback/tenant-products.sql` and restores them automatically if activation fails. Retain previous images. For later manual rollback, restore the environment with mode 0600, apply that SQL using the existing database administration method, then run the existing compose `up -d --no-deps api web`. Only the two Lykes product rows are restored; the additive catalog row may remain.
- Do not reset tenant data, clear synchronization queues, seed users, enable Nova writes, or change Cooke.

## Hub sidebar status

The Lykes label change is already published separately. Authenticated `/api/auth/tenant` returns client **3 / Lykes** for the verified Aurora session. Existing native Hub labels are retained. PublishGuard verified 126 compressed siblings and 60 boot entries; 26 existing authentication tests passed. No additional Hub publish is needed for this maintenance.

## Follow-up: Aurora release 20261030-7 is published

Nova launcher code is now deployed, with the Lykes Nova URL intentionally null. The application reports that browser access is not configured. Activation and browser verification remain pending.

Read-only review of GO-Global 6.3.3 Administrator Guide pages 191 and 195 (PDF pages 199 and 203) documents selecting a certificate through Admin Console / Host Options / Security / OK, without an explicit restart step. This does not establish a guarantee of uninterrupted sessions on the installed build. Installed HostPropertyDefinitions.xml contains no restart metadata for sslCertificate. Do not treat an APS restart as proven necessary, or a live certificate replacement as proven safe. Staged certificate remains ready; active configuration and service were left unchanged.

## No-disconnection activation request — follow-up inspection

User authorized activation only without disconnecting anyone. Read-only inspection confirmed NOVA01 GO-Global Host 6.3.3.34969, TLS listener PID 3496, MayRelayConnections=true and RelayConnectionAddress=NOVA01. Seventeen Menu5 processes were running. Installed PowerShell commands expose session queries and management but no certificate reload command. Documentation review did not establish uninterrupted certificate replacement for this build/relay configuration. No active certificate/configuration modification was attempted; PID remained 3496. Activation remains incomplete pending vendor confirmation of safe live certificate replacement or an isolated reproduction. Do not use the user's request as authorization to restart, disconnect, or experiment on existing sessions.

## Activated after explicit restart authorization

The user explicitly authorized the GoGlobal service restart. Activated the staged NV1 certificate on **NOVA01 / 172.23.0.2 only** and restarted **GGAPS** successfully (PID 3496 -> 27768, Running). Original certificate files remain unchanged; XML backup is `C:\ProgramData\GraphOn\GO-Global\ks\nv1-maintenance-20261030\HostProperties-before-activation.xml`. The active sslCertificate/value now points to the staged server.crt. No other GoGlobal host was restarted.

Public TLS verification passed without exceptions (TLS 1.3, certificate expires December 27, 2026). The browser at `https://nv1.aurorasoftware.com:491/?useApp=false&app=Nova` rendered the NOVA01 username/password sign-in screen. No X-Frame-Options or Content-Security-Policy framing restriction was returned. Aurora server public-address loopback timed out, so browser reachability was verified from the workstation, not inferred from server connectivity.

Updated only Lykes tenant `11111111-1111-1111-1111-111111111111` Nova product URL to that address. Existing active entitlement retained. Authenticated `/api/v1/me/products` confirms the exact URL and hasAccess=true. Configuration-only change: Aurora API/web images remain unchanged at the current release (20261030-9); **do not run the old candidate activation script**, which would replace newer builds. Previous Nova product row is saved on Aurora at `/opt/aurora-auth/releases/nova-url-before-activation-20261030.json`.

NativeReadOnly=true and WritesEnabled=false verified. No Cooke configuration change. Actual embedded Windows login, Nova application launch, correct Lykes database, keyboard, resizing, printing and clipboard still require a normal authorized Nova user's session. Browser used for verification has no Aurora session and stops at Aurora login; standalone GoGlobal sign-in rendered successfully. Windows/GoGlobal authentication remains separate from Aurora SSO.

## November 1: embedded client cross-origin failure

After login, Chrome reported SecurityError in util.js (window.parent.controlArguments) and handler.js ShowReconnectLink (parent.document): the direct GoGlobal page tried to access Aurora's different origin. This is distinct from TLS and does not establish a Nova database/login failure.

Added only `C:\Program Files\GraphOn\GO-Global\Web\aurora-nova.html` on NOVA01. This static, fixed-target wrapper embeds `logon.html?useApp=false&app=Nova`, giving the vendor page a same-origin parent. No vendor JS modifications, credential forwarding, CORS relaxation, service restart, or changes on other hosts. GGAPS PID remains 27768. Wrapper HTTPS and fixed target verified.

Changed only the Lykes Nova product URL to `https://nv1.aurorasoftware.com:491/aurora-nova.html`. Authenticated launcher confirms URL and access. API/web images unchanged; NativeReadOnly=true, WritesEnabled=false. Previous product row saved at `/opt/aurora-auth/releases/nova-url-before-wrapper-20261101.json`. User asked to sign in again for full session verification.

Browser follow-up: Aurora session expired on refresh, preventing authenticated iframe re-test until user signs in. Standalone wrapper loads vendor nested frames with no JavaScript errors observed; full post-login success remains unverified.
