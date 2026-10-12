using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Components;
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
var navigation = new TestNavigation();
using var sessionClient = new HttpClient(new SessionExpiryHandler(navigation) { InnerHandler = new StatusHandler(HttpStatusCode.Unauthorized) });
await sessionClient.GetAsync("https://api.invalid/users");
await sessionClient.GetAsync("https://api.invalid/company");
if(navigation.Redirects!=1 || !navigation.Destination!.Contains("expired=true&returnUrl="))throw new Exception("Expired requests must redirect once and retain the return URL.");
foreach(var status in new[]{HttpStatusCode.OK,HttpStatusCode.Forbidden,HttpStatusCode.InternalServerError})
{
    var nav=new TestNavigation();
    using var client=new HttpClient(new SessionExpiryHandler(nav){InnerHandler=new StatusHandler(status)});
    using var response=await client.GetAsync("https://api.invalid/users");
    if(response.StatusCode!=status || nav.Redirects!=0)throw new Exception("Non-authentication responses must not redirect.");
}
Console.WriteLine("PASS session expiry redirect, concurrent request deduplication, and non-authentication response handling.");
var tokenNavigation=new TestNavigation();
using(var client=new HttpClient(new SessionExpiryHandler(tokenNavigation){InnerHandler=new MissingTokenHandler(tokenNavigation)}))
{
    using var response=await client.GetAsync("https://api.invalid/users");
    if(response.StatusCode!=HttpStatusCode.Unauthorized || tokenNavigation.Redirects!=1 || !(await response.Content.ReadAsStringAsync()).Contains("session has expired"))throw new Exception("Missing tokens must become a clear session-expired response and redirect.");
}
Console.WriteLine("PASS missing-token exception recovery.");
sealed class TokenAccessor : IAccessTokenProviderAccessor {
    public IAccessTokenProvider TokenProvider => throw new NotSupportedException();
}
sealed class StatusHandler(HttpStatusCode status):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(status));
}
sealed class MissingTokenHandler(NavigationManager navigation):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>
        throw new AccessTokenNotAvailableException(navigation,new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect,new AccessToken(),"authentication/login",new InteractiveRequestOptions{Interaction=InteractionType.SignIn,ReturnUrl=navigation.Uri}),[]);
}
sealed class TestNavigation:NavigationManager
{
    public int Redirects;public string? Destination;
    public TestNavigation()=>Initialize("https://app.invalid/","https://app.invalid/aurora/users");
    protected override void NavigateToCore(string uri,NavigationOptions options){Redirects++;Destination=uri;}
    protected override void NavigateToCore(string uri,bool forceLoad){Redirects++;Destination=uri;}
}
