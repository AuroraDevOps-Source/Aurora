using Aurora.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddMudServices();

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = apiBaseAddress;
    options.ProviderOptions.ClientId = "aurora-spa";
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("roles");
    options.ProviderOptions.DefaultScopes.Add("offline_access");
    options.ProviderOptions.PostLogoutRedirectUri = "authentication/logout-callback";
});

// Named client the API calls go through — BaseAddressAuthorizationMessageHandler attaches the
// access token automatically.
builder.Services.AddHttpClient(ApiClients.Authorized, client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(authorizedUrls: [apiBaseAddress]));

// Login has no access token yet, and the handler above throws AccessTokenNotAvailableException
// rather than passing an anonymous request through — so /api/auth/login needs a handler-free
// client. It authenticates with a cookie (browser credentials), not a bearer token.
builder.Services.AddHttpClient(ApiClients.Anonymous, client => client.BaseAddress = new Uri(apiBaseAddress));

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient(ApiClients.Authorized));

await builder.Build().RunAsync();
