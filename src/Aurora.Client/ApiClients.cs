namespace Aurora.Client;

public static class ApiClients
{
    /// <summary>Attaches the bearer access token — for everything behind auth.</summary>
    public const string Authorized = "Aurora.Api";

    /// <summary>No auth handler — for the login call, which has no token yet.</summary>
    public const string Anonymous = "Aurora.Api.Anonymous";
}
