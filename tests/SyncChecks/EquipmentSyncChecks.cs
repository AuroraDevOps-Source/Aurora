using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;

static class EquipmentSyncChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,Guid connectionId,Action<bool,string> check)
    {
        await using var db=await factory.OpenConnectionAsync();
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE tenant_id=@TenantId",new{tenant.TenantId});
        var importer=new SyncImporter(factory,tenant,new OperationalSyncEntities(tenant,new DriverStore(factory,tenant)));
        var type=JsonSerializer.SerializeToNode(new EquipmentTypeDto{Description="Imported truck",Weight=10000,PerHour=25,PerKilometer=1})!.AsObject();type.Remove("Code");type.Remove("Revision");
        var unit=JsonSerializer.SerializeToNode(new EquipmentUnitDto{Id="SYNC-TRUCK",Terminal="PLN",UseForRouting=true})!.AsObject();unit.Remove("TypeCode");unit.Remove("Revision");
        var data=new JsonObject{["Type"]=type,["Unit"]=unit};
        await importer.Apply(connectionId,new([new("equipment-import-1","equipment","EQ1","1",data)],null),default);
        var store=new EquipmentStore(factory,tenant);
        var fleet=await store.List(default);var saved=fleet.Units.Single(u=>u.Id=="SYNC-TRUCK");
        check(fleet.Types.Single(t=>t.Code==saved.TypeCode).Weight==10000,"equipment import stores its capacity and routing settings locally");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='equipment' AND local_id='SYNC-TRUCK'")==0,"equipment import does not echo to Nova");
        saved.Alias="Local name";await store.Save(saved,default);
        var changed=(JsonObject)data.DeepClone();changed["Type"]!["Weight"]=12000;
        await importer.Apply(connectionId,new([new("equipment-import-2","equipment","EQ1","2",changed)],null),default);
        fleet=await store.List(default);saved=fleet.Units.Single(u=>u.Id=="SYNC-TRUCK");
        check(saved.Alias=="Local name"&&fleet.Types.Single(t=>t.Code==saved.TypeCode).Weight==12000,"equipment import merges local unit edits with external capacity edits");
        check(fleet.Types.Single(t=>t.Code=="PLN").Weight==10000,"imported unit capacity does not change another truck's type");
        var updatedType=fleet.Types.Single(t=>t.Code==saved.TypeCode);updatedType.PerHour=30;await store.Save(updatedType,default);
        var refreshed=(await store.List(default)).Units.Single(u=>u.Id==saved.Id);
        check(refreshed.Revision>saved.Revision,"changing equipment costs advances the affected unit revision");
        await store.Delete(refreshed.Id,refreshed.Revision,default);
        check(!(await store.List(default)).Units.Any(u=>u.Id==refreshed.Id),"equipment deletion removes the unit from the local fleet");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='equipment' AND local_id='SYNC-TRUCK' AND operation='delete'")==1,"equipment deletion queues its external tombstone atomically");
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=false WHERE tenant_id=@TenantId",new{tenant.TenantId});
    }
}
