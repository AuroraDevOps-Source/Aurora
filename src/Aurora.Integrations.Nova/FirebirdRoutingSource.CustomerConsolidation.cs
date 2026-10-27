using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Npgsql;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    // Caller holds the company advisory lock. Native membership is authoritative:
    // a refresh retry only sees stops that remain unmanifested, so cannot append twice.
    internal async Task AccumulateConsolidation(NpgsqlConnection pg,NpgsqlTransaction pgTx,SourceOrder[] rows,List<string> warnings,CancellationToken ct)
    {
        var selected=rows.Where(r=>r.StopKind=="D"&&r.Policy?.Consolidation==true).OrderBy(r=>r.NativeId).ToArray();
        if(selected.Length==0)return;
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        foreach(var order in selected)
        {
            var policy=order.Policy!;
            if(await pg.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_order_routing WHERE tenant_id=@TenantId AND source_key=@SourceKey AND order_id=@id AND receiving_terminal IS NOT NULL)",new{tenant.TenantId,Options.SourceKey,id=order.NativeId},pgTx,cancellationToken:ct)))
            {warnings.Add($"Order {order.Pro}: choose consignee delivery instead of a receiving terminal before customer consolidation.");continue;}
            var reserved=await pg.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_planning_draft WHERE tenant_id=@TenantId AND @id=ANY(order_ids) AND finished_session IS NULL AND cancelled_at IS NULL)",new{tenant.TenantId,id=order.Id},pgTx,cancellationToken:ct));
            if(reserved){warnings.Add($"Order {order.Pro}: cancel its open route plan before consolidation.");continue;}
            if(order.Weight is not >0 || order.PlanningCube is not >0 || string.IsNullOrWhiteSpace(order.Address))
            {warnings.Add($"Order {order.Pro}: consolidation is waiting for weight, cubes or a delivery address. Enter actual values or a customer cube estimate.");continue;}
            if(!ConsolidationCapacity.Fits(0,0,order.Weight.Value,order.PlanningCube.Value,policy.MaximumWeight!.Value,policy.MaximumCubes!.Value))
            {warnings.Add($"Order {order.Pro} exceeds the customer's consolidation capacity and needs review.");continue;}
            // Recheck the source values under native locks before assigning freight.
            var header=await db.QuerySingleOrDefaultAsync<ConsolidationOrder>(new CommandDefinition("SELECT PB_STATUS AS StatusId,PB_ID AS Id,PBTWEIGH AS Weight,PB_TTL_CUBE AS Cubes,PBTPIECE AS Pieces,TRIM(PB_LOCATION) AS Terminal,PB_DT_CANC AS Cancelled FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='O' WITH LOCK",new{id=order.NativeId},tx,cancellationToken:ct));
            var stop=await db.QuerySingleOrDefaultAsync<ConsolidationStop>(new CommandDefinition("SELECT OPS_MANIFEST_NUMBER AS Manifest,OPS_DELIVERED_TIME AS Delivered,TRIM(OPS_ACCTID) AS Customer,OPS_ADDR1 AS Address,OPS_CITY AS City,OPS_STATE AS State,OPS_ZIP AS Zip FROM OPS_STOP_REC WHERE OPS_STOP_ID=@id AND OPS_ORDER_ID=@orderId AND OPS_TYPE='D' AND OPS_DISPATCH_TIME IS NULL AND OPS_DEPART_TIME IS NULL WITH LOCK",new{id=order.StopId,orderId=order.NativeId},tx,cancellationToken:ct));
            var key=ConsolidationCapacity.AddressKey(order.Address,order.City,order.State,order.PostalCode);
            if(header is null||stop is null||header.Cancelled is not null||header.StatusId!=order.StatusId||stop.Delivered is not null||stop.Manifest is >0||header.Terminal!=order.Terminal||header.Weight!=order.Weight||header.Cubes!=order.Cube||header.Pieces!=order.Pieces||stop.Customer!=order.CustomerCode||ConsolidationCapacity.AddressKey(stop.Address,stop.City,stop.State,stop.Zip)!=key)
                throw new FormatException("A consolidation order changed while loading. Refresh Orders to retry.");
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_PKUP_DELV_ID=@id",new{id=order.StopId},tx,cancellationToken:ct))>0)continue;
            var prefix="aurora-cc:"+StableId($"{tenant.TenantId}:{Options.SourceKey}:{order.Terminal}:{order.CustomerCode}:{key}").ToString("N")+":";
            var candidates=await db.QueryAsync<long>(new CommandDefinition("SELECT PB_ID FROM OPS_HEADER WHERE PB_TYPE='M' AND PB_DT_CANC IS NULL AND PB_EXTERNAL_REF STARTING WITH @prefix ORDER BY PB_ID",new{prefix},tx,cancellationToken:ct));
            long manifest=0;CustomerConsolidation? metadata=null;string? stored=null;
            foreach(var candidate in candidates)
            {
                var h=await DispatchHeader(db,tx,candidate,true,ct);var info=ManifestNotes.Consolidation(h.Notes);
                if(info is not {Held:true}||h.Driver is not null||h.Truck is not null||h.Terminal!=order.Terminal||h.Origin!=order.Terminal||h.Destination!=order.Terminal||info.Customer!=order.CustomerCode||info.AddressKey!=key)continue;
                var status=await db.ExecuteScalarAsync<string>(new CommandDefinition("SELECT TRIM(CTS_CODE) FROM CT_STAT WHERE CTS_ID=@id",new{id=h.StatusId},tx,cancellationToken:ct));
                if(status!="UNAS"||await ConsolidationStarted(db,tx,candidate,ct))continue;
                var load=await ConsolidationLoad(db,tx,candidate,info,ct,true);
                if(load is null||!ConsolidationCapacity.Fits(load.Weight,load.Cubes,order.Weight.Value,order.PlanningCube.Value,info.MaximumWeight,info.MaximumCubes))continue;
                manifest=candidate;metadata=info;stored=h.Notes;break;
            }
            var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
            if(manifest==0)
            {
                await ValidTerminal(db,tx,order.Terminal,ct);
                manifest=await NextId(db,tx,true,ct);
                metadata=new(order.CustomerCode!,key,policy.EquipmentType,policy.MaximumWeight!.Value,policy.MaximumCubes!.Value,true,[]);
                stored=ManifestNotes.SetPlanningLock(ManifestNotes.WithConsolidation("",metadata),true);
                await db.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE,PB_EXTERNAL_REF,PB_NOTES)
                    VALUES(@manifest,'M','M',@terminal,@terminal,@terminal,1,@now,@day,@reference,@stored)
                    """,new{manifest,terminal=order.Terminal,now,day=now.Date,reference=prefix+Guid.NewGuid().ToString("N"),stored},tx,cancellationToken:ct));
                var routing=await NextId(db,tx,false,ct);
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION) VALUES(@routing,@manifest,1,'L',@terminal)",new{routing,manifest,terminal=order.Terminal},tx,cancellationToken:ct));
            }
            if(order.Cube is not >0)metadata!.EstimatedCubes[order.StopId]=order.PlanningCube.Value;
            var routeStop=await db.QuerySingleAsync<long>(new CommandDefinition("SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@manifest AND OPS_STOP_NUMBER=1",new{manifest},tx,cancellationToken:ct));
            var sequence=await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COALESCE(MAX(OSM_SEQUENCE),0)+1 FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=@routeStop",new{routeStop},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_SEQUENCE,OSM_PKUP_DELV_ID) VALUES(@routeStop,@sequence,@stop)",new{routeStop,sequence,stop=order.StopId},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=@manifest,OPS_MANIFESTED_TIME=@now WHERE OPS_STOP_ID=@stop",new{manifest,now,stop=order.StopId},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_NOTES=@notes WHERE PB_ID=@manifest",new{manifest,notes=ManifestNotes.WithConsolidation(stored,metadata!)},tx,cancellationToken:ct));
        }
        await tx.CommitAsync(ct);
    }
    private static async Task<bool> ConsolidationStarted(FbConnection db,FbTransaction tx,long id,CancellationToken ct)=>await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_STOP_REC WHERE (OPS_ORDER_ID=@id OR OPS_STOP_ID IN (SELECT l.OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS l JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=l.OSM_STOP_ID WHERE r.OPS_ORDER_ID=@id)) AND (OPS_DISPATCH_TIME IS NOT NULL OR OPS_DEPART_TIME IS NOT NULL OR OPS_DELIVERED_TIME IS NOT NULL)",new{id},tx,cancellationToken:ct))>0;
    private static async Task<ConsolidationProgress?> ConsolidationLoad(FbConnection db,FbTransaction tx,long id,CustomerConsolidation info,CancellationToken ct,bool locked=false)
    {
        var members=await DispatchMembers(db,tx,id,locked,ct);
        var terminal=await db.ExecuteScalarAsync<string>(new CommandDefinition("SELECT TRIM(PB_LOCATION) FROM OPS_HEADER WHERE PB_ID=@id",new{id},tx,cancellationToken:ct));
        if(members.Any(m=>m.OrderTerminal!=terminal))return null;
        if(info.Held && await ConsolidationStarted(db,tx,id,ct))return null;
        if(members.Any(m=>m.Cancelled is not null||m.Completed||m.ManifestId!=id||m.StopKind!="D"||m.Weight is not >0||(!(m.Cubes>0)&&!info.EstimatedCubes.ContainsKey(m.Stop))))return null;
        var addresses=await db.QueryAsync<ConsolidationStop>(new CommandDefinition("SELECT s.OPS_MANIFEST_NUMBER AS Manifest,s.OPS_DELIVERED_TIME AS Delivered,TRIM(s.OPS_ACCTID) AS Customer,s.OPS_ADDR1 AS Address,s.OPS_CITY AS City,s.OPS_STATE AS State,s.OPS_ZIP AS Zip FROM OPS_STOP_REC s JOIN OPS_MANIFEST_STOPS l ON l.OSM_PKUP_DELV_ID=s.OPS_STOP_ID JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=l.OSM_STOP_ID WHERE r.OPS_ORDER_ID=@id",new{id},tx,cancellationToken:ct));
        if(addresses.Any(a=>a.Customer!=info.Customer||ConsolidationCapacity.AddressKey(a.Address,a.City,a.State,a.Zip)!=info.AddressKey))return null;
        return new(info.Customer,info.Held,members.Sum(m=>(decimal)m.Weight!.Value),members.Sum(m=>m.Cubes is >0?(decimal)m.Cubes.Value:info.EstimatedCubes[m.Stop]),info.MaximumWeight,info.MaximumCubes,members.Count(m=>m.Cubes is not >0));
    }
    public async Task<NovaDispatchDocument> ReleaseConsolidation(long id,ConsolidationRelease input,CancellationToken ct)
    {
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        var h=await DispatchHeader(db,tx,id,true,ct);var current=await DispatchDocument(db,tx,id,false,ct);
        var info=ManifestNotes.Consolidation(h.Notes)??throw new FormatException("This is not a customer consolidation manifest.");
        if(!info.Held)return current;
        if(current.Revision!=input.Revision||!current.CanEdit)throw new FormatException("The manifest changed. Refresh before releasing it.");
        var load=await ConsolidationLoad(db,tx,id,info,ct,true);
        if(current.Issues.Any(i=>!i.StartsWith("Release this consolidation manifest",StringComparison.Ordinal))||load is null||current.OrderCount==0||load.Weight>info.MaximumWeight||load.Cubes>info.MaximumCubes)throw new FormatException("Review the manifest's orders and capacity before releasing it.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_NOTES=@notes,PB_MFST_DATE=@day WHERE PB_ID=@id",new{id,notes=ManifestNotes.WithConsolidation(h.Notes,info with{Held=false}),day=PlanningToday.ToDateTime(TimeOnly.MinValue)},tx,cancellationToken:ct));
        var saved=await DispatchDocument(db,tx,id,false,ct);await tx.CommitAsync(ct);return saved;
    }
    private sealed class ConsolidationOrder { public long StatusId {get;set;} public long Id {get;set;} public decimal? Weight {get;set;} public decimal? Cubes {get;set;} public decimal? Pieces {get;set;} public string Terminal {get;set;}=""; public DateTime? Cancelled {get;set;} }
    private sealed class ConsolidationStop { public long? Manifest {get;set;} public DateTime? Delivered {get;set;} public string? Customer {get;set;} public string? Address {get;set;} public string? City {get;set;} public string? State {get;set;} public string? Zip {get;set;} }
}
