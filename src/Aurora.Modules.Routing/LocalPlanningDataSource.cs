using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

public sealed class LocalPlanningDataSource(NpgsqlConnectionFactory factory,ITenantContext tenant,TerminalStore terminals,EquipmentStore equipment,DriverStore drivers):ILocalPlanningDataSource
{
    public bool Enabled=>true;
    // The plan supplies its date explicitly. This fallback follows the tenant's
    // saved equipment offset, rather than determining appointments in SQL.
    public DateOnly PlanningToday=>DateOnly.FromDateTime(DateTime.UtcNow);
    public Task Lock(NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)=>db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"planning:{tenant.TenantId}"},tx,cancellationToken:ct));
    public async Task<TerminalDto[]> ReadTerminals(CancellationToken ct)=>(await terminals.List(ct)).ToArray();
    public Task<EquipmentCatalogDto> ReadFleet(CancellationToken ct)=>equipment.List(ct);
    static string Fingerprint(EquipmentCatalogDto catalog)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog))));

    public async Task<string> Snapshot(Guid sourceId,string[] ids,CancellationToken ct)
    {
        var orders=await new TmsStore(factory,tenant).OrdersById(sourceId,ids,ct);
        if(orders.Count!=ids.Length)throw new FormatException("Some orders no longer exist. Refresh the order list.");
        var codes=orders.Select(o=>o.TerminalCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(codes.Length!=1)throw new FormatException("Select orders from one terminal.");
        var savedTerminals=await terminals.List(ct);
        var node=savedTerminals.SingleOrDefault(t=>t.Code.Equals(codes[0],StringComparison.OrdinalIgnoreCase))??throw new FormatException("Save the orders' terminal before planning.");
        var root=RoutingInput.Parse(LocalPlanningSnapshot.Build(tenant.TenantId,sourceId,node,orders,DateTimeOffset.UtcNow));
        root["reporting"]!["fleetFingerprint"]=Fingerprint(await equipment.List(ct));
        root["reporting"]!["terminalRevisions"]=new JsonObject(savedTerminals.Select(t=>KeyValuePair.Create<string,JsonNode?>(t.Code,JsonValue.Create(t.Revision))));
        return root.ToJsonString();
    }
    public void ValidateSource(string json)
    {
        if(RoutingInput.Text(RoutingInput.Parse(json)["reporting"]?["auroraTenant"])!=tenant.TenantId.ToString())throw new FormatException("This plan belongs to a different workspace. Start a new plan.");
    }
    public Task ResolveNodeForPlanning(TerminalDto node,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(!DataRequirements.HasCoordinates(node.Latitude,node.Longitude))throw new RecordErrorException($"The address for terminal {node.Code} could not be located. Look up its coordinates and save again.",new("terminals",node.Code,node.Code));
        return Task.CompletedTask;
    }
    public Task<string> ResolveOrdersForPlanning(string json,CancellationToken ct){ct.ThrowIfCancellationRequested();ValidateSource(json);return Task.FromResult(json);}
    public string PrepareDraft(string json,TerminalDto node)
    {
        ValidateSource(json);
        var root=RoutingInput.Parse(json);
        var depot=RoutingInput.Items(root["locations"]).Single(l=>RoutingInput.Text(l["id"])=="DEPOT");
        depot["latitude"]=node.Latitude;depot["longitude"]=node.Longitude;
        foreach(var location in RoutingInput.Items(root["locations"]))
            if(!DataRequirements.HasCoordinates(location["latitude"]?.GetValue<double>(),location["longitude"]?.GetValue<double>()))
            {
                var id=RoutingInput.Text(location["id"])!;
                if(id=="DEPOT")throw new RecordErrorException($"The address for terminal {node.Code} could not be located. Look up its coordinates and save again.",new("terminals",node.Code,node.Code));
                throw new RecordErrorException($"The address for order {id} could not be located. Look up its coordinates and save again.",new("orders",id,id));
            }
        return root.ToJsonString();
    }
    public async Task RejectConsolidationOrders(string json,CancellationToken ct)
    {
        var codes=RoutingInput.Parse(json)["reporting"]?["orders"]?.AsObject().Select(p=>RoutingInput.Text(p.Value?["customerCode"])).OfType<string>().Distinct().ToArray()??[];
        await using var db=await factory.OpenConnectionAsync(ct);
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_customer WHERE tenant_id=@TenantId AND code=ANY(@codes) AND deleted_at IS NULL AND data#>>'{Planning,Consolidation}'='true')",new{tenant.TenantId,codes},cancellationToken:ct)))throw new FormatException("Some orders are held for customer consolidation. Release their consolidation manifest before planning.");
    }

    public async Task<(string Request,ReplanManifest[] Manifests)> IncludeOpenManifests(string request,TerminalDto origin,TerminalDto? destination,DateOnly date,CancellationToken ct)
    {
        var root=RoutingInput.Parse(request);
        var sourceId=Guid.Parse(RoutingInput.Text(root["reporting"]?["sourceId"])!);
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=(await db.QueryAsync<ReplanRow>(new CommandDefinition("""
            SELECT m.id,m.planning_id AS PlanningId,m.revision,
              ARRAY(SELECT a.order_id FROM aurora_manifest_order a WHERE a.tenant_id=m.tenant_id AND a.manifest_id=m.id ORDER BY a.stop_number) AS Orders
            FROM aurora_manifest m WHERE m.tenant_id=@TenantId AND m.deleted_at IS NULL AND m.status='Planning'
              AND m.manifest_date=@date AND m.details->>'OriginTerminal'=@origin
              AND COALESCE(m.details->>'DestinationTerminal','')=@destination
              AND COALESCE(m.details->>'OptimizationLocked','false')<>'true'
              AND NOT EXISTS(SELECT 1 FROM aurora_manifest_order a WHERE a.tenant_id=m.tenant_id AND a.manifest_id=m.id AND a.source_id<>@sourceId)
            ORDER BY m.planning_id
            """,new{tenant.TenantId,date=date.ToDateTime(TimeOnly.MinValue),origin=origin.Code,destination=destination?.Code??"",sourceId},cancellationToken:ct))).ToArray();
        var included=new List<ReplanManifest>();
        var known=OrderWorkspace.OrderIds(request).ToHashSet(StringComparer.Ordinal);
        foreach(var row in rows.Where(r=>r.Orders.Length>0))
        {
            if(row.Orders.Any(known.Contains))throw new FormatException("An order is already included in a manifest. Refresh before planning.");
            var added=RoutingInput.Parse(await Snapshot(sourceId,row.Orders,ct));
            foreach(var kind in new[]{"deliveries","pickups"})
            {
                root["orders"]![kind]??=new JsonArray();
                foreach(var order in RoutingInput.Items(added["orders"]?[kind]))root["orders"]![kind]!.AsArray().Add(order.DeepClone());
            }
            foreach(var location in RoutingInput.Items(added["locations"]).Where(l=>RoutingInput.Text(l["id"])!="DEPOT"))root["locations"]!.AsArray().Add(location.DeepClone());
            foreach(var report in added["reporting"]!["orders"]!.AsObject())root["reporting"]!["orders"]![report.Key]=report.Value?.DeepClone();
            included.Add(new(row.PlanningId,row.Revision.ToString(CultureInfo.InvariantCulture),row.Orders,date){LocalId=row.Id});
            known.UnionWith(row.Orders);
        }
        root["reporting"]!["replanManifests"]=JsonSerializer.SerializeToNode(included);
        return(root.ToJsonString(),included.ToArray());
    }
    async Task<Booking[]> Bookings(string json,CancellationToken ct)
    {
        var root=RoutingInput.Parse(json);var vehicles=RoutingInput.Items(root["vehicles"]).ToArray();
        var first=vehicles.Min(v=>RoutingInput.Time(v["start"]?["earliestStartTime"]))??throw new FormatException("Set departure times.");
        var last=vehicles.Max(v=>RoutingInput.Time(v["end"]?["latestEndTime"]))??throw new FormatException("Set return times.");
        var excluding=ManifestReplanning.Included(json).Select(m=>m.LocalId).OfType<Guid>().ToArray();
        await using var db=await factory.OpenConnectionAsync(ct);
        return (await db.QueryAsync<Booking>(new CommandDefinition("""
            SELECT vehicle, NULLIF(details->>'DriverId','')::bigint AS DriverId FROM aurora_manifest
            WHERE tenant_id=@TenantId AND deleted_at IS NULL AND status<>'Complete' AND NOT(id=ANY(@excluding))
              AND (status IN ('Dispatched','En Route','Arrived') OR manifest_date<=@last AND COALESCE(NULLIF(details->>'ReturnDate','')::date,manifest_date)>=@first)
            """,new{tenant.TenantId,excluding,first=first.Date,last=last.Date},cancellationToken:ct))).ToArray();
    }
    public async Task<string> ExcludeBookedTrucks(string request,DateOnly date,CancellationToken ct)
    {
        var booked=(await Bookings(request,ct)).Select(b=>b.Vehicle).ToHashSet(StringComparer.Ordinal);
        var root=RoutingInput.Parse(request);
        root["vehicles"]=new JsonArray(RoutingInput.Items(root["vehicles"]).Where(v=>!booked.Contains(RoutingInput.Text(v["id"])!)).Select(v=>v.DeepClone()).ToArray());
        if(!RoutingInput.Items(root["vehicles"]).Any())throw new FormatException("All eligible trucks are reserved by other manifests.");
        return root.ToJsonString();
    }
    public async Task<string> ApplyDrivers(string json,string basis,bool initialize,CancellationToken ct)=>DriverPlanning.Apply(json,await drivers.List(ct),(await Bookings(json,ct)).Select(b=>b.DriverId).OfType<long>().ToHashSet(),basis,initialize);

    public async Task ValidateCommit(string request,IReadOnlyList<RouteSummaryDto> routes,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        ValidateSource(request);
        var root=RoutingInput.Parse(request);var reports=root["reporting"]!["orders"]!.AsObject();
        var source=Guid.Parse(RoutingInput.Text(root["reporting"]!["sourceId"])!);
        var ids=routes.SelectMany(r=>r.Pros).Where(p=>p.StopNumber is not null).Select(p=>p.ProNumber).Distinct().Order(StringComparer.Ordinal).ToArray();
        var revisions=(await db.QueryAsync<OrderRevision>(new CommandDefinition("SELECT id,revision FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@source AND id=ANY(@ids) AND deleted_at IS NULL ORDER BY id FOR UPDATE",new{tenant.TenantId,source,ids},tx,cancellationToken:ct))).ToDictionary(r=>r.Id);
        if(ids.Any(id=>!revisions.TryGetValue(id,out var row)||reports[id]?["localRevision"]?.GetValue<int>()!=row.Revision))throw new FormatException("An order changed after this plan was started. Start a new plan using its current data.");
        foreach(var manifest in ManifestReplanning.Included(request))
        {
            var revision=await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("SELECT revision FROM aurora_manifest WHERE tenant_id=@TenantId AND id=@id AND deleted_at IS NULL AND status='Planning' AND COALESCE(details->>'OptimizationLocked','false')<>'true' FOR UPDATE",new{tenant.TenantId,id=manifest.LocalId},tx,cancellationToken:ct));
            if(revision?.ToString(CultureInfo.InvariantCulture)!=manifest.Revision)throw new FormatException("An included manifest changed. Start a new plan.");
        }
        if(RoutingInput.Text(root["reporting"]?["fleetFingerprint"])!=Fingerprint(await equipment.List(ct)))throw new FormatException("Equipment changed after this plan was started. Start a new plan with its current capacity and costs.");
        var savedTerminals=root["reporting"]?["terminalRevisions"]?.AsObject()??throw new FormatException("This plan has no terminal revision snapshot.");
        var currentTerminals=(await terminals.List(ct)).ToDictionary(t=>t.Code);
        foreach(var code in new[]{RoutingInput.Text(root["reporting"]?["terminal"]),RoutingInput.Text(root["reporting"]?["lineHaulTo"])}.OfType<string>())
            if(!currentTerminals.TryGetValue(code,out var currentTerminal)||savedTerminals[code]?.GetValue<int>()!=currentTerminal.Revision)throw new FormatException("A route terminal changed after planning. Start a new plan with its current address and routing rules.");
        var choices=root["reporting"]?["drivers"]?.AsObject()??throw new FormatException("Choose drivers before creating manifests.");
        var savedDrivers=(await drivers.List(ct)).ToDictionary(d=>d.Id.ToString(CultureInfo.InvariantCulture));
        foreach(var route in routes)
        {
            var choice=choices[route.Vehicle];
            if(!savedDrivers.TryGetValue(RoutingInput.Text(choice?["id"])??"",out var driver)||driver.Revision!=RoutingInput.Text(choice?["revision"]))throw new FormatException("A driver changed after optimization. Run optimization again before creating manifests.");
        }
        root["vehicles"]=new JsonArray(RoutingInput.Items(root["vehicles"]).Where(v=>routes.Any(r=>r.Vehicle==RoutingInput.Text(v["id"]))).Select(v=>v.DeepClone()).ToArray());
        var selectedRequest=root.ToJsonString();
        var booked=await Bookings(selectedRequest,ct);
        if(routes.Any(r=>booked.Any(b=>b.Vehicle==r.Vehicle)))throw new FormatException("A selected truck is now reserved by another manifest.");
        await ApplyDrivers(selectedRequest,DriverCostBasis.Separate,false,ct);
    }
    public Task<Dictionary<string,long>> Export(Guid draftId,Guid sessionId,string draftJson,IReadOnlyList<RouteSummaryDto> routes,CancellationToken ct,bool rollbackOnly=false)=>throw new InvalidOperationException("Local manifests are committed in Aurora and exported through the outbox.");
    sealed class ReplanRow {public Guid Id{get;set;} public long PlanningId{get;set;} public int Revision{get;set;} public string[] Orders{get;set;}=[];}
    sealed record Booking(string Vehicle,long? DriverId);
    sealed record OrderRevision(string Id,int Revision);
}
