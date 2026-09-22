# Aurora and Aurora TMS test deployment — 2026-09-21

Reviewed the working-tree universal-login implementation in both repositories, corrected the
findings below, and published both existing test installations on `FreightOps-Daemon-01`.
Authorization came from Bryan's request; TEST-STAGING.md supplied the server/install details.

| Application | Public URL | Installation | Final release |
|---|---|---|---|
| Aurora | https://user.aurorasoftware.com | `/opt/aurora-auth` | `20260921-2` |
| Aurora TMS | https://nova.aurorasoftware.net | `/opt/aurora-nova` | `20260921-1` |

## Review corrections

- Preserve Aurora office roles through TMS refresh, MFA enrollment and `/me`; bound the SSO
  session to its original refresh expiry (configured 14 days). Local password behavior remains.
- Require verified email before automatic account linking; honor local lockout, detect
  concurrent linking, and reject unsupported roles, invalid tokens and cross-tenant requests.
- Validate PKCE state once, constrain local return paths and surface exchange failures.
- Map the TMS product to its own Aurora workspace instead of Route Optimization.
- Fix anonymous product sign-in: Aurora redirects to its login page, then resumes the original
  authorization request. Silent authorization returns `login_required`. This final correction
  is the Aurora-only `20260921-2` follow-up; TMS binaries did not change after release 1.
- Include PTV Quick update, reusing previous route sequences with a five-second solver budget.

## Configuration and data

Registered public PKCE client `auroratms-spa`, audience `auroratms-api`, nova callback and CORS
origin, and product code `auroratms` with office role prefix `atms`.

Linked Aurora Demo Co (`11111111-1111-1111-1111-111111111111`) to the existing TMS `demo`
workspace (`53b445f7-71b6-4d65-a3c0-e8c5648eea09`). The existing Aurora `bbatts` subject
`01a08138-7cb9-7a10-8bd8-450a7bfd755e` maps to Bryan's existing TMS admin account
`9f21cec7-d066-4025-993c-321c5bbfa772`. No replacement account was created. Other TMS tenants
were not linked. Additional users need the product role and an existing linked TMS account.

Applied additive migrations `0011_auroratms_product.sql`, `20260921160614_AuroraSsoLinks`, and
`20260921164132_AuroraSsoSessionRoles`. Development seeding was disabled. Existing databases,
volumes, uploads, private keys and secrets were retained. FreightOps, Hub and Caddy were not
redeployed. Git working trees were published as source snapshots; no commit/push was made.

## Verification

- Release publish succeeded for Aurora API/client/migrations and TMS API/frontend. TMS retains
  its pre-existing nullable/bundle-size build warnings.
- 85 Aurora routing/navigation checks and 16 login checks passed.
- Focused TMS signature/issuer/audience/lifetime/type, role and expiry tests passed locally;
  14 SSO tests passed against isolated PostgreSQL test containers on staging.
- Frontend PKCE, state consumption, safe returns, MFA retry, network failures and response
  validation checks passed. The actual deployed TMS sign-in card and redirect to Aurora's
  `Version 20260921-2` login page were verified in the browser.
- Live scripted login and PKCE/API checks passed for Aurora, FreightOps, Hub and TMS. The TMS
  check resumed the original anonymous authorization after login, redeemed its original
  verifier, exchanged to Bryan's local account and refreshed with unchanged roles/expiry.
  GET/POST anonymous authorization, silent login errors, token CORS, invalid tokens and tenant
  mismatch were checked. Authenticated browser dashboard navigation was not exercised.
- Embedded-session cookie/origin protections passed. All four application containers run with
  zero restarts and no error-level log entries during verification; TMS API health is healthy.
- Two live PTV calls used only the existing fictional one-order smoke manifest. Both full and
  quick returned `SUCCEEDED`, with no unscheduled orders or violations. Quick took 7.4976074
  seconds total. Larger-route latency was not benchmarked. No shipment or dispatch data was saved.

## Packages and images

Server archives are in the corresponding installation root, and extracted releases are under
`releases/<release>`. Each package includes `source.tar.gz` and `release.json` with base commit
and source hash. These snapshots represent the reviewed working tree at packaging time.

| Archive | SHA-256 |
|---|---|
| `aurora-release-20260921-2.tar.gz` | `1d515952ae9aefb47ec6a81af860097d84acd9927a2b86a799109a450f359ecd` |
| `auroratms-release-20260921-1.tar.gz` | `eb4431c12c62d7a2d90d0f17d6154064cb293a875072fdca3816220da5438897` |

| Running image tag | Image SHA-256 |
|---|---|
| `aurora-auth-api:20260921-2` | `b3150b8b5d0f95ea6b5bf2fccf3ce26693fd05e6d3378834c25839fd9d327047` |
| `aurora-auth-web:20260921-2` | `5aef38ed9f8e1b7b9a0dd07972f772bb22ab231c1baeb8e0e5636a34e0aae880` |
| `aurora-nova-api:20260921-1` | `944fae0627b9b6f7de6de25852290a7b0bcc113527a663eaaee137104dc37007` |
| `aurora-nova-web:20260921-1` | `b84394cf864699328837c19f6011c1a4f0d38a8b3e10052af5a9b6243c594f08` |

## Backups and rollback

Before activation, protected configuration copies and PostgreSQL custom-format backups were
created in `/opt/aurora-auth/backups/sso-20260921-1` and
`/opt/aurora-nova/backups/sso-20260921-1`. Both dumps passed `pg_restore --list` validation;
a full restore rehearsal was not performed. Original image IDs are recorded in `images.json`.
Each original application image is tagged `<container-name>:rollback-20260921-1`.

For an application rollback, select those image tags and recreate only the corresponding API
and web services, retaining the current database volumes and nullable SSO columns. Keep TMS
seeding disabled. Aurora's previous full release `20260915-2` remains available. If reverting
the complete TMS integration, also disable the new Aurora TMS entitlement/SSO configuration
so the older launcher does not expose an unsupported product. Do not restore an old database
merely to roll back application binaries.

The Aurora-only follow-up additionally retains release-1 images as
`aurora-auth-api:pre-20260921-2` and `aurora-auth-web:pre-20260921-2`, plus timestamped env
backups. Those images retain the anonymous sign-in issue and are not the preferred final build.
