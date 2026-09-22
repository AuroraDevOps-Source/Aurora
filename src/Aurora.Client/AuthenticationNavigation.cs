namespace Aurora.Client;

public static class AuthenticationNavigation
{
    // Only resume the local authorization endpoint; never use login as an open redirect.
    public static string? AuthorizationReturnPath(string? returnUrl) =>
        returnUrl == "/connect/authorize" ||
        returnUrl?.StartsWith("/connect/authorize?", StringComparison.Ordinal) == true
            ? returnUrl : null;
}
