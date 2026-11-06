namespace Aurora.Contracts;

public sealed class NovaImportAddress
{
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public string Address {get;set;}="";
    public string Address2 {get;set;}="";
    public string Address3 {get;set;}="";
    public string City {get;set;}="";
    public string State {get;set;}="";
    public string Zip {get;set;}="";
    public string Country {get;set;}="";
    public double? Latitude {get;set;}
    public double? Longitude {get;set;}

    static string Clean(string? value)=>(value??"").Trim().ToUpperInvariant();
    public static NovaImportAddress? Match(IEnumerable<NovaImportAddress> accounts,string name,string city,string state,string zip)
    {
        if(string.IsNullOrWhiteSpace(name))return null;
        var matches=accounts.Where(a=>Clean(a.Name)==Clean(name)
            && (string.IsNullOrWhiteSpace(city)||Clean(a.City)==Clean(city))
            && (string.IsNullOrWhiteSpace(state)||Clean(a.State)==Clean(state))
            && (string.IsNullOrWhiteSpace(zip)||Clean(a.Zip).Replace("-","")==Clean(zip).Replace("-",""))
            && !string.IsNullOrWhiteSpace(a.Address)).Take(2).ToArray();
        return matches.Length==1?matches[0]:null;
    }

    public static readonly (string Part,string Label,string[] Properties)[] Parties=[
        ("pickup","Shipper",["Shipper","OriginAddress","OriginAddress2","OriginCity","OriginState","OriginPostalCode","OriginCountry"]),
        ("delivery","Consignee",["Customer","Address","Address2","City","State","PostalCode","Country"]),
        ("header","Bill to",["BillTo","BillToAddress","BillToAddress2","BillToCity","BillToState","BillToPostalCode","BillToCountry"])];

    public static Dictionary<string,NovaImportAddress> Apply(OrderDocumentDraft draft,IEnumerable<NovaImportAddress> accounts)
    {
        var result=new Dictionary<string,NovaImportAddress>();var messages=new List<string>();
        foreach(var (part,label,properties) in Parties)
        {
            string Get(int i)=>(string?)typeof(TmsOrder).GetProperty(properties[i])!.GetValue(draft.Order)??"";
            var code=Clean(draft.AccountId(part));
            if(code.Length==0 && string.IsNullOrWhiteSpace(Get(0)))continue;
            var match=code.Length==0?Match(accounts,Get(0),Get(3),Get(4),Get(5)):accounts.FirstOrDefault(a=>Clean(a.Code)==code);
            if(match is null){messages.Add($"{label}: no unique Nova address match; kept the imported address.");continue;}
            if(draft.EditedAddressParts?.Contains(part)==true)
            {
                if(code.Length>0)result[part]=match;
                messages.Add($"{label}: kept the reviewed address.");
                continue;
            }
            draft.SetAccountId(part,match.Code.Trim());
            string[] values=[match.Name,match.Address,match.Address2,match.City,match.State,match.Zip,match.Country];
            for(var i=code.Length>0 && !string.IsNullOrWhiteSpace(Get(0))?1:0;i<properties.Length;i++)if(!string.IsNullOrWhiteSpace(match.Address) || !string.IsNullOrWhiteSpace(values[i]))typeof(TmsOrder).GetProperty(properties[i])!.SetValue(draft.Order,values[i]?.Trim()??"");
            result[part]=match;messages.Add($"{label}: using the address from Nova account {match.Code.Trim()}.");
        }
        draft.AddressLookupMessages=messages.ToArray();return result;
    }
}
