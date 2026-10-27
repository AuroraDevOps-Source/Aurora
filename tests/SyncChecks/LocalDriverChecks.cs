using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;
using System.Text.Json.Nodes;

static class LocalDriverChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,Guid connectionId,Action<bool,string> check)
    {
        await using var db=await factory.OpenConnectionAsync();
        await db.ExecuteAsync("INSERT INTO aurora_terminal(tenant_id,id,code,name) VALUES(@TenantId,gen_random_uuid(),'101','Test terminal')",new{tenant.TenantId});
        var store=new DriverStore(factory,tenant);
        var input=new NovaDriver{Number="D1",FirstName="Test",LastName="Driver",Terminal="101",PerHour=25};
        var saved=await store.Save(0,input,default);
        check(saved.Id>0&&saved.Revision=="0"&&(await store.List(default)).Length==1,"driver profile is saved entirely in Aurora");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='driver'")==1,"driver save queues sync in its business transaction");
        saved.PerHour=30;
        var updated=await store.Save(saved.Id,saved,default);
        check(updated.Revision=="1"&&updated.PerHour==30,"driver rate edit increments local revision");
        try{await store.Save(saved.Id,saved,default);throw new Exception("Stale driver save accepted");}
        catch(FormatException){check(true,"driver stale save rejected");}
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='driver'")==2,"failed save does not add outbound work");
        input.Number="D2";input.Terminal="missing";
        try{await store.Save(0,input,default);throw new Exception("Unknown terminal accepted");}
        catch(FormatException){check(true,"driver requires a saved terminal");}
        await store.Delete(updated.Id,updated.Revision,default);
        check((await store.List(default)).Length==0&&await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='driver' AND operation='delete'")==1,"driver delete keeps a tombstone and queues external deletion");
        input.Number="D3";input.Terminal="101";
        var importer=new SyncImporter(factory,tenant,new DriverSyncEntities(store));
        var data=DriverStore.Data(input);
        await importer.Apply(connectionId,new([new("driver-import-1","driver","native-driver-3","r1",data)],null),default);
        var imported=(await store.List(default)).Single();
        check(imported.Number=="D3"&&imported.Id!=saved.Id,"incoming driver gets a local identity independent of Nova");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='driver'")==3,"external driver import does not echo an outbound update");
        imported.Phone="123";await store.Save(imported.Id,imported,default);
        var external=(JsonObject)data.DeepClone();external["PerHour"]=40;
        await importer.Apply(connectionId,new([new("driver-import-2","driver","native-driver-3","r2",external)],null),default);
        var merged=(await store.List(default)).Single();
        check(merged.Phone=="123"&&merged.PerHour==40,"real driver importer merges independent phone and pay edits");
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=false WHERE tenant_id=@TenantId",new{tenant.TenantId});
        merged.Email="driver@example.test";
        var offline=await store.Save(merged.Id,merged,default);
        check(offline.Email=="driver@example.test","Aurora driver edit succeeds while external sync is disabled");
        check(await new SyncStore(factory,tenant).Claim(connectionId,default) is null,"offline edits wait for connection to resume");
    }
}
