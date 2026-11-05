using System.Globalization;

namespace Aurora.Contracts;

// Shared mapping for reopening the same compact review form from live order data.
public static class ImportedOrderMapping
{
    static readonly (string Part,string Field,string Property)[] TextFields = [
        ("header","PBBNAME","BillTo"),("header","PBBADDR1","BillToAddress"),("header","PBBADDR2","BillToAddress2"),
        ("header","PBBCITY","BillToCity"),("header","PBBSTATE","BillToState"),("header","PBBZIP","BillToPostalCode"),("header","PBBCOUNTRY","BillToCountry"),
        ("pickup","OPS_NAME","Shipper"),("pickup","OPS_ADDR1","OriginAddress"),("pickup","OPS_ADDR2","OriginAddress2"),
        ("pickup","OPS_CITY","OriginCity"),("pickup","OPS_STATE","OriginState"),("pickup","OPS_ZIP","OriginPostalCode"),("pickup","OPS_COUNTRY","OriginCountry"),
        ("delivery","OPS_NAME","Customer"),("delivery","OPS_ADDR1","Address"),("delivery","OPS_ADDR2","Address2"),
        ("delivery","OPS_CITY","City"),("delivery","OPS_STATE","State"),("delivery","OPS_ZIP","PostalCode"),("delivery","OPS_COUNTRY","Country")];

    public static OrderDocumentDraft FromOrder(TmsOrder order)
    {
        var draft=new OrderDocumentDraft{Order=order,TagNumber=order.Id,BillOfLading=order.Reference};
        var notes=new List<string>();
        foreach(var line in (order.Notes??"").Replace("\r\n","\n").Split('\n'))
        {
            if(line.StartsWith("Original Tag: "))draft.TagNumber=line[14..];
            else if(line.StartsWith("Work type: ") && line[11..] is "Pickup" or "Delivery")order.WorkType=line[11..];
            else if(line.StartsWith("Shipment reference: "))draft.ShipmentReference=line[20..];
            else if(line.StartsWith("HAWB: "))draft.HouseAirWaybill=line[6..];
            else if(line.StartsWith("MAWB: "))draft.MasterAirWaybill=line[6..];
            else notes.Add(line);
        }
        order.Notes=string.Join("\n",notes);
        return draft;
    }

    public static Dictionary<string,NovaRecordPart> Parts(NovaRecordDocument record)
    {
        // Imported orders have one pickup and one delivery. Avoid modifying the wrong
        // stop if Nova has since expanded the order into a more complex stop sequence.
        var pickups=record.Parts.Where(p=>p.Title=="Shipper / pickup").ToArray();
        var deliveries=record.Parts.Where(p=>p.Title=="Consignee / delivery").ToArray();
        if(pickups.Length!=1 || deliveries.Length!=1)throw new FormatException("This order's stops have changed. Open it from Orders to edit its full stop list.");
        return new(){["header"]=record.Parts.Single(p=>p.Key=="header"),["pickup"]=pickups[0],["delivery"]=deliveries[0]};
    }

    public static OrderDocumentDraft Read(NovaRecordDocument record,string notes,TimeZoneInfo zone)
    {
        var parts=Parts(record);
        string Value(string part,string field)=>parts[part].Fields.Single(f=>f.Key==field).Value??"";
        decimal Number(string field)=>decimal.TryParse(Value("header",field),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:0;
        DateTimeOffset? Date(string part,string field)
        {
            if(!DateTime.TryParse(Value(part,field),CultureInfo.InvariantCulture,out var date))return null;
            date=DateTime.SpecifyKind(date,DateTimeKind.Unspecified);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date,zone));
        }
        var order=new TmsOrder{Id=Value("header","PBNUM"),NativeOrderId=record.Id,TerminalCode=Value("header","PB_LOCATION"),
            Reference=Value("header","PBBOL"),Pieces=Number("PBTPIECE"),Weight=Number("PBTWEIGH"),Cube=Number("PB_TTL_CUBE"),Notes=notes};
        foreach(var (part,field,property) in TextFields)typeof(TmsOrder).GetProperty(property)!.SetValue(order,Value(part,field));
        order.WorkType=Date("pickup","OPS_APPT_LOW") is not null && Date("delivery","OPS_APPT_LOW") is null?"Pickup":"Delivery";
        var draft=FromOrder(order);
        var active=order.WorkType=="Pickup"?"pickup":"delivery";
        order.AppointmentFrom=Date(active,"OPS_APPT_LOW");order.AppointmentTo=Date(active,"OPS_APPT_HIGH");
        order.ScheduledAt=order.AppointmentFrom??DateTimeOffset.UnixEpoch;
        draft.NativeStatusId=int.TryParse(Value("header","PB_STATUS"),out var status)?status:null;
        return draft;
    }

    public static Dictionary<string,Dictionary<string,string?>> Edit(NovaRecordDocument current,OrderDocumentDraft draft,TimeZoneInfo zone,string notes="",bool clearSchedule=false)
    {
        var parts=Parts(current);
        var values=current.Parts.ToDictionary(p=>p.Key,p=>p.Fields.ToDictionary(f=>f.Key,f=>f.Value));
        void Set(string part,string field,object? value)=>values[parts[part].Key][field]=Convert.ToString(value,CultureInfo.InvariantCulture);
        // Start from the current record so contact fields, account IDs, address line 3,
        // inactive-stop appointments and other fields not shown in this form survive.
        foreach(var (part,field,property) in TextFields)Set(part,field,typeof(TmsOrder).GetProperty(property)!.GetValue(draft.Order));
        Set("header","PB_STATUS",draft.NativeStatusId);Set("header","PB_LOCATION",draft.Order.TerminalCode);
        Set("header","PBBOL",draft.BillOfLading);Set("header","PBTPIECE",draft.Order.Pieces);
        Set("header","PBTWEIGH",draft.Order.Weight);Set("header","PB_TTL_CUBE",draft.Order.Cube);
        var original=Read(current,notes,zone);
        if(draft.BillOfLading!=original.BillOfLading)
        { Set("pickup","OPS_BOL",draft.BillOfLading);Set("delivery","OPS_BOL",draft.BillOfLading); }
        if(clearSchedule)
        {
            var active=draft.Order.WorkType=="Pickup"?"pickup":"delivery";
            Set(active,"OPS_APPT_LOW",null);Set(active,"OPS_APPT_HIGH",null);
        }
        else if(draft.Order.ScheduledAt!=original.Order.ScheduledAt || draft.Order.WorkType!=original.Order.WorkType)
        {
            var active=draft.Order.WorkType=="Pickup"?"pickup":"delivery";
            var duration=(original.Order.AppointmentTo-original.Order.AppointmentFrom)??TimeSpan.Zero;
            Set(active,"OPS_APPT_LOW",TimeZoneInfo.ConvertTime(draft.Order.ScheduledAt,zone).ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
            Set(active,"OPS_APPT_HIGH",TimeZoneInfo.ConvertTime(draft.Order.ScheduledAt+duration,zone).ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
        }
        return values;
    }
}
