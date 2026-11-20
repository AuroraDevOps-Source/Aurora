using System.Globalization;
using Aurora.Contracts;

namespace Aurora.Api.Features.Routing;

public static class LykesDailyReport
{
    public static List<OrderDocumentDraft>? Read(Stream stream, TimeZoneInfo zone)
    {
        var rows = OrderReportRows.Read(stream, ["CID", "Cus Name", "Term", "HAWB", "MAWB",
            "Reference 1", "Pieces", "Weight", "Dim Weight", "Shipper Name", "Cons Name", "Effective Service Date"], out var date1904, daily:true);
        if (rows is null) return null;
        if (rows.Count is < 1 or > 5000) throw new FormatException("Import 1–5,000 daily report shipments at a time.");
        var drafts = new List<OrderDocumentDraft>(rows.Count);
        var pros = new HashSet<string>();
        foreach (var row in rows)
        {
            string Get(string field) => row.GetValueOrDefault(field) ?? "";
            var cid = Get("CID");
            var digits = string.Concat(cid.Where(char.IsAsciiDigit));
            var pro = OrderDocumentDraft.SplitTag(digits).Pro;
            if (!pros.Add(pro)) throw new FormatException($"CID {cid} repeats Nova PRO {pro} within the report.");
            decimal? Quantity(string field)
            {
                var value = Get(field);
                if (value == "") return null;
                if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) &&
                    n >= 0 && (field != "Pieces" || decimal.Truncate(n) == n)) return n;
                throw new FormatException($"CID {cid} has an invalid {field} value.");
            }

            var payer = Get("Cus Name");
            var novaPayer = BillToName(payer);
            var effectiveDate = Get("Effective Service Date");
            var draft = new OrderDocumentDraft
            {
                DocumentType = "Lykes", TagNumber = pro, DailyReportBillToName = novaPayer,
                BillOfLading = Get("HAWB"), HouseAirWaybill = Get("HAWB"), MasterAirWaybill = Get("MAWB"),
                ShipmentReference = Get("Reference 1"), TerminalCode = Get("Term"),
                FreightLines = [new() { Pieces = Quantity("Pieces"), WeightLb = Quantity("Weight"), DimWeightLb = Quantity("Dim Weight") }],
                Order = new()
                {
                    BillTo = novaPayer[..Math.Min(novaPayer.Length,40)], Notes = $"CID: {cid}\nBill-to name: {payer}",
                    Shipper = Get("Shipper Name"), OriginAddress = Get("Shipper Add 1"), OriginAddress2 = Get("Shipper Add 2"),
                    OriginCity = Get("Shipper City"), OriginState = Get("Shipper State"), OriginPostalCode = Get("Shipper Zip"),
                    Customer = Get("Cons Name"), Address = Get("Cons Add 1"), Address2 = Get("Cons Add 2"),
                    City = Get("Cons City"), State = Get("Cons State"), PostalCode = Get("Cons Zip"),
                    WorkType = Get("Service Level").ToUpperInvariant() is "PICKUP ONLY" or "PICKUP + TRANSFER" ? "Pickup" : "Delivery"
                }
            };
            if (effectiveDate.Length > 0)
            {
                if (double.TryParse(effectiveDate, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial >= 0 && serial < 2957000)
                    draft.LocalScheduledAt = DateTime.FromOADate(serial + (date1904 ? 1462 : 0));
                else if (DateTime.TryParse(effectiveDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    draft.LocalScheduledAt = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
                else throw new FormatException($"CID {cid} has an invalid Effective Service Date.");
                draft.ApplyLocalSchedule(zone);
            }
            draft.Normalize();
            draft.Order.TerminalCode = draft.TerminalCode;
            draft.ApplyFreightTotals();
            drafts.Add(draft);
        }
        return drafts;
    }
    private static string BillToName(string payer)
    {
        var dash = payer.IndexOf('-');
        if (dash < 0) return payer;
        var name = payer[..dash].Trim();
        if (!name.Equals("AVERITT EXPRESS", StringComparison.OrdinalIgnoreCase)) return name;

        var suffix = payer[(dash + 1)..].Trim();
        foreach (var service in new[] { "NEXT DAY", "SAME DAY" })
            if (suffix.Equals(service, StringComparison.OrdinalIgnoreCase) ||
                suffix.EndsWith(" " + service, StringComparison.OrdinalIgnoreCase))
                return name + " " + service;
        return payer;
    }
}
