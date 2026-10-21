using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aurora.Contracts;

namespace Aurora.Api.Features.Auth;

public interface IInvitationEmail
{
    Task<InvitationEmailResult> Send(Guid userId, string userName, string email, CancellationToken ct);
}

public sealed class ResendInvitationEmail(HttpClient http, IConfiguration configuration, ILogger<ResendInvitationEmail> logger) : IInvitationEmail
{
    public async Task<InvitationEmailResult> Send(Guid userId, string userName, string email, CancellationToken ct)
    {
        var key = configuration["RESEND_API_KEY"];
        var origin = configuration["Resend:PublicOrigin"] ?? configuration["Hosting:PublicOrigin"];
        if (string.IsNullOrWhiteSpace(key) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            logger.LogWarning("Invitation for user {UserId} was not sent: email configuration is missing or invalid.", userId);
            return new(false, "Invitation email is not configured. Configure RESEND_API_KEY and Resend__PublicOrigin, then use Send invitation.");
        }

        var loginUrl = new Uri(uri, "/login?changePassword=true").AbsoluteUri;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        request.Headers.Add("Idempotency-Key", $"user-invitation/{userId}/{Guid.NewGuid():N}");
        request.Content = JsonContent.Create(new
        {
            from = "Aurora <alerts@aurorsoftware.com>",
            to = new[] { email },
            subject = "Your Aurora account is ready — sign in and change your password",
            text = $"Your Aurora account has been created.\n\nUsername: {userName}\n\nSign in and change your password: {loginUrl}\n\nUse the initial password provided by your administrator. You need to change it to your own password after signing in. If you have not received your initial password, contact your administrator.",
            html = $"<h1>Welcome to Aurora</h1><p>Your account has been created.</p><p>Username: <strong>{WebUtility.HtmlEncode(userName)}</strong></p><p><a href=\"{WebUtility.HtmlEncode(loginUrl)}\">Sign in and change your password</a></p><p>Use the initial password provided by your administrator. You need to change it to your own password after signing in. If you have not received your initial password, contact your administrator.</p>"
        });
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return new(true, null);
            // Provider bodies can contain recipient data; keep them out of logs and API errors.
            logger.LogWarning("Resend rejected invitation for user {UserId} with status {StatusCode}.", userId, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning("Resend invitation request failed for user {UserId} ({FailureType}).", userId, ex.GetType().Name);
        }
        return new(false, "Invitation email could not be sent. Use Send invitation to try again. If this continues, ask the server administrator to check Resend.");
    }
}
