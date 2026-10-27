using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    public DateOnly PlanningToday => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,Options.TimeZoneId));

    // Trucks committed to loads outside this plan are unavailable, including locked
    // planned loads and dispatched loads from an earlier day.
    internal static async Task<string[]> BookedTrucks(FbConnection db,FbTransaction tx,long[] included,DateOnly date,CancellationToken ct)
    {
        var rows=await db.QueryAsync<TruckBooking>(new CommandDefinition("""
            SELECT TRIM(e.EH_UNIT_ID) AS Unit,h.PB_MFST_DATE AS Starts,h.PB_NOTES AS Notes,TRIM(s.CTS_CODE) AS Status,
                (SELECT COUNT(*) FROM OPS_STOP_REC r WHERE r.OPS_ORDER_ID=h.PB_ID AND (r.OPS_DISPATCH_TIME IS NOT NULL OR r.OPS_DEPART_TIME IS NOT NULL)) AS Started FROM OPS_HEADER h
            LEFT JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS
            LEFT JOIN DRIVER_PKUP_DELV d ON d.DPD_LOAD_NUMBER=h.PB_ID
            JOIN EQUIP_HEADER e ON e.EH_ID=h.PB_CURRENT_TRACTOR OR e.EH_ID=h.PB_PLANNED_TRAILER OR e.EH_ID=h.PB_PLANNED_TRACTOR OR e.EH_ID=d.DPD_ID
            WHERE h.PB_TYPE='M' AND h.PB_DT_CANC IS NULL AND h.PB_ID NOT IN @included
                AND COALESCE(s.CTS_CODE,'') NOT IN ('CPLT','VOID','DONE')
                AND (CAST(h.PB_MFST_DATE AS DATE)=@day OR s.CTS_CODE NOT IN ('UNAS','ASG')
                    OR (h.PB_MFST_DATE<=@day AND h.PB_NOTES CONTAINING 'Aurora trip until v1 ')
                    OR EXISTS(SELECT 1 FROM OPS_STOP_REC r WHERE r.OPS_ORDER_ID=h.PB_ID AND (r.OPS_DISPATCH_TIME IS NOT NULL OR r.OPS_DEPART_TIME IS NOT NULL)))
            """,new{included=included.Length==0?new long[]{-1}:included,day=date.ToDateTime(TimeOnly.MinValue)},tx,cancellationToken:ct));
        return rows.Where(r=>r.Started>0||r.Status is not ("UNAS" or "ASG")||r.Starts?.Date==date.ToDateTime(TimeOnly.MinValue)||r.Starts?.Date<=date.ToDateTime(TimeOnly.MinValue)&&ManifestNotes.TripUntil(r.Notes)>=date).Select(r=>r.Unit).Distinct().ToArray();
    }

    private sealed class TruckBooking { public string Unit {get;set;}=""; public DateTime? Starts {get;set;} public string? Notes {get;set;} public string? Status {get;set;} public long Started {get;set;} }

    public async Task<string> ExcludeBookedTrucks(string request,DateOnly date,CancellationToken ct)
    {
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        var root=RoutingInput.Parse(request);
        var last=RoutingInput.Items(root["vehicles"]).Select(v=>RoutingInput.Time(v["end"]?["latestEndTime"])).Where(t=>t is not null).Select(t=>DateOnly.FromDateTime(t!.Value.DateTime)).DefaultIfEmpty(date).Max();
        if(last.DayNumber-date.DayNumber>14)throw new FormatException("Plan at most 14 days at a time.");
        var busy=new HashSet<string>();
        for(var day=date;day<=last;day=day.AddDays(1))busy.UnionWith(await BookedTrucks(db,tx,ManifestReplanning.Included(request).Select(m=>m.Id).ToArray(),day,ct));
        foreach(var vehicle in RoutingInput.Items(root["vehicles"]).ToArray())
            if(busy.Contains(RoutingInput.Text(vehicle["id"])!))root["vehicles"]!.AsArray().Remove(vehicle);
        if(!RoutingInput.Items(root["vehicles"]).Any())throw new FormatException("No trucks are available for this plan. Other manifests have reserved the available trucks.");
        return root.ToJsonString();
    }

    public async Task<NovaDispatchDocument> SavePlanningLock(long id,ManifestPlanningLockEdit input,CancellationToken ct)
    {
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        var header=await DispatchHeader(db,tx,id,true,ct);
        var current=await DispatchDocument(db,tx,id,false,ct);
        if(ManifestNotes.Consolidation(header.Notes) is not null)throw new FormatException("Customer consolidation manifests always stay outside route optimization.");
        if(current.Revision!=input.Revision)throw new FormatException("This manifest changed. Refresh before changing its optimization lock.");
        if(!current.CanEdit)throw new FormatException("Dispatched or started manifests are already excluded from optimization.");
        if(current.OptimizationLocked!=input.Locked)
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_NOTES=@notes WHERE PB_ID=@id",new{id,notes=ManifestNotes.SetPlanningLock(header.Notes,input.Locked)},tx,cancellationToken:ct));
        var result=await DispatchDocument(db,tx,id,false,ct);await tx.CommitAsync(ct);return result;
    }

    internal sealed record ReplanSnapshot(ReplanManifest Manifest,SourceOrder[] Orders);
    internal async Task<ReplanSnapshot?> Snapshot(FbConnection db,FbTransaction tx,long id,bool locked,CancellationToken ct)
    {
        var header=await DispatchHeader(db,tx,id,locked,ct);
        var status=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT TRIM(CTS_CODE) FROM CT_STAT WHERE CTS_ID=@StatusId",header,tx,cancellationToken:ct));
        var routing=(await db.QueryAsync<ManifestRoutingStop>(new CommandDefinition("SELECT OPS_STOP_ID AS Id,OPS_STOP_NUMBER AS Sequence,TRIM(OPS_MFST_LOC_TYPE) AS Kind,TRIM(OPS_MFST_LOCATION) AS Location FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id ORDER BY OPS_STOP_NUMBER",new{id},tx,cancellationToken:ct))).ToArray();
        var started=await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND (OPS_DISPATCH_TIME IS NOT NULL OR OPS_DEPART_TIME IS NOT NULL OR OPS_DELIVERED_TIME IS NOT NULL)",new{id},tx,cancellationToken:ct));
        if(header.Cancelled is not null || ManifestNotes.Consolidation(header.Notes) is not null || ManifestNotes.OptimizationLocked(header.Notes) || status is not ("UNAS" or "ASG") || started>0)return null;
        var members=await DispatchMembers(db,tx,id,locked,ct);
        var links=await DispatchLinks(db,tx,id,ct);
        if(members.Any(m=>m.Completed || m.Cancelled is not null || m.ManifestId!=id || m.OrderTerminal!=header.Origin))return null;
        var endpoints=new ManifestEndpoints(header.Origin,header.Destination,header.Origin!=header.Destination);
        await VerifyManifest(db,tx,id,endpoints,members.Select(m=>m.Stop).ToArray(),ct);
        var rows=(await db.QueryAsync<SourceOrder>(new CommandDefinition("""
            SELECT h.PB_ID AS NativeId,h.PBNUM AS Pro,TRIM(h.PB_LOCATION) AS Terminal,TRIM(s.OPS_ACCTID) AS CustomerCode,h.PBTPIECE AS Pieces,h.PBTWEIGH AS Weight,h.PB_TTL_CUBE AS Cube,h.PBBOL AS Reference,
                s.OPS_STOP_ID AS StopId,TRIM(s.OPS_TYPE) AS StopKind,s.OPS_NAME AS Customer,s.OPS_ADDR1 AS Address,s.OPS_CITY AS City,s.OPS_STATE AS State,s.OPS_ZIP AS PostalCode,
                s.OPS_LATITUDE AS Latitude,s.OPS_LONGITUDE AS Longitude,s.OPS_APPT_LOW AS AppointmentFrom,s.OPS_APPT_HIGH AS AppointmentTo
            FROM OPS_STOP_REC r JOIN OPS_MANIFEST_STOPS l ON l.OSM_STOP_ID=r.OPS_STOP_ID
            JOIN OPS_STOP_REC s ON s.OPS_STOP_ID=l.OSM_PKUP_DELV_ID JOIN OPS_HEADER h ON h.PB_ID=s.OPS_ORDER_ID
            WHERE r.OPS_ORDER_ID=@id ORDER BY r.OPS_STOP_NUMBER,l.OSM_SEQUENCE
            """,new{id},tx,cancellationToken:ct))).ToArray();
        if(rows.Any(r=>r.StopKind is not ("P" or "D")) || rows.Select(r=>r.Id).Distinct().Count()!=rows.Length)return null;
        var vehicle=header.Truck is {} truck ? await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT TRIM(EH_UNIT_ID) FROM EQUIP_HEADER WHERE EH_ID=@truck AND EH_DELETE_DATE IS NULL",new{truck},tx,cancellationToken:ct)) : null;
        if(header.Date is null || links.Length>1 || links.Any(l=>l.Active!="A" || l.Driver!=header.Driver))return null;
        var state=(await db.QueryAsync(new CommandDefinition("""
            SELECT h.PB_ID,h.PB_STATUS,h.PB_CURRENT_DRIVER,h.PB_CURRENT_TRACTOR,s.OPS_STOP_ID,s.OPS_DISPATCH_TIME,s.OPS_DEPART_TIME,s.OPS_STATUS
            FROM OPS_STOP_REC r JOIN OPS_MANIFEST_STOPS l ON l.OSM_STOP_ID=r.OPS_STOP_ID
            JOIN OPS_STOP_REC s ON s.OPS_STOP_ID=l.OSM_PKUP_DELV_ID JOIN OPS_HEADER h ON h.PB_ID=s.OPS_ORDER_ID
            WHERE r.OPS_ORDER_ID=@id ORDER BY s.OPS_STOP_ID
            """,new{id},tx,cancellationToken:ct))).ToArray();
        if(state.Any(r=>r.OPS_DISPATCH_TIME is not null || r.OPS_DEPART_TIME is not null))return null;
        var revision=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{header,routing,members,links,rows,status,vehicle,state}))));
        return new(new(id,revision,rows.Select(r=>r.Id).ToArray(),DateOnly.FromDateTime(header.Date.Value)),rows);
    }

    public async Task<(string Request,ReplanManifest[] Manifests)> IncludeOpenManifests(string request,TerminalDto origin,TerminalDto? destination,DateOnly date,CancellationToken ct)
    {
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        var ids=await db.QueryAsync<long>(new CommandDefinition("""
            SELECT h.PB_ID FROM OPS_HEADER h JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS
            WHERE h.PB_TYPE='M' AND h.PB_DT_CANC IS NULL AND h.PB_LOCATION=@origin AND h.PBORIG=@origin AND h.PBDEST=@destination
                AND CAST(h.PB_MFST_DATE AS DATE)=@date AND s.CTS_CODE IN ('UNAS','ASG') ORDER BY h.PB_ID
            """,new{origin=origin.Code,destination=destination?.Code??origin.Code,date=date.ToDateTime(TimeOnly.MinValue)},tx,cancellationToken:ct));
        var snapshots=new List<ReplanSnapshot>();
        foreach(var id in ids)if(await Snapshot(db,tx,id,false,ct) is {} snapshot)snapshots.Add(snapshot);
        var root=RoutingInput.Parse(request);
        var existing=snapshots.SelectMany(s=>s.Orders).ToArray();
        if(existing.Any(o=>!string.IsNullOrWhiteSpace(o.CustomerCode)))
        {
            await using var pg=await factory.OpenConnectionAsync(ct);
            var policies=await CustomerPolicies(pg,null,ct);
            foreach(var order in existing)order.Policy=policies.GetValueOrDefault(order.CustomerCode??"");
        }
        if(existing.Length>0)
        {
            if(existing.Select(r=>r.Id).Distinct().Count()!=existing.Length)throw new FormatException("An order appears on multiple open manifests. Reconcile it before planning.");
            var extra=BuildRequest(origin,existing);
            var current=OrderWorkspace.OrderIds(request).ToHashSet();
            foreach(var kind in new[]{"deliveries","pickups"})
            {
                root["orders"]![kind]??=new JsonArray();
                foreach(var order in RoutingInput.Items(extra["orders"]?[kind]))
                {
                    if(!current.Add(RoutingInput.Text(order["id"])!))throw new FormatException("An order selected for planning is already on an included manifest. Refresh Orders.");
                    root["orders"]![kind]!.AsArray().Add(order.DeepClone());
                }
            }
            foreach(var location in RoutingInput.Items(extra["locations"]).Where(l=>RoutingInput.Text(l["id"])!="DEPOT"))root["locations"]!.AsArray().Add(location.DeepClone());
            foreach(var pair in extra["reporting"]!["orders"]!.AsObject())root["reporting"]!["orders"]![pair.Key]=pair.Value!.DeepClone();
        }
        var included=snapshots.Select(s=>s.Manifest).ToArray();
        root["reporting"]!["replanManifests"]=JsonSerializer.SerializeToNode(included);
        return(root.ToJsonString(),included);
    }

    internal async Task<Dictionary<string,long>> PrepareManifestReplacement(FbConnection db,FbTransaction tx,string saved,IReadOnlyList<RouteSummaryDto> routes,CancellationToken ct)
    {
        var included=ManifestReplanning.Included(saved);
        ManifestReplanning.ValidateResult(saved,routes);
        foreach(var manifest in included.OrderBy(m=>m.Id))
        {
            var current=await Snapshot(db,tx,manifest.Id,true,ct);
            if(current is null || current.Manifest.Revision!=manifest.Revision)throw new FormatException($"Manifest {manifest.Id} changed or was locked/dispatched while planning. Nothing was saved; start a new plan.");
        }
        var available=included.OrderBy(m=>m.Id).ToList();var reused=new Dictionary<string,long>();
        foreach(var route in routes.OrderBy(r=>r.Vehicle,StringComparer.Ordinal))
        {
            var candidate=available.FirstOrDefault();
            if(candidate is not null){reused.Add(route.Vehicle,candidate.Id);available.Remove(candidate);}
        }

        var unassigned=await db.QuerySingleAsync<long>(new CommandDefinition("SELECT CTS_ID FROM CT_STAT WHERE CTS_CODE='UNAS'",transaction:tx,cancellationToken:ct));
        foreach(var manifest in included)
        {
            // Accepting a new plan releases previous dispatch assignments. The resulting
            // manifest has planned equipment and must be assigned/released again.
            await db.ExecuteAsync(new CommandDefinition("DELETE FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=@id",new{id=manifest.Id},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_PLANNED_DRIVER=NULL,PB_PLANNED_DRIVER2=NULL,PB_CURRENT_DRIVER=NULL,PB_CURRENT_TRACTOR=NULL,PB_PLANNED_TRAILER=NULL,PB_PLANNED_TRACTOR=NULL,PB_STATUS=@unassigned,PB_EXTERNAL_REF=NULL WHERE PB_ID=@id",new{id=manifest.Id,unassigned},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL,OPS_MANIFESTED_TIME=NULL
                WHERE OPS_STOP_ID IN(SELECT l.OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS l JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=l.OSM_STOP_ID WHERE r.OPS_ORDER_ID=@id)
                """,new{id=manifest.Id},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID IN(SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id)",new{id=manifest.Id},tx,cancellationToken:ct));
        }
        return reused;
    }
}
