using System.Security.Claims;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Microsoft.AspNetCore; // GetOpenIddictServerRequest() — OpenIddictServerAspNetCoreHelpers lives here, not OpenIddict.Server.AspNetCore
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aurora.Api.Controllers;

[ApiController]
public sealed class AuthorizationController(
    UserManager<ApplicationUser> userManager,
    AuroraDbContext dbContext,
    NpgsqlConnectionFactory connectionFactory) : ControllerBase
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // The pre-authentication cookie set by /api/auth/login (see LoginEndpoints) is what
        // makes this silent: the client is registered with ConsentType.Implicit, so once this
        // cookie is present OpenIddict signs in and redirects straight back with a code —
        // the user never sees anything here.
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result is not { Succeeded: true })
        {
            // This endpoint is browser navigation, unlike the bearer-protected API. An
            // anonymous product sign-in must resume the same PKCE request after login.
            // Silent checks still receive the OIDC login_required error.
            if (request.HasPromptValue("none"))
                return Forbid(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired
                    }));

            var parameters = Request.HasFormContentType
                ? QueryString.Create(await Request.ReadFormAsync(HttpContext.RequestAborted))
                : Request.QueryString;
            var returnTo = Request.PathBase + Request.Path + parameters;
            return LocalRedirect("/login?returnUrl=" + Uri.EscapeDataString(returnTo));
        }

        // AuthenticateAsync(scheme) does not update the ambient HttpContext.User (it's still
        // whatever the *default* scheme resolved, which found no bearer token here) — but
        // ITenantContext reads HttpContext.User, and the user_tenant lookup below needs
        // app.user_id set from *this* principal. Make it the ambient one.
        HttpContext.User = result.Principal;

        var user = await userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("The authenticated user could not be resolved.");

        // user_tenant's RLS policy is scoped by user_id, not tenant_id (see its DbUp script) —
        // by design, since this lookup runs before any tenant is known.
        var tenantId = await dbContext.UserTenants
            .Where(ut => ut.UserId == user.Id && ut.IsDefault)
            .Select(ut => (Guid?)ut.TenantId)
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"User '{user.Id}' has no default tenant.");

        // Make the now-resolved tenant visible to ITenantContext for the rest of this request —
        // there is no tenant_id claim yet to read it from (identity.SetClaim below is what
        // creates it), and the tenant-scoped role lookup that follows needs it.
        HttpContext.Items[HttpTenantContext.TenantOverrideItemsKey] = tenantId;

        // UserManager.GetRolesAsync() is not tenant-aware (Identity has no concept of tenant),
        // so roles are queried directly, scoped to this tenant, rather than through it.
        //
        // The joins onto product/tenant_product are what enforce entitlement: a role is only
        // minted into the token if this tenant currently owns the product that role belongs to.
        // Cancel a product and its roles stop being issued on the next login — the downstream
        // application then fails its own "must hold a role" check without needing any licence
        // logic of its own.
        await using var roleConnection = await connectionFactory.OpenConnectionAsync(HttpContext.RequestAborted);
        var roles = await roleConnection.QueryAsync<string>(
            """
            SELECT r."Name"
            FROM "AspNetUserRoles" ur
            JOIN "AspNetRoles"  r  ON r."Id" = ur."RoleId"
            JOIN product        p  ON r."Name" LIKE p.role_prefix || ':%'
            JOIN tenant_product tp ON tp.product_code = p.code AND tp.tenant_id = ur."TenantId"
            WHERE ur."UserId" = @UserId AND ur."TenantId" = @TenantId AND tp.is_active
            """,
            new { UserId = user.Id, TenantId = tenantId });

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
                .SetClaim(Claims.Email, user.Email)
                .SetClaim(Claims.EmailVerified, user.EmailConfirmed)
                .SetClaim(Claims.Name, user.UserName)
                .SetClaim("tenant_id", tenantId.ToString())
                .SetClaims(Claims.Role, [.. roles]);

        // Audiences follow entitlement too: a token is only addressed to the APIs of products
        // this tenant owns, so FreightOps rejects a token minted for a Hub-only customer even
        // before roles are considered.
        var audiences = await roleConnection.QueryAsync<string>(
            """
            SELECT p.api_audience
            FROM tenant_product tp
            JOIN product p ON p.code = tp.product_code
            WHERE tp.tenant_id = @TenantId AND tp.is_active
            """,
            new { TenantId = tenantId });

        identity.SetScopes(request.GetScopes());
        identity.SetResources([.. audiences]);

        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject or Claims.Name or Claims.Email or Claims.Role or "tenant_id"
                => [Destinations.AccessToken],
            _ => [Destinations.AccessToken]
        });

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new NotImplementedException($"Unsupported grant type '{request.GrantType}'.");
        }

        // Authorization-code and refresh-token grants both resolve to a principal that was
        // already signed in via /connect/authorize — re-assert it, refreshing its claims.
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (result is not { Succeeded: true })
        {
            return Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The token is no longer valid."
                }));
        }

        var subject = result.Principal.GetClaim(Claims.Subject);
        var user = subject is null ? null : await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            return Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user no longer exists."
                }));
        }

        return SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
