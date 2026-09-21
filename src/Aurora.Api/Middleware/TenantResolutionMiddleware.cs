namespace Aurora.Api.Middleware;

/// <summary>
/// Fail loud, not open (§3.3): an authenticated request with a missing or malformed
/// tenant_id claim is rejected here rather than falling through to a data-access path
/// with no tenant context. Anonymous requests (login, token endpoints) pass through untouched.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id");
            if (claim is null || !Guid.TryParse(claim.Value, out _))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Missing or invalid tenant_id claim.");
                return;
            }
        }

        await next(context);
    }
}
