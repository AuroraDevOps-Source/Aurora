using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aurora.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Aurora.Api.Features.Routing;

public static class AtlasDeliveryTickets
{
    // This reader implements the reviewed Waylanders profile. Edited instructions must go to the model.
    public static bool Supports(string company, string global) =>
        Fingerprint(company) == "6D091EA01DAA7B47E2BD3D5B65A650DDC404DC9C3DDC81439F29FFA6FBB30D52" &&
        Fingerprint(global) == "954A0DBAECADF88310AA54375A0A8F1B41B17DE991D5F1852681D8FCCC105F00";
    static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim().ReplaceLineEndings("\n"))));
    static Regex Pattern(string pattern) => new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(300));
    static readonly Regex Timestamp = Pattern(@"^\d{1,2}/\d{1,2}/\d{2,4}, \d{1,2}:\d{2} [AP]M Atlas$");
    static readonly Regex Header = Pattern(@"\ADelivery Ticket\n(?<name>[^\n]+)\n(?<street>[^\n]+)\n(?<zip>\d{5}(?:-\d{4})?)\n(?<city>[^\n]+)\n\uF095 (?<phone>[\d ()+.\-]+)\n\uF0E0 (?<email>[^\s@]+@[^\s@]+)\nOrder #: (?<id>[1-9]\d{0,8})\nCarrier: Waylanders Delivery\nORDER ID\nBox ID\nDescription\nDimensions\nQty\n#\k<id>\n");
    static readonly Regex Box = Pattern(@"\G(?<box>[A-Z]{2,6}[0-9]{2,10})\n(?<desc>[^\n]{1,180})\n(?<l>\d{1,4}(?:\.\d+)?)x(?<w>\d{1,4}(?:\.\d+)?)x(?<h>\d{1,4}(?:\.\d+)?) @ (?<lb>\d{1,5}(?:\.\d+)?)lbs\n1\n");
    static readonly Regex Service = Pattern(@"\ADelivery Service\nDelivery Instruction\n(?<service>[^\n]+)\n(?<handling>[^\n]+)\z");

    static readonly Regex NoEarlierThan = Pattern(@"^Dolly to door Deliver no earlier (January|February|March|April|May|June|July|August|September|October|November|December) ([1-9]|[12][0-9]|3[01])(st|nd|rd|th)$");

    public static List<OrderDocumentDraft>? Read(Stream stream, CancellationToken ct)
    {
        try
        {
            using var pdf = PdfDocument.Open(stream);
            if (pdf.NumberOfPages is < 1 or > 60) return null;
            var tickets = new List<StringBuilder>();
            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                if (page.NumberOfImages > 0 || page.Letters.Count > 16000) return null;
                var lines = ContentOrderTextExtractor.GetText(page).ReplaceLineEndings("\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2 || !Timestamp.IsMatch(lines[^2]) ||
                    lines[^1] != $"https://atlas.article.com/#/carrierLanding/pod/multiple {page.Number}/{pdf.NumberOfPages}") return null;
                foreach (var line in lines[..^2])
                {
                    if (line is "Signature: Date:" or "Comments:") continue;
                    if (line == "Delivery Ticket") tickets.Add(new StringBuilder());
                    if (tickets.Count == 0) return null;
                    tickets[^1].Append(line).Append('\n');
                }
            }
            var result = new List<OrderDocumentDraft>();
            var ids = new HashSet<string>();
            foreach (var ticket in tickets)
            {
                var text = ticket.ToString().TrimEnd();
                var h = Header.Match(text);
                if (!h.Success || !ids.Add(h.Groups["id"].Value)) return null;
                var lines = new List<DocumentFreightLine>();
                var offset = h.Length;
                while (Box.Match(text, offset) is { Success: true } box)
                {
                    decimal Number(string key) => decimal.Parse(box.Groups[key].Value, CultureInfo.InvariantCulture);
                    lines.Add(new() { Pieces=1, Description=box.Groups["box"].Value+" "+box.Groups["desc"].Value,
                        Length=Number("l"), Width=Number("w"), Height=Number("h"), WeightLb=Number("lb") });
                    offset += box.Length;
                }
                var service = Service.Match(text[offset..]);
                if (lines.Count == 0 || !service.Success) return null;
                var handling = service.Groups["handling"].Value;
                // Appointment/date interpretation beyond the verified ticket vocabulary goes through AI.
                var knownHandling = handling is "None" or "Dolly to door" or "Room of choice with assembly"
                    or "Room of choice with assembly There is an elevator but the building is locked, so we will need to be in touch day of delivery."
                    || NoEarlierThan.IsMatch(handling);
                if (!knownHandling || service.Groups["service"].Value is not ("Ground-level Door" or "Inside + Assembly")) return null;
                var draft = new OrderDocumentDraft { TagNumber=h.Groups["id"].Value, DocumentType="Other", FreightLines=lines.ToArray() };
                var order = draft.Order;
                order.WorkType="Delivery"; order.Customer=h.Groups["name"].Value; order.Address=h.Groups["street"].Value;
                order.PostalCode=h.Groups["zip"].Value; order.City=h.Groups["city"].Value;
                order.Notes=$"Phone: {h.Groups["phone"].Value}\nEmail: {h.Groups["email"].Value}\nDelivery Service: {service.Groups["service"].Value}\nDelivery Instruction: {handling}";
                draft.Normalize(); draft.ApplyFreightTotals(); result.Add(draft);
            }
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            // Unsupported PDF features or a changed layout are handled by the normal extraction service.
            return null;
        }
    }
}
