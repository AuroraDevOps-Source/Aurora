# Single sign-on across Aurora, FreightOps and the Integration Hub

Aurora is the identity provider. FreightOps and the Hub accept tokens it issues **alongside the
tokens they issue themselves**, so this ships without a cutover: existing logins keep working,
and each application's login screen can be pointed at Aurora whenever it suits.

## How it fits together

- **Roles are namespaced per product** — `fo:Manager`, `hub:Admin`, `aurora:Admin`. Each
  application strips its own prefix and ignores every other product's roles, so an Admin in the
  Hub is never an Admin in FreightOps. Neither application's own role vocabulary changed.
- **Entitlement is enforced by role issuance, not by a licence check.** Aurora only mints a
  product's roles — and only addresses the token to that product's API — when the tenant has an
  active `tenant_product` row. Cancel the product and access stops at the next login, with no
  code in the downstream application involved.
- **FreightOps is installed once per customer**, so its URL is stored per tenant
  (`tenant_product.instance_url`) and each install is pinned to one tenant.
- **Users are linked, not migrated.** Both applications keep their own user rows (home terminal,
  client assignment, alert preferences). On first Aurora login the row is matched by email and
  the Aurora user id recorded. Nothing has to be migrated up front, and the differing password
  hashes stop mattering because Aurora becomes the only thing checking passwords.

## Configuring FreightOps

Per deployment, in `appsettings.json` (or environment overrides):

| Setting | Value |
|---|---|
| `Aurora:Authority` | Aurora's base URL, e.g. `https://auth.aurora.example.com`. **Leave empty to disable SSO** — the app then behaves exactly as it does today. |
| `Aurora:Audience` | `freightops-api` |
| `Aurora:TenantId` | The Aurora tenant GUID this install belongs to. This is how a per-customer install refuses a token issued for a different customer. |
| `Aurora:RequireHttpsMetadata` | `true` in production; `false` only for local HTTP testing. |

**Run `FreightOps/Migrations/SQL/AuroraUserLink.sql` before or with the deploy.** FreightOps does
not apply migrations at startup — `ApplyMigrations()` exists in `Helpers/MigrationExtension.cs`
but is never called — so the column will not appear on its own. If the new build starts against a
database that lacks it, the EF model and the schema disagree and *every* login fails, not just
Aurora ones, because loading an `ApplicationUser` selects a column that is not there.

(The equivalent `20260910090000_AuroraUserLink` migration is in the project if you prefer
`dotnet ef database update`; the SQL script records itself in the EF history table either way.)

## Configuring the Integration Hub

| Setting | Value |
|---|---|
| `Aurora:Authority` | Aurora's base URL. **Leave empty to disable SSO.** |
| `Aurora:Audience` | `hub-api` |
| `Aurora:RequireHttpsMetadata` | `true` in production. |

Schema is handled by DbUp: migration `0023_aurora_sso_links.sql` is embedded in `Hub.API.dll` and
applies automatically on deploy + IIS restart. Nothing to run by hand.

The links themselves are environment-specific, so they are a one-off instead:
`DB Scripts/provision-aurora-sso-links.sql` (edit the four `\set` values at the top, then run it
once). It ties a Hub account to an Aurora user and a Hub client to an Aurora tenant.

A Hub user signing in through Aurora resolves to a client via that link, so every existing
`GetClientId()` filter keeps working untouched.

## Granting a product to a tenant

```sql
INSERT INTO tenant_product (tenant_id, product_code, instance_url)
VALUES ('<tenant guid>', 'freightops', 'https://freightops.customer.example.com');
```

Then assign the user a role for it, e.g. `fo:Manager`, in `AspNetUserRoles`. The tile appears on
the launcher and the token starts carrying that role.

To revoke: `UPDATE tenant_product SET is_active = false WHERE ...`.

## What is deliberately not done yet

- **The FreightOps and Hub front ends still use their own login screens.** Both APIs accept
  Aurora tokens now, and both applications are registered as OIDC clients in Aurora
  (`freightops-spa`, `hub-spa`), so pointing each front end at Aurora is the remaining step —
  and can be done one application at a time.
- `IsAuroraUser()` in the Hub (its flag for Aurora Software staff) is not set for Aurora-issued
  tokens, so such users are treated as ordinary tenant users. Worth renaming that flag to
  something like `IsPlatformStaff`, since "Aurora" now also means the product.
- Access tokens are signed but no longer encrypted, because other applications must be able to
  read them with standard JWT validation. Nothing secret may be placed in a claim.

## Deploying Aurora behind a proxy (Cloudflare)

Aurora stamps its issuer into every token and publishes it in the discovery document, and both
other applications validate against it. Behind a TLS-terminating proxy the origin sees plain HTTP
on an internal host, so left alone it would advertise the wrong issuer and every downstream token
would be rejected. Two settings prevent that:

| Setting | Value |
|---|---|
| `Hosting:PublicOrigin` | The public URL, e.g. `https://user.aurorasoftware.com`. Pins the issuer. |
| `Hosting:BehindReverseProxy` | `true`. Honours `X-Forwarded-Proto`/`Host` and disables Aurora's own HTTPS redirect, which would otherwise loop forever against a proxy that speaks HTTP to the origin. |

Enforce HTTPS at the proxy ("Always Use HTTPS") rather than in the app. Whatever terminates TLS
must forward `X-Forwarded-Proto`, or the endpoint URLs in the discovery document come out as
`http://` and the browser refuses them as mixed content.

Also set `Cors:AllowedOrigins` to every product front end that signs in through Aurora, and
`Products:AuroraUrl` / `Products:FreightOpsUrl` / `Products:HubUrl` so the launcher tiles and the
registered redirect URIs point at the real deployments.

### Why the session cookie is SameSite=Lax

Signing in from another product is a cross-site top-level navigation — the Hub sends the browser
to Aurora's `/connect/authorize`. A `SameSite=Strict` cookie is deliberately withheld on exactly
that request, so Aurora would not recognise the existing session and would challenge instead of
completing silently. Lax is the correct setting for an identity provider's session cookie and
still withholds it from cross-site POSTs.
