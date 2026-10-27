using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;

static class OrderSyncChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,Guid connectionId,Action<bool,string> check)
    {
        await using var db=await factory.OpenConnectionAsync();
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE tenant_id=@TenantId",new{tenant.TenantId});
        var importer=new SyncImporter(factory,tenant,new OperationalSyncEntities(tenant,new DriverStore(factory,tenant)));
        var item=new TmsOrder{Id="IMPORTED-1",NativeOrderId=123,Customer="External consignee",TerminalCode="PLN",Address="1 Main St",City="Test",ScheduledAt=new DateTimeOffset(2040,6,1,12,0,0,TimeSpan.Zero),Latitude=40,Longitude=-80};
        var data=JsonSerializer.SerializeToNode(item)!.AsObject();
        foreach(var key in new[]{"SourceId","Revision","TerminalId","CustomerId","OptimizationDraftId","ManifestId","ManifestNumber","StopNumber"})data.Remove(key);
        var batch=new ExternalBatch([new("order-import-1","order","123","1",data)],"orders-1");
        await importer.Apply(connectionId,batch,default);
        var tms=new TmsStore(factory,tenant);
        var saved=(await tms.Orders(null,null,null,item.Id,null,default)).Single();
        check(saved.NativeOrderId==123&&saved.TerminalId is not null,"incoming order resolves its terminal and retains external identity");
        var localId=$"{saved.SourceId:D}/{saved.Id}";
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='order' AND local_id=@localId",new{localId})==0,"initial order import does not echo back to Nova");
        await importer.Apply(connectionId,batch,default);
        check((await tms.Orders(null,null,null,item.Id,null,default)).Count==1,"order import retry does not create a duplicate");
        saved.Notes="Local instructions";await tms.SaveOrder(saved,false,default);
        var updated=(JsonObject)data.DeepClone();updated["Address"]="2 Main St";
        await importer.Apply(connectionId,new([new("order-import-2","order","123","2",updated)],"orders-2"),default);
        saved=(await tms.OrdersById(saved.SourceId,[saved.Id],default)).Single();
        check(saved.Address=="2 Main St"&&saved.Notes=="Local instructions","order import merges remote address with local instructions");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_conflict WHERE entity_type='order' AND local_id=@localId",new{localId})==0,"independent order edits do not create a conflict");
        var payload=await db.QuerySingleAsync<string>("SELECT payload::text FROM aurora_sync_outbox WHERE entity_type='order' AND local_id=@localId AND state='pending'",new{localId});
        var merged=JsonNode.Parse(payload)!;
        check(merged["Notes"]!.GetValue<string>()=="Local instructions"&&merged["Address"]!.GetValue<string>()=="2 Main St"&&merged["SourceId"] is null,"merged order outbound payload has both edits without local source identifiers");
        await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='synced' WHERE entity_type='order' AND local_id=@localId AND state='pending'",new{localId});
        var manifest=new JsonObject{["Number"]="SYNC-M1",["ManifestDate"]="2040-06-01",["Status"]="Planning",["Vehicle"]="PLN-TRUCK",["Details"]=JsonSerializer.SerializeToNode(new ManifestDetailsDto{ManifestDate=new DateOnly(2040,6,1)}),["Route"]=new JsonObject(),
            ["Orders"]=new JsonArray(new JsonObject{["SourceId"]=saved.SourceId.ToString(),["Id"]=saved.Id,["StopNumber"]=1})};
        await importer.Apply(connectionId,new([new("manifest-import-1","manifest","M1","1",manifest)],"manifest-1"),default);
        var imported=(await tms.Manifests(default)).Single(m=>m.Number=="SYNC-M1");
        var assigned=(await tms.OrdersById(saved.SourceId,[saved.Id],default)).Single();
        check(imported.OrderCount==1&&assigned.ManifestId==imported.Id&&assigned.Status==WorkspaceOrderStatus.Ready,"manifest import assigns orders without inventing execution status");
        var invalid=(JsonObject)manifest.DeepClone();invalid["Number"]="SYNC-M2";
        try{await importer.Apply(connectionId,new([new("manifest-import-bad","manifest","M2","1",invalid)],"bad"),default);throw new Exception("Double assignment accepted");}
        catch(FormatException){check(true,"manifest import rejects an order assigned elsewhere");}
        check(!(await tms.Manifests(default)).Any(m=>m.Number=="SYNC-M2"),"failed manifest import rolls back its new record");
        await importer.Apply(connectionId,new([new("manifest-import-delete","manifest","M1","2",manifest,true)],"manifest-2"),default);
        assigned=(await tms.OrdersById(saved.SourceId,[saved.Id],default)).Single();
        check(assigned.ManifestId is null&&assigned.Status==WorkspaceOrderStatus.Ready,"incoming manifest deletion releases its orders without deleting them");
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=false WHERE tenant_id=@TenantId",new{tenant.TenantId});
    }
}
