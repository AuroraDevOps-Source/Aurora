using System.Globalization;
using Aurora.Contracts;

namespace Aurora.Api.Features.Routing;

// Crown's tabular export repeats shipment totals for driver/charge rows. Read its
// explicit columns directly, without an AI output limit or summing repeated freight.
public static class CrownOrderReport
{
    public static List<OrderDocumentDraft>? Read(Stream stream,TimeZoneInfo zone)
    {
        var data=OrderReportRows.Read(stream,["Ship ID","CID","Pcs","Wt","Shipper","Consignee","Service Date","Service Level"],out var date1904);
        if(data is null)return null;
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
            var draft=new OrderDocumentDraft{TagNumber=Get("CID"),BillToAccountId=Get("Account ID"),HouseAirWaybill=Get("HAWB"),MasterAirWaybill=Get("MAWB"),Order=new(){
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
        return output;
    }
}
