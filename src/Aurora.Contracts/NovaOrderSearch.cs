using System.Globalization;

namespace Aurora.Contracts;

public static class NovaOrderSearch
{
    // Keep the original search for PROs, references and names; also recognize Aurora's native ID alias.
    public static long? NativeId(string? search)
    {
        var value = search?.Trim();
        if (value is null || !value.StartsWith("FB", StringComparison.OrdinalIgnoreCase)) return null;
        var digits = value.AsSpan(2);
        if (digits.StartsWith("-")) digits = digits[1..];
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }
}
