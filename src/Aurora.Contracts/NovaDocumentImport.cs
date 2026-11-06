using System.Globalization;
namespace Aurora.Contracts;

// Uses the native editor's field definitions and validation; no arbitrary database columns from extraction.
public static class NovaDocumentImport
{
    public const int DefaultStatus = 110;
    public static string PostalCode(string value){value??="";return System.Text.RegularExpressions.Regex.IsMatch(value,@"^[0-9]{5}[- ][0-9]{4}$")?value.Replace("-","").Replace(" ",""):value;}

    public static NovaRecordEdit Build(OrderDocumentDraft draft,NovaRecordDocument template,int status,TimeZoneInfo zone)
    {
        var terminal=draft.Order.TerminalCode;
        draft.Normalize();
        var o=draft.Order;
        // Import is data entry, not a planning-readiness check. The selected terminal
        // is authoritative; optional business fields can be completed later.
        o.TerminalCode=terminal?.Trim().ToUpperInvariant();
        var values=template.Parts.ToDictionary(p=>p.Key,p=>p.Fields.ToDictionary(f=>f.Key,f=>f.Value));
        void Set(string part,string key,object? value)=>values[part][key]=Convert.ToString(value,CultureInfo.InvariantCulture);
        Set("header","PBPURORD",draft.PurchaseOrder);Set("header","PBNUM",o.Id);Set("header","PB_STATUS",status);Set("header","PB_LOCATION",o.TerminalCode);
        Set("header","PBBOL",string.IsNullOrWhiteSpace(draft.HouseAirWaybill)?draft.BillOfLading.Trim():draft.HouseAirWaybill.Trim());Set("header","PB_CUST_ORD",draft.MasterAirWaybill.Trim());Set("header","PBTPIECE",o.Pieces);Set("header","PBTWEIGH",o.Weight);Set("header","PB_TTL_CUBE",o.Cube);
        foreach(var (key,value) in new[]{("PBBNAME",o.BillTo),("PBBADDR1",o.BillToAddress),("PBBADDR2",o.BillToAddress2),("PBBCITY",o.BillToCity),("PBBSTATE",o.BillToState),("PBBZIP",PostalCode(o.BillToPostalCode)),("PBBCOUNTRY",o.BillToCountry)})Set("header",key,value);
        void Stop(string part,string name,string address,string address2,string city,string state,string zip,string country)
        {
            foreach(var (key,value) in new[]{("OPS_NAME",name),("OPS_ADDR1",address),("OPS_ADDR2",address2),("OPS_CITY",city),("OPS_STATE",state),("OPS_ZIP",PostalCode(zip)),("OPS_COUNTRY",country),("OPS_BOL",draft.BillOfLading.Trim())})Set(part,key,value);
        }
        Stop("pickup",o.Shipper,o.OriginAddress,o.OriginAddress2,o.OriginCity,o.OriginState,o.OriginPostalCode,o.OriginCountry);
        Stop("delivery",o.Customer,o.Address,o.Address2,o.City,o.State,o.PostalCode,o.Country);
        var active=o.WorkType=="Pickup"?"pickup":"delivery";
        Set(active,"OPS_APPT_LOW",TimeZoneInfo.ConvertTime(o.AppointmentFrom??o.ScheduledAt,zone).ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
        Set(active,"OPS_APPT_HIGH",TimeZoneInfo.ConvertTime(o.AppointmentTo??o.ScheduledAt,zone).ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
        return new("",values);
    }
}
