using System.Text.RegularExpressions;

namespace Aurora.Modules.Routing;

// Export receipts remain the first line of native notes for retry recovery.
public static class ManifestNotes
{
    public static string Receipt(string? stored)
    {
        var first = (stored ?? "").Split('\n', 2)[0].Trim();
        return Regex.IsMatch(first, @"\AAurora (routing|line haul|consolidation) export v1 [A-F0-9]{64}\z") ? first : "";
    }
    public static string Read(string? stored) => Receipt(stored).Length == 0 ? stored ?? "" :
        (stored ?? "").Split('\n', 2).Skip(1).FirstOrDefault()?.TrimStart('\r', '\n') ?? "";
    public static string Write(string? stored, string notes)
    {
        if (notes is null || notes.Length > 4000) throw new FormatException("Notes must be 4,000 characters or fewer.");
        var receipt = Receipt(stored);
        return receipt.Length == 0 ? notes : receipt + (notes.Length == 0 ? "" : "\n" + notes);
    }
}
