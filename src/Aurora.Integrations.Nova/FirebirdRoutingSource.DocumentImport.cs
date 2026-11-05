using System.Globalization;
using Aurora.Contracts;
namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    public async Task<NovaRecordDocument> ImportDocument(OrderDocumentDraft draft,CancellationToken ct,bool rollbackOnly=false)
    {
        draft.ApplyLocalSchedule(TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId));
        var matches=await ResolveDocumentAddresses([draft],ct);
        var edit=NovaDocumentImport.Build(draft,NewRecord("orders"),draft.NativeStatusId??NovaDocumentImport.DefaultStatus,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId));
        if(matches.TryGetValue(draft,out var parties))foreach(var (part,address) in parties)
        {
            var values=edit.Values[part];
            if(part=="header")values["PB_BILL_ID"]=address.Code.Trim();
            else
            {
                values["OPS_ACCTID"]=address.Code.Trim();values["OPS_ADDR3"]=address.Address3?.Trim();
                if(HasCoordinates(address.Latitude,address.Longitude))
                {values["OPS_LATITUDE"]=address.Latitude?.ToString(CultureInfo.InvariantCulture);values["OPS_LONGITUDE"]=address.Longitude?.ToString(CultureInfo.InvariantCulture);}
            }
        }
        var saved=await CreateRecord("orders",edit,ct,rollbackOnly,document:draft);
        return saved with {LocationWarnings=[..saved.LocationWarnings,..draft.AddressLookupMessages]};
    }
}
