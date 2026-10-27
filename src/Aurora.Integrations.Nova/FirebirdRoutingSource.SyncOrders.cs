using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;
public sealed partial class FirebirdRoutingSource
{
    private static readonly (string Property,string Column)[] SyncOrderFields=[("Reference","PBBOL"),("Pieces","PBTPIECE"),("Weight","PBTWEIGH"),("Cube","PB_TTL_CUBE"),("TerminalCode","PB_LOCATION"),("BillTo","PBBNAME"),("BillToAddress","PBBADDR1"),("BillToAddress2","PBBADDR2"),("BillToCity","PBBCITY"),("BillToState","PBBSTATE"),("BillToPostalCode","PBBZIP"),("BillToCountry","PBBCOUNTRY")];
    private static readonly (string Property,string Column)[] SyncDeliveryFields=[("Customer","OPS_NAME"),("Address","OPS_ADDR1"),("Address2","OPS_ADDR2"),("City","OPS_CITY"),("State","OPS_STATE"),("PostalCode","OPS_ZIP"),("Country","OPS_COUNTRY"),("Latitude","OPS_LATITUDE"),("Longitude","OPS_LONGITUDE"),("CustomerCode","OPS_ACCTID")];
    private static readonly (string Property,string Column)[] SyncPickupFields=[("Shipper","OPS_NAME"),("OriginAddress","OPS_ADDR1"),("OriginAddress2","OPS_ADDR2"),("OriginCity","OPS_CITY"),("OriginState","OPS_STATE"),("OriginPostalCode","OPS_ZIP"),("OriginCountry","OPS_COUNTRY"),("OriginLatitude","OPS_LATITUDE"),("OriginLongitude","OPS_LONGITUDE")];
    private int[]? _syncReadyStatuses,_syncPickupStatuses;
    private sealed class SyncOrderHeader { public string Kind {get;set;}=""; public DateTime? Date {get;set;} public long Status {get;set;} public string StatusCode {get;set;}=""; public int Deleted {get;set;} }
    private sealed class SyncStop { public long Id {get;set;} public string Kind {get;set;}=""; public DateTime? AppointmentFrom {get;set;} public DateTime? AppointmentTo {get;set;} public DateTime? Delivered {get;set;} public long? Manifest {get;set;} }
    private async Task<IReadOnlyList<NovaSyncSnapshot>> ReadSyncOrder(FbConnection db,FbTransaction tx,long id,SyncMapping? mapping,CancellationToken ct)
    {
        var header=await db.QuerySingleOrDefaultAsync<SyncOrderHeader>(new CommandDefinition("""
            SELECT h.PB_TYPE AS Kind,h.PB_DT_ENT AS "Date",h.PB_STATUS AS Status,
            COALESCE(TRIM(s.CTS_CODE),'') AS StatusCode,CASE WHEN h.PB_DT_CANC IS NOT NULL THEN 1 ELSE 0 END AS Deleted
            FROM OPS_HEADER h LEFT JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS WHERE h.PB_ID=@id
            """,new{id},tx,cancellationToken:ct));
        if(header is null || header.Deleted!=0)
        {
            if(mapping is null)return [];
            var previous=JsonNode.Parse(mapping.Baseline)!.AsObject();
            return [new("order",id.ToString(),SyncHash(previous,true),previous,true)];
        }
        if(header.Kind.Trim()!="O")throw new FormatException("Expected a Nova order.");
        var data=Baseline(mapping,SyncObject(new TmsOrder{Id=$"FB-{id}",ScheduledAt=LocalTime(header.Date??DateTime.Today)}));
        data=await NativeProjection(db,tx,"OPS_HEADER","PB_ID",id,SyncOrderFields,data,ct)??throw new KeyNotFoundException("Order disappeared.");
        var stops=(await db.QueryAsync<SyncStop>(new CommandDefinition("""
            SELECT OPS_STOP_ID AS Id,TRIM(OPS_TYPE) AS Kind,OPS_APPT_LOW AS AppointmentFrom,OPS_APPT_HIGH AS AppointmentTo,
            OPS_DELIVERED_TIME AS Delivered,OPS_MANIFEST_NUMBER AS Manifest
            FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE IN ('P','D') ORDER BY OPS_STOP_NUMBER,OPS_STOP_ID
            """,new{id},tx,cancellationToken:ct))).ToArray();
        // Aurora's order editor represents one pickup and one delivery. Never silently
        // collapse native multi-leg orders and then write a partial aggregate back.
        if(stops.Count(s=>s.Kind=="P")!=1 || stops.Count(s=>s.Kind=="D")!=1)throw new FormatException($"Order {id} needs one pickup and one delivery before synchronization.");
        var pickup=stops.Single(s=>s.Kind=="P");var delivery=stops.Single(s=>s.Kind=="D");
        if(_syncReadyStatuses is null)
        {
            await using var pgConfig=await factory.OpenConnectionAsync(ct);
            _syncReadyStatuses=await ReadyStatuses(pgConfig,null,ct);
            _syncPickupStatuses=await PickupReadyStatuses(pgConfig,null,ct);
        }
        var pickupWork=_syncPickupStatuses!.Contains((int)header.Status);
        data["WorkType"]=pickupWork?"Pickup":"Delivery";
        data["NativeOrderId"]=id;data["UseConsigneeAddress"]=true;
        if(mapping is null || mapping.Revision=="legacy-snapshot")
        {
            var routing=await OrderRouting([id],ct);
            if(routing.TryGetValue(id,out var savedRouting))data["ReceivingTerminal"]=savedRouting.ReceivingTerminal;
        }
        var activeStop=pickupWork?pickup:delivery;

        await NativeProjection(db,tx,"OPS_STOP_REC","OPS_STOP_ID",pickup.Id,SyncPickupFields,data,ct);
        await NativeProjection(db,tx,"OPS_STOP_REC","OPS_STOP_ID",delivery.Id,SyncDeliveryFields,data,ct);
        data["ScheduledAt"]=LocalTime(activeStop.AppointmentFrom??header.Date??DateTime.Today);
        data["AppointmentFrom"]=activeStop.AppointmentFrom is {} from?JsonValue.Create(LocalTime(from)):null;
        data["AppointmentTo"]=activeStop.AppointmentTo is {} to?JsonValue.Create(LocalTime(to)):null;
        data["Status"]=delivery.Delivered is not null?"Delivered":header.StatusCode switch{"DPCH"=>"Dispatched","ENR"=>"InTransit","CPLT"=>"Delivered","VOID"=>"Voided",_=>activeStop.Manifest is >0?"Routed":_syncReadyStatuses.Contains((int)header.Status)||pickupWork?WorkspaceOrderStatus.Ready:"Pending"};
        foreach(var key in new[]{"SourceId","Revision","TerminalId","CustomerId","ManifestId","ManifestNumber","StopNumber","OptimizationDraftId"})data.Remove(key);
        return [new("order",id.ToString(CultureInfo.InvariantCulture),SyncHash(data),data){BusinessDate=header.Date is {} date?DateOnly.FromDateTime(date):null,Open=S(data,"Status") is not ("Delivered" or "Voided")}];
    }

