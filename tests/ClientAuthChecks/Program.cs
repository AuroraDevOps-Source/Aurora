using System.Text.Json;
using Aurora.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

var factory = new AuroraAccountClaimsPrincipalFactory(new TokenAccessor());
var options = new RemoteAuthenticationUserOptions { AuthenticationType = "oidc", NameClaim = "name", RoleClaim = "role" };
var cases = new (string Json, bool Admin)[] {
    ("[\"aurora:Admin\",\"hub:Admin\",\"fo:Admin\"]", true),
    ("[\"aurora:Admin\"]", true),
    ("\"aurora:Admin\"", true),
    ("[\"hub:Admin\",\"aurora:Dispatcher\"]", false),
    ("[]", false),
    ("[null,42,{},\"\"]", false)
};
foreach (var (json, admin) in cases) {
    var account = new RemoteUserAccount { AdditionalProperties = new Dictionary<string, object>() };
    account.AdditionalProperties["name"] = "Dispatch user";
    account.AdditionalProperties["role"] = JsonSerializer.Deserialize<JsonElement>(json);
    var user = await factory.CreateUserAsync(account, options);
    if (user.IsInRole("aurora:Admin") != admin || user.Identity?.Name != "Dispatch user") throw new Exception("Incorrect role mapping: " + json);
}
var noRoles = await factory.CreateUserAsync(new RemoteUserAccount { AdditionalProperties = new Dictionary<string, object>() }, options);
if (noRoles.IsInRole("aurora:Admin")) throw new Exception("Missing roles must not grant admin access.");
var anonymous = await factory.CreateUserAsync(null!, options);
if (anonymous.Identity?.IsAuthenticated == true || anonymous.IsInRole("aurora:Admin")) throw new Exception("Anonymous identity must stay unauthenticated.");
Console.WriteLine("PASS 8 client authentication checks: array/scalar roles, non-admin, missing/invalid roles, and anonymous identity.");
sealed class TokenAccessor : IAccessTokenProviderAccessor {
    public IAccessTokenProvider TokenProvider => throw new NotSupportedException();
}
