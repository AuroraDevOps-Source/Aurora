# Aurora

Multi-tenant platform behind `user.aurorasoftware.com`: an OpenID Connect identity
provider, a product launcher, and the Route Optimization workspace. Sibling products
(Integration Hub, FreightOps) federate against this issuer and appear alongside
Route Optimization in the shared header.

.NET 10 throughout — ASP.NET Core API, Blazor WebAssembly client, PostgreSQL 16.

## Solution layout

| Project | Purpose |
| --- | --- |
| `src/Aurora.Api` | HTTP host. OpenIddict 7.6 authorization/token/user-info endpoints, cookie login, tenant resolution middleware, launcher and routing endpoints. |
| `src/Aurora.Client` | Blazor WebAssembly UI (MudBlazor). Login, product workspace with embedded product iframes, and the native Route Optimization pages. |
| `src/Aurora.Contracts` | DTOs shared between API and client. |
| `src/Aurora.Infrastructure` | EF Core + Npgsql, ASP.NET Identity entities, Dapper connection factory, tenancy context. |
| `src/Aurora.Migrations` | DbUp console migrator; numbered SQL in `Scripts/`, embedded as resources. |
| `src/Aurora.Modules.Routing` | Route optimization — PTV client, fleet/equipment settings, request merging, run summaries. |
| `tests/` | Executable check projects and browser/SQL checks (see below). |
| `deploy/` | Dockerfiles, Compose stack, nginx config. Secrets live in `deploy/private/`, untracked. |
| `scripts/` | Staging install, release deployment, and verification tooling. |

## Tenant isolation

Isolation is enforced at three layers, and the design intent is that no single one is
load-bearing on its own:

1. **Claim gate** — `TenantResolutionMiddleware` rejects any authenticated request
   whose `tenant_id` claim is missing or unparseable, rather than letting it reach a
   data-access path with no tenant context. Anonymous login/token requests pass through.
2. **Session context** — `TenantConnectionSetup` sets `app.tenant_id` and `app.user_id`
   on the Postgres session. Both the EF Core connection interceptor and the Dapper
   connection factory call it, so the two data-access paths cannot drift apart.
3. **Row-level security** — policies in the migration scripts key off `app.tenant_id`.
   This is the database-level backstop: a query that escapes the layers above still
   returns nothing.

`aurora_app` is the runtime role and is *not* `BYPASSRLS`. `aurora_owner` and
`aurora_admin` are.

## Local development

Prerequisites: .NET 10 SDK, PostgreSQL 16+.

Bootstrap the database once, as the Postgres superuser — this creates the roles and
database that the migrator itself cannot, because DbUp connects as `aurora_owner`:

```bash
psql -U postgres -f scripts/bootstrap-local-db.sql
```

Run the migrator, then the API and client:

```bash
dotnet run --project src/Aurora.Migrations
```

```bash
dotnet run --project src/Aurora.Api --launch-profile https
```

```bash
dotnet run --project src/Aurora.Client --launch-profile https
```

The API listens on `https://localhost:7077`, the client on `https://localhost:7259`;
the client's `wwwroot/appsettings.Development.json` points `ApiBaseUrl` at the former.
The dev seeder (`DevDataSeeder`) creates the demo tenant and an admin account.

### Configuration

The API reads these; supply them with user secrets locally and environment variables
in Docker. Nothing here belongs in `appsettings.json`.

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:Aurora` | Postgres connection string. |
| `Hosting:PublicOrigin` | Issuer origin when behind a proxy. |
| `Hosting:DataProtectionPath` | Persisted data-protection key ring. |
| `Auth:SigningCertificatePath` / `Auth:EncryptionCertificatePath` / `Auth:CertificatePassword` | Non-development token certificates. |
| `Auth:AllowEmbeddedLogin` | Issues `SameSite=None` session cookies so products can load in workspace iframes. |
| `Cors:AllowedOrigins` | Permitted browser origins. |
| `Ptv:*` | PTV routing credentials. |
| `RESEND_API_KEY` | Server-only Resend API key for user invitations. |
| `Resend:PublicOrigin` | Public **client** origin for invitation links; falls back to `Hosting:PublicOrigin`. Use `https://localhost:7259` locally. |

### User invitation emails

