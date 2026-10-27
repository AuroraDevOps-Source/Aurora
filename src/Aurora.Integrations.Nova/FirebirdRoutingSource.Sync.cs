using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Aurora.Integrations.Nova.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource : INovaSyncMapper
{
    private Guid _syncConnection;
    public INovaSyncMapper ForSynchronization(Guid connection)
    {
        if (_syncConnection != Guid.Empty && _syncConnection != connection) throw new InvalidOperationException("A mapper belongs to one connection.");
        _syncConnection = connection;
        return this;
    }
    private sealed record SyncMapping(string LocalId,string ExternalId,string Revision,string Baseline);
    private async Task<SyncMapping?> Mapping(string kind,string id,bool external,CancellationToken ct)
    {
        await using var pg=await factory.OpenConnectionAsync(ct);
        return await pg.QuerySingleOrDefaultAsync<SyncMapping>(new CommandDefinition(
            "SELECT local_id AS LocalId,external_id AS ExternalId,external_revision AS Revision,baseline::text AS Baseline FROM aurora_sync_mapping WHERE tenant_id=@TenantId AND connection_id=@connection AND entity_type=@kind AND "+(external?"external_id":"local_id")+"=@id",
            new{tenant.TenantId,connection=_syncConnection,kind,id},cancellationToken:ct));
    }
    private static string SyncHash(JsonObject data,bool deleted=false)
    {
        static JsonNode? Sorted(JsonNode? n)=>n switch{JsonObject o=>new JsonObject(o.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>KeyValuePair.Create(p.Key,Sorted(p.Value)))),JsonArray a=>new JsonArray(a.Select(Sorted).ToArray()),_=>n?.DeepClone()};
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new JsonArray(deleted,Sorted(data)).ToJsonString())));
    }
    private static JsonObject SyncObject<T>(T value)
    {
        var obj=JsonSerializer.SerializeToNode(value)!.AsObject();obj.Remove("Revision");return obj;
    }
    private static JsonObject Baseline(SyncMapping? map,JsonObject defaults)
    {
        if(map is not null)foreach(var field in JsonNode.Parse(map.Baseline)!.AsObject())defaults[field.Key]=field.Value?.DeepClone();
        return defaults;
    }
    private static object? NativeValue(JsonNode? value)=>value is null?null:value is JsonValue scalar && scalar.TryGetValue<string>(out var text)?text:JsonSerializer.Deserialize<object>(value.ToJsonString()) is JsonElement e?e.ValueKind switch{JsonValueKind.Number=>e.GetDecimal(),JsonValueKind.True=>"Y",JsonValueKind.False=>"N",_=>null}:null;
    private static string S(JsonObject obj,string key)=>obj[key]?.GetValue<string>()??"";
    private static readonly (string Property,string Column)[] SyncTerminalFields=[("Code","LM_ID"),("Name","LM_NAME"),("Address","LM_ADDR1"),("City","LM_CITY"),("State","LM_STATE"),("PostalCode","LM_ZIP"),("Country","LM_COUNTRY"),("Region","LM_REGION"),("Latitude","LM_LATITUDE"),("Longitude","LM_LONGITUDE")];
    private static readonly (string Property,string Column)[] SyncCustomerFields=[("Code","AC_CUST"),("Name","AC_NAME"),("Address","AC_ADDR1"),("Address2","AC_ADDR2"),("City","AC_CITY"),("State","AC_STATE"),("PostalCode","AC_ZIP"),("Country","AC_COUNTRY_CD"),("Phone","AC_PHONE")];
    private static async Task<JsonObject?> NativeProjection(FbConnection db,FbTransaction tx,string table,string key,object id,(string Property,string Column)[] fields,JsonObject data,CancellationToken ct)
    {
        var row=(IDictionary<string,object>?)await db.QuerySingleOrDefaultAsync(new CommandDefinition($"SELECT {string.Join(',',fields.Select(f=>f.Column).Distinct())} FROM {table} WHERE {key}=@id",new{id},tx,cancellationToken:ct));
        if(row is null)return null;
        foreach(var (property,column) in fields)
        {
            var value=row[column];
            data[property]=value is null or DBNull ? property is "Pieces" or "Weight" or "Cube"?JsonValue.Create(0m):data[property] is JsonValue existing && existing.TryGetValue<string>(out _)?JsonValue.Create(""):null
                :value is string text?JsonValue.Create(text.TrimEnd()):JsonSerializer.SerializeToNode(value);
        }
        return data;
    }
    private static async Task WriteProjection(FbConnection db,FbTransaction tx,string table,string key,object id,(string Property,string Column)[] fields,JsonObject data,bool exists,CancellationToken ct)
    {
        var values=fields.Where(f=>f.Column!=key).ToDictionary(f=>f.Column,f=>NativeValue(data[f.Property]));
        if(!exists){values[key]=id;await Insert(db,tx,table,values,ct);return;}
        var args=new DynamicParameters();args.Add("id",id);
        var sets=values.Select((p,i)=>{args.Add("v"+i,p.Value);return p.Key+"=@v"+i;});
        if(await db.ExecuteAsync(new CommandDefinition($"UPDATE {table} SET {string.Join(',',sets)} WHERE {key}=@id",args,tx,cancellationToken:ct))!=1)throw new SyncConflictException("The native record no longer exists.");
    }
    public async Task<IReadOnlyList<NovaSyncSnapshot>> Read(NovaChangeEvent change,FbConnection db,FbTransaction tx,CancellationToken ct)
    {
        RequireEnabled();
        var kind=change.EntityType;
        if(kind=="order-or-manifest")
        {
            var type=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT PB_TYPE FROM OPS_HEADER WHERE PB_ID=@id",new{id=long.Parse(change.EntityId,CultureInfo.InvariantCulture)},tx,cancellationToken:ct));
            if(type is null)
            {
                var deleted=new List<NovaSyncSnapshot>();
                foreach(var candidate in new[]{"order","manifest"})
                    if(await Mapping(candidate,change.EntityId,true,ct) is {} old)deleted.Add(new(candidate,change.EntityId,SyncHash(JsonNode.Parse(old.Baseline)!.AsObject(),true),JsonNode.Parse(old.Baseline)!.AsObject(),true));
                return deleted;
            }
            kind=type.Trim()=="M"?"manifest":"order";
        }
        if(kind=="equipment")
        {
            var type=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT TRIM(t.CTT_CODE) FROM EQUIP_HEADER e JOIN CT_TYPE t ON t.CTT_ID=e.EH_UNIT_TYPE WHERE e.EH_ID=@id",new{id=long.Parse(change.EntityId,CultureInfo.InvariantCulture)},tx,cancellationToken:ct));
            if(type=="DRV")kind="driver";
        }
        var mapping=await Mapping(kind,change.EntityId,true,ct);
        JsonObject? data;
        switch(kind)
        {
            case "terminal":
                data=await NativeProjection(db,tx,"LOCMAST","LM_ID",change.EntityId,SyncTerminalFields,Baseline(mapping,SyncObject(new TerminalDto())),ct);data?.Remove("Id");
                if(data is not null && mapping is null)
                {
                    var node=data.Deserialize<TerminalDto>()!;
                    await ApplyTerminalRouting([node],ct);
                    data=SyncObject(node);data.Remove("Id");
                }
                break;
            case "customer":
                data=await NativeProjection(db,tx,"ACCOUNT","AC_CUST",change.EntityId,SyncCustomerFields,Baseline(mapping,SyncObject(new CustomerDto())),ct);data?.Remove("Id");break;
            case "driver":
                var driver=(await Drivers(db,tx,ct)).SingleOrDefault(d=>d.Id.ToString(CultureInfo.InvariantCulture)==change.EntityId);
                data=driver is null?null:DriverStore.Data(driver);break;
            case "equipment":data=await ReadSyncEquipment(db,tx,long.Parse(change.EntityId,CultureInfo.InvariantCulture),mapping,ct);break;
            case "order":return await ReadSyncOrder(db,tx,long.Parse(change.EntityId,CultureInfo.InvariantCulture),mapping,ct);
            case "manifest":return await ReadSyncManifest(db,tx,long.Parse(change.EntityId,CultureInfo.InvariantCulture),mapping,ct);
            default:throw new FormatException("Unsupported Nova synchronization entity.");
        }
        if(data is null)
        {
            if(mapping is null)return [];
            data=JsonNode.Parse(mapping.Baseline)!.AsObject();
            return [new(kind,change.EntityId,SyncHash(data,true),data,true)];
        }
        return [new(kind,change.EntityId,SyncHash(data),data)];
    }
    private async Task<JsonObject?> ReadSyncEquipment(FbConnection db,FbTransaction tx,long id,SyncMapping? mapping,CancellationToken ct)
    {
        var row=await db.QuerySingleOrDefaultAsync<NovaFleetRow>(new CommandDefinition("""
            SELECT h.EH_ID AS NativeId,TRIM(h.EH_UNIT_ID) AS Unit,TRIM(h.EH_DOMICILE) AS Terminal,
            TRIM(t.CTT_CODE) AS Kind,TRIM(t.CTT_DESC) AS Description,h.EH_MAX_WEIGHT AS Weight,h.EH_MAX_CUBE AS Cubes,
            h.EH_TOT_FIXED_COST_HR AS PerHour,h.EH_TOT_VAR_COST_MI AS PerMile,h.EH_STD_START_TM AS StartTime,h.EH_STD_END_TM AS EndTime
            FROM EQUIP_HEADER h JOIN CT_TYPE t ON t.CTT_ID=h.EH_UNIT_TYPE WHERE h.EH_ID=@id AND h.EH_DELETE_DATE IS NULL
            """,new{id},tx,cancellationToken:ct));
        if(row is null)return null;
        var fleet=MapFleet([row],LocalTime(DateTime.Today).ToString("zzz"));
        await ApplyEquipmentProfiles(fleet,ct);
        var data=Baseline(mapping,new JsonObject{["Unit"]=SyncObject(fleet.Units[0]),["Type"]=SyncObject(fleet.Types[0])});
        var unit=data["Unit"]!.AsObject();var type=data["Type"]!.AsObject();
        unit["Id"]=row.Unit;unit["Terminal"]=row.Terminal??"";
        if(type["PowerOnly"]?.GetValue<bool>()==true)unit["UseForRouting"]=false;
        type["Weight"]=row.Weight;type["Cubes"]=row.Cubes;type["PerHour"]=row.PerHour;type["PerKilometer"]=row.PerMile/1.609344;
        await NativeProjection(db,tx,"EQUIP_HEADER","EH_ID",id,[("Vin","EH_VIN"),("Plate","EH_TAG_NUMBER"),("PlateState","EH_TAG_STATE")],unit,ct);
        unit.Remove("TypeCode");unit.Remove("Revision");type.Remove("Code");type.Remove("Revision");
        return data;
    }
}
