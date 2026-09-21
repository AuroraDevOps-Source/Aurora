using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Aurora.Infrastructure.Tenancy;

public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    /// <summary>
    /// Set mid-request once /connect/authorize resolves the user's default tenant — at that
    /// point the identity being built still carries no tenant_id claim (it's what's being
    /// decided), so later queries in the same request (e.g. the tenant-scoped roles lookup)
    /// need this override rather than the claim. See AuthorizationController.Authorize.
    /// </summary>
    public const string TenantOverrideItemsKey = "Aurora.TenantIdOverride";

    public Guid? TenantIdOrNull
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;

            if (httpContext?.Items.TryGetValue(TenantOverrideItemsKey, out var overrideValue) == true
                && overrideValue is Guid overrideTenantId)
            {
                return overrideTenantId;
            }

            var claim = httpContext?.User.FindFirst("tenant_id");
            if (claim is null)
            {
                return null;
            }

            return Guid.TryParse(claim.Value, out var tenantId)
                ? tenantId
                : throw new InvalidOperationException($"tenant_id claim '{claim.Value}' is not a valid GUID.");
        }
    }

    public Guid? UserIdOrNull
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            // OpenIddict's access-token principal carries "sub"; the Identity pre-auth cookie
            // principal carries ClaimTypes.NameIdentifier — both name the same user id.
            var claim = user?.FindFirst("sub") ?? user?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim is null)
            {
                return null;
            }

            return Guid.TryParse(claim.Value, out var userId)
                ? userId
                : throw new InvalidOperationException($"User id claim '{claim.Value}' is not a valid GUID.");
        }
    }
}