Adding a user under Admin > Users submits an invitation through the
[Resend Email API](https://resend.com/docs/api-reference/emails/send-email), from
`alerts@aurorsoftware.com`. Verify that exact sender domain in Resend. The email
includes the username and a sign-in link that continues to Change password.
Administrators provide the initial password separately; passwords are never emailed.
Users can also open Change password using the lock icon in the header. The email
instructs users to change their password; other application access is not blocked
until they do so.

Add the settings from `deploy/api.env.example` to the deployment's existing
`private/api.env`, then recreate the API container. Compose already loads that
file into the API process. The top-level deployment `.env` only configures Compose
substitution (such as `RELEASE_TAG`); putting a key there alone will not pass it to
the API. Keep API keys in the ignored env file, never in client configuration.
For local `dotnet run`, use process environment variables or API user secrets
(`RESEND_API_KEY` and `Resend:PublicOrigin`); .NET does not automatically load `.env`.

The user is committed before sending the invitation. Missing configuration or a
Resend failure leaves the account saved and displays an email warning. Use
**Send invitation** on that user's row to retry; editing a user does not send
another invitation. A successful response means Resend accepted the message,
not that the recipient's mailbox has delivered it.

## Checks

`tests/` holds executable console checks rather than a unit-test framework:

```bash
dotnet run --project tests/RoutingChecks
dotnet run --project tests/OrderWorkspaceChecks
dotnet run --project tests/PlanningSessionChecks
dotnet run --project tests/RoadRoutingChecks
dotnet run --project tests/EquipmentChecks
dotnet run --project tests/InvitationEmailChecks

dotnet run --project tests/FreightWorkflowChecks
```

`tests/PlannerPreview` is a Blazor harness for inspecting planner components. Its `/aurora/orders` page exercises filtering, selection, and the single-form planner with synthetic data and simulated PTV responses. Freight workflows can be saved and executed in memory. It does not sign in or persist orders/manifests to a database; use the full API/client for SSO and database verification.
See `docs/ROUTING-WIZARD-2026-09-23.md` for session storage and recovery details.
`tests/login-checks.mjs` and `tests/map-checks.mjs` are browser-driven checks;
`tests/equipment-isolation.sql` asserts that RLS actually blocks cross-tenant reads.

## Deployment

`deploy/compose.yml` runs Postgres, the API and the web client behind an external
`proxy` network (Caddy terminates TLS). Per-environment secrets, certificates and the
data-protection key ring stay in `deploy/private/` and on the host — never in an image
and never in this repository.

`DEPLOYMENT-2026-09-13.md` records the current staging installation, the SSO
verification results, and the repeat-deployment procedure. `docs/single-sign-on.md`
covers the federation design; `docs/ROUTING-*.md` cover the routing and equipment work.

The native Aurora workspace now contains Orders and Manifest. See `docs/AURORA-WORKSPACE-2026-09-24.md` for selection, Finish, imports, persistence and test-server verification.

### Nova / Firebird route optimization

`FirebirdRouting` is an opt-in, tenant-bound source. Configure `Enabled`, `TenantId`,
`SourceKey`, `ConnectionString`, `ReadyStatus` (110), and `TimeZoneId` in API user
secrets or server configuration. Credentials stay on the API server. The managed
Firebird provider connects remotely; a Firebird server installation on the client PC
is not required. The local test configuration targets `wayl_test` on `172.23.0.5`.

Opening Route Optimization or choosing **Refresh orders** refreshes unmanifested
delivery orders with the configured ready status. Native order IDs are displayed as
`FB-<PB_ID>` because PRO numbers may be zero or repeated. Ship date sets the planning
day; it does not exclude undated ready orders. Native appointments are retained.
The import preserves existing Aurora data and open drafts. It does not import
equipment or invent depot coordinates: configure those in Transportation Nodes and
Fleet Management. Default service duration is 900 seconds (`ServiceSeconds`).

**Finish** writes all accepted routes in one Firebird transaction as unassigned
Nova manifests, with delivery-stop membership in optimized order. Nova numbers are
saved on Aurora's manifests. Live order status and membership are checked again.
Stable `PB_EXTERNAL_REF` values and batch fingerprints recover a Firebird commit
whose Aurora commit or response was lost: retry the same Finish selection. Changing
that selection after an uncertain export or editing the exported Nova membership
requires reconciliation. Later Aurora manifest edits/deletions are not synchronized.

Run `dotnet run --project tests/FirebirdRoutingChecks` for isolated checks. Setting
`FIREBIRD_CHECK_CONFIG` to the local API user-secrets JSON explicitly enables checks
against the restored `127.0.0.1:55432/aurora_test` database and the specified Firebird
test database. They refresh Aurora and roll back all Firebird business writes;
Firebird generators may advance even when the transaction rolls back.

Orders also expose a Freight workflow for movement legs, warehouse/storage work,
ordered tasks, partner references, and execution history. Apply migration 0018 before
using it. See `docs/FREIGHT-WORKFLOW-2026-10-07.md` for behavior and integration boundaries.
