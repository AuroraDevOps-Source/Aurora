using Microsoft.AspNetCore; // GetHttpRequest() — OpenIddictServerAspNetCoreHelpers lives here, not OpenIddict.Server.AspNetCore
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Aurora.Api.Auth;

/// <summary>
/// OpenIddict's default token endpoint returns the refresh token in the JSON body.
/// The doc's token contract wants it in an HttpOnly/Secure/SameSite=Strict cookie instead,
/// scoped to the token endpoint so it never rides along on ordinary API calls.
/// </summary>
public sealed class MoveRefreshTokenToCookieHandler : IOpenIddictServerHandler<ApplyTokenResponseContext>
{
    public const string CookieName = "Aurora.rt";
    public const string CookiePath = "/connect/token";

    public ValueTask HandleAsync(ApplyTokenResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var httpContext = context.Transaction.GetHttpRequest()?.HttpContext
            ?? throw new InvalidOperationException("This handler requires an ASP.NET Core host.");

        if (!string.IsNullOrEmpty(context.Response.RefreshToken))
        {
            httpContext.Response.Cookies.Append(CookieName, context.Response.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = CookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(14)
            });

            context.Response.RefreshToken = null;
        }

        return default;
    }
}
