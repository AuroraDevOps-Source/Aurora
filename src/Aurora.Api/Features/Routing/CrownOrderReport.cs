using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Aurora.Contracts;

namespace Aurora.Api.Features.Routing;

// Crown's tabular export repeats shipment totals for driver/charge rows. Read its
// explicit columns directly, without an AI output limit or summing repeated freight.
public static class CrownOrderReport
{
    static readonly XNamespace Ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static List<OrderDocumentDraft>? Read(Stream stream,TimeZoneInfo zone)
    {
        using var zip=new ZipArchive(stream,ZipArchiveMode.Read,leaveOpen:true);
        if(zip.Entries.Sum(e=>e.Length)>100*1024*1024)throw new FormatException("The expanded workbook is too large.");
        XDocument? Xml(string path){var entry=zip.GetEntry(path);if(entry is null)return null;using var s=entry.Open();return XDocument.Load(s);}
        var shared=Xml("xl/sharedStrings.xml")?.Descendants(Ns+"si").Select(s=>string.Concat(s.Descendants(Ns+"t").Select(t=>t.Value))).ToArray()??[];
        var date1904=(string?)Xml("xl/workbook.xml")?.Descendants(Ns+"workbookPr").FirstOrDefault()?.Attribute("date1904") is "1" or "true";
        var data=new List<Dictionary<string,string>>();bool recognized=false;
        foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("xl/worksheets/sheet",StringComparison.Ordinal)&&e.FullName.EndsWith(".xml",StringComparison.Ordinal)))
        {
            Dictionary<string,string>? headers=null;
            foreach(var row in Xml(entry.FullName)!.Descendants(Ns+"row"))
            {
                var cells=new Dictionary<string,string>();
                foreach(var c in row.Elements(Ns+"c"))
                {
                    var column=new string(((string?)c.Attribute("r")??"").TakeWhile(char.IsLetter).ToArray());
                    var value=(string?)c.Element(Ns+"v")??"";
                    if((string?)c.Attribute("t")=="s")value=shared[int.Parse(value,CultureInfo.InvariantCulture)];
                    else if((string?)c.Attribute("t")=="inlineStr")value=string.Concat(c.Descendants(Ns+"t").Select(t=>t.Value));
                    cells[column]=value.Trim();
                }
                if(headers is null)
                {
                    if(new[]{"Ship ID","CID","Pcs","Wt","Shipper","Consignee","Service Date","Service Level"}.All(h=>cells.Values.Contains(h)))
                    {headers=cells;recognized=true;}
                    continue;
                }
                var values=headers.ToDictionary(h=>h.Value,h=>cells.GetValueOrDefault(h.Key)??"");
                if(string.IsNullOrWhiteSpace(values["CID"]))continue;
                data.Add(values);
                if(data.Count>20000)throw new FormatException("Import up to 20,000 report rows at a time.");
            }
        }
        if(!recognized)return null;
        var output=new List<OrderDocumentDraft>();
        foreach(var group in data.GroupBy(r=>r["CID"].Trim().ToUpperInvariant()))
        {
            var r=group.First();
            string Get(string key){var v=r.GetValueOrDefault(key)??"";return v.All(c=>c=='.')?"":v;}
            decimal Number(string key)=>decimal.TryParse(Get(key),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:0;
            // Charge/driver rows may differ; shipment data must not silently disappear.
            foreach(var key in new[]{"Ship ID","CID","Pcs","Wt","Service Date","Shipper","Shipper City","Shipper State","Shipper Zip","Consignee","Consignee City","Consignee State","Consignee Zip","HAWB","MAWB","Service Level"})
                if(group.Select(x=>x.GetValueOrDefault(key)??"").Distinct().Count()>1)throw new FormatException($"Tag {Get("CID")} has conflicting {key} values. Review those rows before importing.");
            var service=Get("Service Level").ToUpperInvariant();
            var draft=new OrderDocumentDraft{TagNumber=Get("CID"),HouseAirWaybill=Get("HAWB"),MasterAirWaybill=Get("MAWB"),Order=new(){
                Id=OrderDocumentDraft.SplitTag(group.Key).Pro,TerminalCode=Get("Terminal"),Shipper=Get("Shipper"),OriginCity=Get("Shipper City"),OriginState=Get("Shipper State"),OriginPostalCode=Get("Shipper Zip"),
                Customer=Get("Consignee"),City=Get("Consignee City"),State=Get("Consignee State"),PostalCode=Get("Consignee Zip"),BillTo=Get("Cus Name")[..Math.Min(40,Get("Cus Name").Length)],
                Pieces=Number("Pcs"),Weight=Number("Wt"),WorkType=service is "PICKUP ONLY" or "PICKUP + TRANSFER"?"Pickup":"Delivery",
                Notes=$"Crown Ship ID: {Get("Ship ID")}\nAccount ID: {Get("Account ID")}\nCrown customer name: {Get("Cus Name")}\nService level: {Get("Service Level")}\nMove type: {Get("Move Type")}\nSource report rows: {group.Count()}"}};
            var date=Get("Service Date");
            if(!string.IsNullOrEmpty(date))
            {
                var local=double.TryParse(date,NumberStyles.Float,CultureInfo.InvariantCulture,out var serial)?DateTime.FromOADate(serial+(date1904?1462:0)):DateTime.Parse(date,CultureInfo.InvariantCulture);
                draft.Order.ScheduledAt=new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),zone));
            }
            draft.Normalize();output.Add(draft);
        }
        if(output.Count is <1 or >5000)throw new FormatException("Import 1–5,000 Crown shipments at a time.");
        // Distinct full Tags can share the same numeric suffix across terminals.
        // Use Crown's shipment identity only for those collisions, retaining the Tag.
        foreach(var group in output.GroupBy(d=>d.Order.Id).Where(g=>g.Count()>1).ToArray())
            foreach(var draft in group)
            {
                var original=draft.TagNumber;
                var source=data.First(r=>r["CID"].Trim().Equals(original,StringComparison.OrdinalIgnoreCase));
                var shipId=OrderDocumentDraft.SplitTag(source["Ship ID"]).Pro;
                draft.Order.Notes=$"Crown Tag: {original}\nNova PRO uses Crown Ship ID because the Tag number is shared across terminals.\n"+draft.Order.Notes;
                draft.TagNumber=OrderDocumentDraft.SplitTag(original).Terminal+shipId;draft.Normalize();
            }
        if(output.Select(d=>d.Order.Id).Distinct().Count()!=output.Count)throw new FormatException("Crown shipment identifiers still overlap after resolving shared Tag numbers.");
        return output;
    }
}
