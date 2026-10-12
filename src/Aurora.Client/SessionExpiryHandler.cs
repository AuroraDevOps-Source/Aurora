using System.Net;
using System.Net.Http.Json;
using Aurora.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace Aurora.Client;

// Wrap token acquisition as well as HTTP responses: token acquisition can fail
// before a request ever reaches the API.
public sealed class SessionExpiryHandler(NavigationManager navigation) : DelegatingHandler
{
    private bool _redirecting;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try { response = await base.SendAsync(request, cancellationToken); }
        catch (AccessTokenNotAvailableException)
        {
            response = new(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request,
                Content = JsonContent.Create(new ApiErrorDto("Your session has expired. Sign in again."))
            };
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized && !_redirecting)
        {
            _redirecting = true;
            var path = navigation.ToBaseRelativePath(navigation.Uri);
            if (!path.StartsWith("login", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("authentication/", StringComparison.OrdinalIgnoreCase))
                navigation.NavigateTo($"login?expired=true&returnUrl={Uri.EscapeDataString(navigation.Uri)}", forceLoad: true, replace: true);
        }
        return response;
    }
}