    private sealed class SyncManifestHeader { public DateTime? Date {get;set;} public string Status {get;set;}=""; public string Origin {get;set;}=""; public string Destination {get;set;}=""; public string Truck {get;set;}=""; public long? Driver {get;set;} public string? Notes {get;set;} public int Deleted {get;set;} }
    private sealed class SyncManifestMember { public long OrderId {get;set;} public int StopNumber {get;set;} }
    private async Task<IReadOnlyList<NovaSyncSnapshot>> ReadSyncManifest(FbConnection db,FbTransaction tx,long id,SyncMapping? mapping,CancellationToken ct)
    {
        var row=await db.QuerySingleOrDefaultAsync<SyncManifestHeader>(new CommandDefinition("""
            SELECT COALESCE(CAST(h.PB_MFST_DATE AS TIMESTAMP),h.PB_DT_ENT) AS "Date",COALESCE(TRIM(s.CTS_CODE),'') AS Status,
            COALESCE(NULLIF(TRIM(h.PBORIG),''),TRIM(h.PB_LOCATION),'') AS Origin,COALESCE(TRIM(h.PBDEST),'') AS Destination,
            COALESCE(TRIM(e.EH_UNIT_ID),'') AS Truck,COALESCE(h.PB_CURRENT_DRIVER,h.PB_PLANNED_DRIVER) AS Driver,
            h.PB_NOTES AS Notes,CASE WHEN h.PB_DT_CANC IS NOT NULL THEN 1 ELSE 0 END AS Deleted
            FROM OPS_HEADER h LEFT JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS
            LEFT JOIN EQUIP_HEADER e ON e.EH_ID=COALESCE(h.PB_CURRENT_TRACTOR,h.PB_PLANNED_TRACTOR,h.PB_PLANNED_TRAILER)
            WHERE h.PB_ID=@id AND h.PB_TYPE='M'
            """,new{id},tx,cancellationToken:ct));
        if(row is null || row.Deleted!=0)
        {
            if(mapping is null)return [];
            var previous=JsonNode.Parse(mapping.Baseline)!.AsObject();return [new("manifest",id.ToString(),SyncHash(previous,true),previous,true)];
        }
        var members=(await db.QueryAsync<SyncManifestMember>(new CommandDefinition("""
            SELECT os.OPS_ORDER_ID AS OrderId,m.OSM_SEQUENCE AS StopNumber FROM OPS_STOP_REC route
            JOIN OPS_MANIFEST_STOPS m ON m.OSM_STOP_ID=route.OPS_STOP_ID JOIN OPS_STOP_REC os ON os.OPS_STOP_ID=m.OSM_PKUP_DELV_ID
            WHERE route.OPS_ORDER_ID=@id ORDER BY route.OPS_STOP_NUMBER,m.OSM_SEQUENCE
            """,new{id},tx,cancellationToken:ct))).ToArray();
        if(members.DistinctBy(m=>m.OrderId).Count()!=members.Length)throw new FormatException($"Manifest {id} has repeated order visits and requires review.");
        var changes=new List<NovaSyncSnapshot>();
        foreach(var member in members)
            changes.AddRange((await ReadSyncOrder(db,tx,member.OrderId,await Mapping("order",member.OrderId.ToString(),true,ct),ct)).Select(s=>s with{RequiredByCurrentWork=true}));
        await using var pg=await factory.OpenConnectionAsync(ct);
        var data=Baseline(mapping,SyncObject(new TmsManifest{Number=id.ToString(CultureInfo.InvariantCulture)}));data.Remove("Id");
        data["ManifestDate"]=(row.Date is {} date?DateOnly.FromDateTime(date):PlanningToday).ToString("yyyy-MM-dd");
        data["Status"]=row.Status switch{"DPCH"=>"Dispatched","ENR"=>"En Route","CPLT"=>"Complete",_=>"Planning"};data["Vehicle"]=row.Truck;
        var details=data["Details"]!.AsObject();details["OriginTerminal"]=row.Origin;details["DestinationTerminal"]=row.Destination;
        // Keep Aurora's structured planning metadata out of editable human notes.
        details["Notes"]=ManifestNotes.Read(row.Notes);
        details["OptimizationLocked"]=ManifestNotes.OptimizationLocked(row.Notes);
        details["ReturnDate"]=ManifestNotes.TripUntil(row.Notes)?.ToString("yyyy-MM-dd");
        var dispatchState=await DispatchDocument(db,tx,id,false,ct);
        details.Remove("SourceStatus");
        details["SourceReadOnlyReason"]=!dispatchState.CanEdit && row.Status is not ("DPCH" or "ENR" or "CPLT")
            ? $"This manifest is {dispatchState.Status} in Nova and cannot be edited here." : null;
        var driverMap=row.Driver is >0?await Mapping("driver",row.Driver.Value.ToString(),true,ct):null;
        details["DriverId"]=driverMap is null?null:JsonValue.Create(long.Parse(driverMap.LocalId,CultureInfo.InvariantCulture));
        var orders=new JsonArray();
        foreach(var member in members)
        {
            var orderMap=await Mapping("order",member.OrderId.ToString(),true,ct);
            var key=orderMap?.LocalId;var separator=key?.IndexOf('/')??-1;
            var source=separator>0?Guid.Parse(key![..separator]):await WorkspaceOrderSource.Get(pg,tenant.TenantId,changes.Single(s=>s.EntityType=="order"&&s.ExternalId==member.OrderId.ToString()).Data["TerminalCode"]?.GetValue<string>(),null,ct);
            orders.Add(new JsonObject{["SourceId"]=separator>0?key![..separator]:source.ToString(),["Id"]=separator>0?key![(separator+1)..]:$"FB-{member.OrderId}",["StopNumber"]=orders.Count+1});
        }
        data["Orders"]=orders;data["Route"]??=new JsonObject();
        foreach(var key in new[]{"OrderCount","Pieces","Weight","Editable"})data.Remove(key);
        changes.Add(new("manifest",id.ToString(CultureInfo.InvariantCulture),SyncHash(data),data){BusinessDate=row.Date is {} day?DateOnly.FromDateTime(day):null,Open=row.Status!="CPLT"});
        return changes;
    }
}

