using System.Net.Http.Json;
using Aurora.Contracts;

namespace Aurora.Client.Pages.Shared;
public partial class NovaRecords
{
    void ChangeAccountName(string part,string key,string? value)
    {
        _values[part][key]=value;
        _values[part][part=="header"?"PB_BILL_ID":"OPS_ACCTID"]=null;
    }
    Task SelectAccount(string part,string id)=>Run(async()=>{
        using var response=await Http.GetAsync("api/v1/aurora/firebird/masters/customers/"+Uri.EscapeDataString(id));
        await Check(response);
        var account=(await response.Content.ReadFromJsonAsync<NovaMasterDocument>())!;
        _coordinateParts.Remove(part);
        foreach(var value in NovaAccountSelection.Values(account,part=="header"))
            if(_values[part].ContainsKey(value.Key))_values[part][value.Key]=value.Value;
    });
}
