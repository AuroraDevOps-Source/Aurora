using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Aurora.Api.Controllers;

// The standard Blazor OIDC client loads this endpoint after exchanging its code.
// Return only the already validated, tenant-scoped identity and requested profile claims.
[ApiController]
public sealed class UserInfoController : ControllerBase
{
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo")]
    public IActionResult GetUserInfo()
    {
        var claims = new Dictionary<string, object?>
        {
            [Claims.Subject] = User.GetClaim(Claims.Subject)
        };
        if (User.HasScope(Scopes.Profile))
            claims[Claims.Name] = User.GetClaim(Claims.Name);
        if (User.HasScope(Scopes.Email))
            claims[Claims.Email] = User.GetClaim(Claims.Email);
        if (User.HasScope(Scopes.Roles))
            claims[Claims.Role] = User.GetClaims(Claims.Role).ToArray();
        return Ok(claims);
    }
}
