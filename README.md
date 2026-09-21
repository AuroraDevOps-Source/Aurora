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

## Checks

`tests/` holds executable console checks rather than a unit-test framework:

```bash
dotnet run --project tests/RoutingChecks
dotnet run --project tests/RoadRoutingChecks
dotnet run --project tests/EquipmentChecks
```

`tests/PlannerPreview` is a Blazor harness for eyeballing planner components.
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
