namespace Aurora.Client;

public static class RoutingDisplayText
{
    public static string Clean(string? text) => System.Text.RegularExpressions.Regex.Replace(text ?? "", "ptv", "routing service", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
