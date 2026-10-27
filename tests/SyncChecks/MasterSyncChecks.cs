using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;

static class MasterSyncChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,Guid connectionId,Action<bool,string> check)
    {
        await using var db=await factory.OpenConnectionAsync();
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE tenant_id=@TenantId",new{tenant.TenantId});
        var drivers=new DriverStore(factory,tenant);
        var importer=new SyncImporter(factory,tenant,new OperationalSyncEntities(tenant,drivers));
        JsonObject Data<T>(T item){var data=JsonSerializer.SerializeToNode(item)!.AsObject();data.Remove("Id");data.Remove("Revision");return data;}
        var terminal=Data(new TerminalDto{Code="SYNC",Name="External terminal",Latitude=40,Longitude=-80});
        await importer.Apply(connectionId,new([new("master-terminal-1","terminal","SYNC","r1",terminal)],null),default);
        var terminals=new TerminalStore(factory,tenant);
        var saved=(await terminals.List(default)).Single(t=>t.Code=="SYNC");
        check(saved.Latitude==40&&saved.Longitude==-80,"terminal import saves address coordinates locally");
        saved.Name="Local name";await terminals.Save(saved,false,default);
        var updated=(JsonObject)terminal.DeepClone();updated["Latitude"]=41;
        await importer.Apply(connectionId,new([new("master-terminal-2","terminal","SYNC","r2",updated)],null),default);
        saved=(await terminals.List(default)).Single(t=>t.Code=="SYNC");
        check(saved.Name=="Local name"&&saved.Latitude==41,"terminal import preserves local name while merging external coordinates");
        var customer=Data(new CustomerDto{Code="SYNC-C",Name="External customer"});
        await importer.Apply(connectionId,new([new("master-customer-1","customer","SYNC-C","r1",customer)],null),default);
        check((await new CustomerStore(factory,tenant).List(default)).Any(c=>c.Code=="SYNC-C"),"customer import creates a locally owned customer record");
        var order=new TmsOrder{Id="USED-TERMINAL",Customer="Test",TerminalId=saved.Id};
        await new TmsStore(factory,tenant).SaveOrder(order,true,default);
        await terminals.Delete(saved.Id,saved.Revision,default);
        check(!(await terminals.List(default)).Any(t=>t.Id==saved.Id),"in-use terminal can be deleted from the active directory");
        check((await new TmsStore(factory,tenant).OrdersById(order.SourceId,[order.Id],default)).Single().TerminalId==saved.Id,"terminal deletion preserves existing order references");
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=false WHERE tenant_id=@TenantId",new{tenant.TenantId});
    }
}
