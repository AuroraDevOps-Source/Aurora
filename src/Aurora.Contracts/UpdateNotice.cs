namespace Aurora.Contracts;

public sealed record UpdateNotice(DateTimeOffset? UpdateAt, string? MessageTemplate = null)
{
    public const int MaxMessageLength = 1000;
    public const string DefaultMessage = "Update coming at {time}. Please log off at that time, wait 60 seconds, then press Ctrl+Shift+R to reload.";

    public static string FormatMessage(string? template, DateTimeOffset when) =>
        (template ?? DefaultMessage).Replace("{time}", when.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt (zzz)"), StringComparison.Ordinal);
}
