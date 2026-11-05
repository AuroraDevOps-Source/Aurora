using Aurora.Api.Features.Auth;
using Aurora.Contracts;
using Aurora.Integrations.Nova;
using Aurora.Modules.Routing;

namespace Aurora.Api.Features.Routing;

public static class ImportedOrderEndpoints
{
    public static void MapImportedOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/order-imports").RequireAuthorization();
        group.MapGet("/{batch:guid}/orders/{pro}",(Guid batch,string pro,OrderImportHistoryStore history,
            FirebirdRoutingSource native,TmsStore local,IConfiguration configuration,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>
        {
            pro=OrderDocumentDraft.SplitTag(pro).Pro;
            await history.RequireCreatedOrder(batch,pro,ct);
            if(!configuration.GetValue<bool>("Synchronization:LocalWorkspace"))return Results.Ok(await native.ReadImportedOrder(pro,ct));
            var order=await ReadLocal(local,pro,ct);
            return Results.Ok(new ImportedOrderEdit(order.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),ImportedOrderMapping.FromOrder(order),false));
        }));
        group.MapPut("/{batch:guid}/orders/{pro}",(Guid batch,string pro,ImportedOrderEdit input,OrderImportHistoryStore history,
            FirebirdRoutingSource native,TmsStore local,IConfiguration configuration,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>
        {
            pro=OrderDocumentDraft.SplitTag(pro).Pro;
            await history.RequireCreatedOrder(batch,pro,ct);
            if(!configuration.GetValue<bool>("Synchronization:LocalWorkspace"))await native.SaveImportedOrder(pro,input,ct);
            else
            {
                var current=await ReadLocal(local,pro,ct);
                if(input.Revision!=current.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture))throw new FormatException("This order changed. Close and reopen it before saving.");
                if(input.Draft.Order.Id!=pro || OrderDocumentDraft.SplitTag(input.Draft.TagNumber).Pro!=pro)throw new FormatException("The PRO number cannot be changed on an existing order.");
                // Copy only fields displayed in the review window. Ownership, assignments,
                // execution status and all other fields remain the current server values.
                foreach(var name in EditableFields)typeof(TmsOrder).GetProperty(name)!.SetValue(current,typeof(TmsOrder).GetProperty(name)!.GetValue(input.Draft.Order));
                current.Reference=input.Draft.BillOfLading;
                current.Notes=input.Draft.NotesWithReferences();
                await local.SaveOrder(current,false,ct,documentImport:true);
            }
            return Results.NoContent();
        }));
    }

    static async Task<TmsOrder> ReadLocal(TmsStore store,string pro,CancellationToken ct)
    {
        var orders=(await store.Orders(null,null,null,pro,null,ct)).Where(o=>o.Id==pro).ToArray();
        return orders.Length==1?orders[0]:throw new KeyNotFoundException("Order no longer exists or is not uniquely available in this workspace.");
    }
    static readonly string[] EditableFields=["Customer","Address","Address2","City","State","PostalCode","Country",
        "Shipper","OriginAddress","OriginAddress2","OriginCity","OriginState","OriginPostalCode","OriginCountry",
        "BillTo","BillToAddress","BillToAddress2","BillToCity","BillToState","BillToPostalCode","BillToCountry",
        "ScheduledAt","WorkType","Pieces","Weight","Cube","TerminalId","TerminalCode"];
}
