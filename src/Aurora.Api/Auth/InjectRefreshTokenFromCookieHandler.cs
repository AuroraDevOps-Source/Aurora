using Microsoft.AspNetCore; // GetHttpRequest() — OpenIddictServerAspNetCoreHelpers lives here, not OpenIddict.Server.AspNetCore
using OpenIddict.Abstractions; // IsRefreshTokenGrantType()
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Aurora.Api.Auth;

/// <summary>
/// Counterpart to <see cref="MoveRefreshTokenToCookieHandler"/>: since the refresh token
/// never appears in the client's JSON, the refresh call arrives with no refresh_token field —
/// this pulls it back out of the cookie the browser attached automatically.
/// </summary>
public sealed class InjectRefreshTokenFromCookieHandler : IOpenIddictServerHandler<ExtractTokenRequestContext>
{
    public ValueTask HandleAsync(ExtractTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        if (request is not null && request.IsRefreshTokenGrantType() && string.IsNullOrEmpty(request.RefreshToken))
        {
            var httpContext = context.Transaction.GetHttpRequest()?.HttpContext;
            if (httpContext is not null &&
                httpContext.Request.Cookies.TryGetValue(MoveRefreshTokenToCookieHandler.CookieName, out var cookieValue))
            {
                request.RefreshToken = cookieValue;
            }
        }

        return default;
    }
}
