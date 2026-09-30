using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace Aurora.Client;

// OpenIddict userinfo returns role as an array, even when there is only one role.
public sealed class AuroraAccountClaimsPrincipalFactory(IAccessTokenProviderAccessor accessor)
    : AccountClaimsPrincipalFactory<RemoteUserAccount>(accessor)
{
    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account, RemoteAuthenticationUserOptions options)
    {
        var user = await base.CreateUserAsync(account, options);
        if (user.Identity is ClaimsIdentity identity && !string.IsNullOrEmpty(options.RoleClaim) &&
            account?.AdditionalProperties.TryGetValue(options.RoleClaim, out var value) == true &&
            value is JsonElement { ValueKind: JsonValueKind.Array } roles)
        {
            foreach (var claim in identity.FindAll(options.RoleClaim).ToArray())
                identity.RemoveClaim(claim);
            foreach (var role in roles.EnumerateArray())
                if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString()))
                    identity.AddClaim(new Claim(options.RoleClaim, role.GetString()!));
        }
        return user;
    }
}
