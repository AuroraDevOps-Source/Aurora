using System.Text.RegularExpressions;

namespace Aurora.Integrations.Nova;

internal sealed partial class DailyBillToIndex
{
    private readonly Dictionary<string,string?> codes = new(StringComparer.Ordinal);

    public DailyBillToIndex(IEnumerable<(string Code, string Name)> accounts)
    {
        foreach (var (code, name) in accounts)
        {
            var key = Key(name);
            if (key.Length > 0 && !codes.TryAdd(key, code))
                codes[key] = null; // Multiple account IDs share this name.
        }
    }

    public bool TryFind(string name, out string? code) => codes.TryGetValue(Key(name), out code);

    [GeneratedRegex(@"(?:^|[\s,()\-]+)(?:INC|INCORPORATED|LLC|CORP|CORPORATION)[\s,()\-]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LegalEnding();

    internal static string Key(string name)
    {
        var text = name.Trim().ToUpperInvariant().Replace(".", "");
        text = LegalEnding().Replace(text, "");
        // Keep meaningful symbols such as & and + distinct from an omitted word.
        return string.Concat(text.Where(c => !char.IsWhiteSpace(c) && (!char.IsPunctuation(c) || c == '&')));
    }
}
