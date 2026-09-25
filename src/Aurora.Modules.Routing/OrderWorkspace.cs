using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class OrderWorkspace(NpgsqlConnectionFactory factory, ITenantContext tenant)
{
    public static string[] OrderIds(string json)
    {
        var root = RoutingInput.Parse(json);
        if (RoutingInput.Items(root["orders"]?["pickups"]).Any() || RoutingInput.Items(root["orders"]?["pickupDeliveries"]).Any())
            throw new FormatException("This workspace supports delivery orders only.");
        var ids = RoutingInput.Items(root["orders"]?["deliveries"]).Select(o => RoutingInput.Text(o["id"]) ?? throw new FormatException("Every order needs an ID.")).ToArray();
        if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new FormatException("Provide distinct delivery IDs.");
        return ids;
    }
    public static string SelectOrders(string json, string[] ids)
    {
        var known = OrderIds(json).ToHashSet(StringComparer.Ordinal);
        if (ids.Length == 0 || ids.Length > 5000 || ids.Distinct().Count() != ids.Length || ids.Any(id => !known.Contains(id)))
            throw new FormatException("Select between 1 and 5,000 distinct orders from this import.");
        var root = RoutingInput.Parse(json);
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        root["orders"]!["deliveries"] = new JsonArray(RoutingInput.Items(root["orders"]?["deliveries"]).Where(o => selected.Contains(RoutingInput.Text(o["id"])!)).Select(o => o.DeepClone()).ToArray());
        root.Remove("routes");
        if (root["reporting"] is JsonObject report)
        {
            var kept = new JsonObject();
            foreach (var id in ids) if (RoutingInput.ReportOrder(root, id) is { } item) kept[id] = item.DeepClone();
            report["orders"] = kept;
        }
        return root.ToJsonString();
    }
    public async Task Import(SeedOrdersDto input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150 || input.RequestJson.Length > 25 * 1024 * 1024) throw new FormatException("Provide a name and a file up to 25 MB.");
        var ids = OrderIds(input.RequestJson);
        if(ids.Length > 5000) throw new FormatException("Import at most 5,000 orders at a time.");
        var root = RoutingInput.Parse(RoutingInput.Prepare(input.RequestJson));
        var fallback = RoutingInput.Items(root["vehicles"]).Select(v => RoutingInput.Time(v["start"]?["earliestStartTime"] ?? v["start"]?["earliestStart"])).FirstOrDefault(t => t is not null);
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var source = Guid.NewGuid();
        var added = await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES (@TenantId,@source,@Name,CAST(@RequestJson AS jsonb)) ON CONFLICT(tenant_id,name) DO NOTHING", new { tenant.TenantId, source, input.Name, input.RequestJson }, tx, cancellationToken:ct));
        if(added == 0) { await tx.CommitAsync(ct); return; }
        foreach(var id in ids)
        {
            var report = RoutingInput.ReportOrder(root,id);
            var appointment = RoutingInput.Describe(root,id);
            var time = (appointment?.Windows.Select(w => w.EarliestStart ?? w.LatestStart ?? w.LatestEnd).FirstOrDefault(t => t is not null) ?? fallback
                ?? throw new FormatException($"Order {id} needs a dated appointment or vehicle shift.")).ToUniversalTime();
            var customer = RoutingInput.Text(RoutingInput.Get(report,"carrierConsigneeName") ?? RoutingInput.Get(report,"shipToName")) ?? id;
            var city = RoutingInput.Text(RoutingInput.Get(report,"city")) ?? "";
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city) VALUES (@TenantId,@source,@id,@time,@customer,@city)", new {tenant.TenantId,source,id,time,customer,city},tx,cancellationToken:ct));
        }
        await tx.CommitAsync(ct);
    }
    public async Task<IEnumerable<OrderSourceDto>> Sources(CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        return await db.QueryAsync<OrderSourceDto>(new CommandDefinition("SELECT id, name FROM aurora_order_source WHERE tenant_id=@TenantId ORDER BY name",new {tenant.TenantId},cancellationToken:ct));
    }
    public async Task<IEnumerable<WorkspaceOrderDto>> Orders(Guid source, DateTimeOffset? from, DateTimeOffset? to, string? status, CancellationToken ct)
    {
        if(from > to) throw new FormatException("End must be after start.");
        if(!string.IsNullOrEmpty(status) && !WorkspaceOrderStatus.All.Contains(status)) throw new FormatException("Unknown order status.");
        await using var db = await factory.OpenConnectionAsync(ct);
        var rows = await db.QueryAsync<OrderRow>(new CommandDefinition("""
            SELECT id, scheduled_at AS ScheduledAt, customer, city, status FROM aurora_order
            WHERE tenant_id=@TenantId AND source_id=@source AND (@from IS NULL OR scheduled_at >= @from)
            AND (@to IS NULL OR scheduled_at <= @to) AND (@status IS NULL OR @status='' OR status=@status)
            ORDER BY scheduled_at,id
            """,new {tenant.TenantId,source,from=from?.ToUniversalTime(),to=to?.ToUniversalTime(),status},cancellationToken:ct));
        return rows.Select(r=>new WorkspaceOrderDto(r.Id,new DateTimeOffset(r.ScheduledAt),r.Customer,r.City,r.Status));
    }
    private sealed record OrderRow(string Id, DateTime ScheduledAt, string Customer, string City, string Status);
    public async Task<OrderDraftDto> CreateDraft(CreateOrderDraftDto input, string owner, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        var json = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT request::text FROM aurora_order_source WHERE tenant_id=@TenantId AND id=@SourceId",new{tenant.TenantId,input.SourceId},cancellationToken:ct)) ?? throw new FormatException("Import not found.");
        var selected = FleetPlanning.Build(SelectOrders(json,input.OrderIds), await new EquipmentStore(factory, tenant).List(ct));
        var available = await db.QueryAsync<string>(new CommandDefinition("SELECT id FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=ANY(@OrderIds) AND status='ReadyToRoute'",new{tenant.TenantId,input.SourceId,input.OrderIds},cancellationToken:ct));
        if(available.Count()!=input.OrderIds.Length) throw new FormatException("Some selected orders are already manifested. Refresh the grid.");
        var id=Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_planning_draft(tenant_id,id,owner_id,source_id,order_ids,request) VALUES (@TenantId,@id,@owner,@SourceId,@OrderIds,CAST(@selected AS jsonb))",new{tenant.TenantId,id,owner,input.SourceId,input.OrderIds,selected},cancellationToken:ct));
        return new(id,input.SourceId,"Selected orders.json",selected,false);
    }
    public async Task<OrderDraftDto> Draft(Guid id,string owner,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<OrderDraftDto>(new CommandDefinition("SELECT id, source_id AS SourceId, 'Selected orders.json' AS FileName, request::text AS RequestJson, finished_session IS NOT NULL AS Finished FROM aurora_planning_draft WHERE tenant_id=@TenantId AND id=@id AND owner_id=@owner",new{tenant.TenantId,id,owner},cancellationToken:ct)) ?? throw new KeyNotFoundException("Planning draft not found.");
    }
    public async Task<string> ValidateDraft(Guid id,string owner,string request,CancellationToken ct)
    {
        var draft=await Draft(id,owner,ct);
        if(draft.Finished) throw new FormatException("This draft has already been finished. Start a new selection from Orders.");
        if(!OrderIds(draft.RequestJson).ToHashSet(StringComparer.Ordinal).SetEquals(OrderIds(request))) throw new FormatException("The wizard must keep the orders selected in the grid. Start a new selection to change them.");
        var terminal=RoutingInput.Text(RoutingInput.Parse(draft.RequestJson)["reporting"]?["terminal"]) ?? "";
        return FleetPlanning.ApplySavedFleet(request,await new EquipmentStore(factory,tenant).List(ct),terminal);
    }
    public async Task<IReadOnlyList<SavedManifestDto>> Manifests(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<ManifestRow>(new CommandDefinition("SELECT id, draft_id AS DraftId, vehicle, created_at AS CreatedAt, data::text AS Data FROM aurora_manifest WHERE tenant_id=@TenantId ORDER BY created_at DESC,vehicle",new{tenant.TenantId},cancellationToken:ct));
        return rows.Select(r=>new SavedManifestDto(r.Id,r.DraftId,r.Vehicle,new DateTimeOffset(r.CreatedAt),JsonSerializer.Deserialize<RouteSummaryDto>(r.Data)!, JsonNode.Parse(r.Data)?["ManifestDetails"]?.Deserialize<ManifestDetailsDto>(), JsonNode.Parse(r.Data)?["ManifestRevision"]?.GetValue<int>() ?? 0)).ToArray();
    }
    public async Task<SavedManifestDto> UpdateManifest(Guid id, UpdateManifestDto input, CancellationToken ct)
    {
        if (input.Details is null) throw new FormatException("Manifest details are required.");
        input.Details.Validate();
        await using var db=await factory.OpenConnectionAsync(ct);
        var changed=await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_manifest SET data=jsonb_set(jsonb_set(data,'{ManifestDetails}',CAST(@details AS jsonb)),
                '{ManifestRevision}',to_jsonb(@nextRevision::int))
            WHERE tenant_id=@TenantId AND id=@id AND COALESCE((data->>'ManifestRevision')::int,0)=@Revision
            """,new {tenant.TenantId,id,details=JsonSerializer.Serialize(input.Details),input.Revision,nextRevision=input.Revision+1},cancellationToken:ct));
        if(changed==0) {
            if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest WHERE tenant_id=@TenantId AND id=@id)",new{tenant.TenantId,id},cancellationToken:ct))) throw new KeyNotFoundException("Manifest not found.");
            throw new FormatException("This manifest changed in another window. Cancel and refresh before editing again.");
        }
        return (await Manifests(ct)).Single(m=>m.Id==id);
    }
    private sealed record ManifestRow(Guid Id, Guid DraftId,string Vehicle,DateTime CreatedAt,string Data);
    public static IReadOnlyList<(RouteSummaryDto Route, string[] Orders)> Assignments(OptimizationResultDto result, string[] selected)
    {
        if(result.Status!="SUCCEEDED" || result.SummaryError is not null) throw new FormatException("Only a completed, readable result can be saved.");
        var allowed=selected.ToHashSet(StringComparer.Ordinal); var used=new HashSet<string>(StringComparer.Ordinal);
        var routes=new List<(RouteSummaryDto,string[])>();
        foreach(var route in result.Routes)
        {
            var ids=route.Pros.OrderBy(p=>p.StopNumber).Select(p=>p.ProNumber).Distinct(StringComparer.Ordinal).ToArray();
            if(ids.Length==0) continue;
            if(ids.Any(id=>!allowed.Contains(id) || !used.Add(id))) throw new FormatException("Result contains an unknown or multiply assigned order.");
            if(route.Pros.Any(p=>p.StopNumber is null)) throw new FormatException("Result is missing stop sequence information.");
            routes.Add((route,ids));
        }
        if(routes.Count==0 || used.Count != result.Summary?.Scheduled) throw new FormatException("Result does not contain a complete scheduled-order manifest. Review the result before saving.");
        return routes;
    }
    public async Task<IReadOnlyList<SavedManifestDto>> Finish(Guid draftId,Guid session,string owner,OptimizationResultDto result,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var draft=await db.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition("SELECT source_id AS SourceId, order_ids AS OrderIds, finished_session AS FinishedSession FROM aurora_planning_draft WHERE tenant_id=@TenantId AND id=@draftId AND owner_id=@owner FOR UPDATE",new{tenant.TenantId,draftId,owner},tx,cancellationToken:ct)) ?? throw new KeyNotFoundException("Draft not found.");
        if(draft.FinishedSession is not null)
        {
            if(draft.FinishedSession!=session) throw new FormatException("This draft already created manifests from another result.");
            await tx.CommitAsync(ct);
            return (await Manifests(ct)).Where(m=>m.DraftId==draftId).ToArray();
        }
        var assignments=Assignments(result,draft.OrderIds);
        var scheduled=assignments.SelectMany(a=>a.Orders).Order(StringComparer.Ordinal).ToArray();
        var states=await db.QueryAsync<string>(new CommandDefinition("SELECT status FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=ANY(@scheduled) ORDER BY id FOR UPDATE",new{tenant.TenantId,draft.SourceId,scheduled},tx,cancellationToken:ct));
        if(states.Count()!=scheduled.Length || states.Any(s=>s!=WorkspaceOrderStatus.Ready)) throw new FormatException("Another plan already manifested some orders. Refresh Orders and plan the remaining orders.");
        foreach(var (route,ids) in assignments)
        {
            var id=Guid.NewGuid(); var data=JsonSerializer.Serialize(route);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest(tenant_id,id,draft_id,vehicle,data) VALUES (@TenantId,@id,@draftId,@Vehicle,CAST(@data AS jsonb))",new{tenant.TenantId,id,draftId,route.Vehicle,data},tx,cancellationToken:ct));
            foreach(var order in ids)
            {
                var stop=route.Pros.First(p=>p.ProNumber==order).StopNumber!.Value;
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) VALUES (@TenantId,@id,@SourceId,@order,@stop); UPDATE aurora_order SET status='Routed' WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@order",new{tenant.TenantId,id,draft.SourceId,order,stop},tx,cancellationToken:ct));
            }
        }
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_planning_draft SET finished_session=@session WHERE tenant_id=@TenantId AND id=@draftId",new{tenant.TenantId,session,draftId},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return (await Manifests(ct)).Where(m=>m.DraftId==draftId).ToArray();
    }
    private sealed class DraftRow { public Guid SourceId { get; set; } public string[] OrderIds { get; set; } = []; public Guid? FinishedSession { get; set; } }
}
