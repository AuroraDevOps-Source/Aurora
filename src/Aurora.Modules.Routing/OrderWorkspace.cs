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
        Guid? terminalId = null;
        var terminalCode = RoutingInput.Text(root["reporting"]?["terminal"])?.Trim();
        if (!string.IsNullOrWhiteSpace(terminalCode))
        {
            if (terminalCode.Length > 80) throw new FormatException("Terminal codes allow up to 80 characters.");
            terminalId = await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("INSERT INTO aurora_terminal(tenant_id,code,name) VALUES(@TenantId,@terminalCode,@terminalCode) ON CONFLICT(tenant_id,lower(code)) DO UPDATE SET code=aurora_terminal.code WHERE aurora_terminal.deleted_at IS NULL RETURNING id", new {tenant.TenantId,terminalCode},tx,cancellationToken:ct));
            if (terminalId is null) throw new FormatException("The imported terminal has been deleted. Use an active terminal code.");
        }
        foreach(var id in ids)
        {
            var report = RoutingInput.ReportOrder(root,id);
            var appointment = RoutingInput.Describe(root,id);
            var time = (appointment?.Windows.Select(w => w.EarliestStart ?? w.LatestStart ?? w.LatestEnd).FirstOrDefault(t => t is not null) ?? fallback
                ?? throw new FormatException($"Order {id} needs a dated appointment or vehicle shift.")).ToUniversalTime();
            var customer = RoutingInput.Text(RoutingInput.Get(report,"carrierConsigneeName") ?? RoutingInput.Get(report,"shipToName")) ?? id;
            var city = RoutingInput.Text(RoutingInput.Get(report,"city")) ?? "";
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,terminal_id) VALUES (@TenantId,@source,@id,@time,@customer,@city,@terminalId)", new {tenant.TenantId,source,id,time,customer,city,terminalId},tx,cancellationToken:ct));
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
            WHERE tenant_id=@TenantId AND deleted_at IS NULL AND source_id=@source AND (@from IS NULL OR scheduled_at >= @from)
            AND (@to IS NULL OR scheduled_at <= @to) AND (@status IS NULL OR @status='' OR status=@status)
            ORDER BY scheduled_at,id
            """,new {tenant.TenantId,source,from=from?.ToUniversalTime(),to=to?.ToUniversalTime(),status},cancellationToken:ct));
        return rows.Select(r=>new WorkspaceOrderDto(r.Id,new DateTimeOffset(r.ScheduledAt),r.Customer,r.City,r.Status));
    }
    private sealed record OrderRow(string Id, DateTime ScheduledAt, string Customer, string City, string Status);
    public async Task<OrderDraftDto> CreateDraft(CreateOrderDraftDto input, string owner, CancellationToken ct)
    {
        if (input.Batch is not null && !PlanningBatch.All.Contains(input.Batch)) throw new FormatException("Unknown planning batch.");
        if (input.OrderIds is not { Length: > 0 }) throw new FormatException("Select at least one order to plan.");
        await using var db = await factory.OpenConnectionAsync(ct);
        var json = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT request::text FROM aurora_order_source WHERE tenant_id=@TenantId AND id=@SourceId",new{tenant.TenantId,input.SourceId},cancellationToken:ct)) ?? throw new FormatException("Import not found.");
        var selectedJson = RoutingInput.Parse(SelectOrders(json,input.OrderIds));
        var terminals = (await db.QueryAsync<string?>(new CommandDefinition("SELECT t.code FROM aurora_order o LEFT JOIN aurora_terminal t ON (t.tenant_id,t.id)=(o.tenant_id,o.terminal_id) AND t.deleted_at IS NULL WHERE o.tenant_id=@TenantId AND o.source_id=@SourceId AND o.id=ANY(@OrderIds) AND o.deleted_at IS NULL", new {tenant.TenantId,input.SourceId,input.OrderIds},cancellationToken:ct))).Distinct().ToArray();
        if (terminals.Length != 1 || terminals[0] is null) throw new FormatException("Select orders assigned to one terminal. Set Terminal on the orders first.");
        var importedTerminal = RoutingInput.Text(selectedJson["reporting"]?["terminal"])?.Trim();
        if (!string.Equals(importedTerminal, terminals[0], StringComparison.OrdinalIgnoreCase))
            throw new FormatException("These orders use an imported depot for a different terminal. Import matching terminal/depot data before planning them.");
        if (selectedJson["reporting"] is not JsonObject) selectedJson["reporting"] = new JsonObject();
        selectedJson["reporting"]!["terminal"] = terminals[0];
        // Re-classify on the server: the batch decides which orders go to a node instead of the consignee.
        var nodes = await new TerminalStore(factory, tenant).List(ct);
        var active = nodes.FirstOrDefault(n => string.Equals(n.Code, terminals[0], StringComparison.OrdinalIgnoreCase)) ?? throw new FormatException("The orders' terminal is no longer saved.");
        var orders = await new TmsStore(factory, tenant).OrdersById(input.SourceId, input.OrderIds, ct);
        var assignments = orders.Select(o => PlanningBatches.Classify(o, active, nodes)).ToArray();
        var batch = PlanningBatch.For(input.Batch, assignments);
        if (input.Batch is not null && assignments.FirstOrDefault(a => a.Batch != input.Batch) is { } moved)
            throw new FormatException($"Order {moved.OrderId} now belongs to the {PlanningBatch.Label(moved.Batch)} batch. Refresh Route Optimization and start again.");
        var destinations = assignments.Where(a => a.DestinationNodeId is not null).ToDictionary(a => a.OrderId, a => nodes.Single(n => n.Id == a.DestinationNodeId));
        selectedJson = RoutingInput.Parse(PlanningBatches.ApplyDestinations(selectedJson.ToJsonString(), destinations));
        var selected = FleetPlanning.Build(selectedJson.ToJsonString(), await new EquipmentStore(factory, tenant).List(ct));
        if (input.ShipDate is { } shipDate) selected = PlannerEdits.FleetDay(selected, shipDate, input.DepartAt ?? "06:00", input.ReturnBy ?? "18:00");
        var name = $"{PlanningBatch.Label(batch)} · {terminals[0]}" + (input.ShipDate is { } day ? $" · {day:MMM d}" : "");
        await using var tx = await db.BeginTransactionAsync(ct);
        // Lock the order rows so two planners cannot open plans on the same freight at once.
        var available = await db.QueryAsync<string>(new CommandDefinition("SELECT id FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=ANY(@OrderIds) AND status='ReadyToRoute' AND deleted_at IS NULL FOR UPDATE",new{tenant.TenantId,input.SourceId,input.OrderIds},tx,cancellationToken:ct));
        if(available.Count()!=input.OrderIds.Length) throw new FormatException("Some selected orders are already manifested. Refresh the grid.");
        var inProcess = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT count(*) FROM aurora_order o WHERE o.tenant_id=@TenantId AND o.source_id=@SourceId AND o.id=ANY(@OrderIds) AND " + PlanningLock.DraftForOrder + " IS NOT NULL",new{tenant.TenantId,input.SourceId,input.OrderIds},tx,cancellationToken:ct));
        if(inProcess>0) throw new FormatException($"{inProcess} of these orders are already in another open plan (Optimization in Process). Refresh Route Optimization.");
        var id=Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_planning_draft(tenant_id,id,owner_id,source_id,order_ids,request,name,batch) VALUES (@TenantId,@id,@owner,@SourceId,@OrderIds,CAST(@selected AS jsonb),@name,@batch)",new{tenant.TenantId,id,owner,input.SourceId,input.OrderIds,selected,name,batch},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return new(id,input.SourceId,name,selected,false,batch);
    }
    // Releases the plan's orders for other plans. Finished plans cannot be cancelled.
    public async Task CancelDraft(Guid id,string owner,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var changed=await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_planning_draft SET cancelled_at=now() WHERE tenant_id=@TenantId AND id=@id AND owner_id=@owner AND finished_session IS NULL AND cancelled_at IS NULL",new{tenant.TenantId,id,owner},cancellationToken:ct));
        if(changed==0 && (await Draft(id,owner,ct)).Finished) throw new FormatException("This plan already created manifests and cannot be cancelled.");
    }
    public async Task<OrderDraftDto> Draft(Guid id,string owner,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<OrderDraftDto>(new CommandDefinition("SELECT id, source_id AS SourceId, name AS FileName, request::text AS RequestJson, finished_session IS NOT NULL AS Finished, batch AS Batch, cancelled_at IS NOT NULL AS Cancelled FROM aurora_planning_draft WHERE tenant_id=@TenantId AND id=@id AND owner_id=@owner",new{tenant.TenantId,id,owner},cancellationToken:ct)) ?? throw new KeyNotFoundException("Planning draft not found.");
    }
    public async Task<string> ValidateDraft(Guid id,string owner,string request,CancellationToken ct)
    {
        var draft=await Draft(id,owner,ct);
        if(draft.Finished) throw new FormatException("This draft has already been finished. Start a new plan from Route Optimization.");
        if(draft.Cancelled) throw new FormatException("This plan was cancelled. Start a new plan from Route Optimization.");
        if(!OrderIds(draft.RequestJson).ToHashSet(StringComparer.Ordinal).SetEquals(OrderIds(request))) throw new FormatException("The plan must keep the orders it started with. Start a new plan from Route Optimization to change them.");
        var terminal=RoutingInput.Text(RoutingInput.Parse(draft.RequestJson)["reporting"]?["terminal"]) ?? "";
        return PlannerEdits.FitTrafficMode(FleetPlanning.ApplySavedFleet(request,await new EquipmentStore(factory,tenant).List(ct),terminal));
    }
    public async Task<IReadOnlyList<SavedManifestDto>> Manifests(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<ManifestRow>(new CommandDefinition("SELECT id, draft_id AS DraftId, vehicle, created_at AS CreatedAt, data::text AS Data FROM aurora_manifest WHERE tenant_id=@TenantId AND draft_id IS NOT NULL AND deleted_at IS NULL ORDER BY created_at DESC,vehicle",new{tenant.TenantId},cancellationToken:ct));
        return rows.Select(Saved).ToArray();
    }
    // Only manifests created by route optimization carry a route; manual manifests report not found.
    public async Task<SavedManifestDto> Manifest(Guid id, CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var row=await db.QuerySingleOrDefaultAsync<ManifestRow>(new CommandDefinition("SELECT id, draft_id AS DraftId, vehicle, created_at AS CreatedAt, data::text AS Data FROM aurora_manifest WHERE tenant_id=@TenantId AND id=@id AND draft_id IS NOT NULL AND deleted_at IS NULL",new{tenant.TenantId,id},cancellationToken:ct));
        return row is null ? throw new KeyNotFoundException("This manifest was not created by route optimization.") : Saved(row);
    }
    private static SavedManifestDto Saved(ManifestRow r)=>new(r.Id,r.DraftId,r.Vehicle,new DateTimeOffset(r.CreatedAt),JsonSerializer.Deserialize<RouteSummaryDto>(r.Data)!, JsonNode.Parse(r.Data)?["ManifestDetails"]?.Deserialize<ManifestDetailsDto>(), JsonNode.Parse(r.Data)?["ManifestRevision"]?.GetValue<int>() ?? 0);
    public async Task<SavedManifestDto> UpdateManifest(Guid id, UpdateManifestDto input, CancellationToken ct)
    {
        if (input.Details is null) throw new FormatException("Manifest details are required.");
        input.Details.Validate();
        await using var db=await factory.OpenConnectionAsync(ct);
        var changed=await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_manifest SET data=jsonb_set(jsonb_set(data,'{ManifestDetails}',CAST(@details AS jsonb)),
                '{ManifestRevision}',to_jsonb(@nextRevision::int)), details=CAST(@details AS jsonb),revision=revision+1
            WHERE tenant_id=@TenantId AND id=@id AND revision=@Revision AND draft_id IS NOT NULL AND deleted_at IS NULL
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
    public async Task<IReadOnlyList<SavedManifestDto>> Finish(Guid draftId,Guid session,string owner,OptimizationResultDto result,CancellationToken ct,string[]? vehicles=null)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var draft=await db.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition("SELECT source_id AS SourceId, order_ids AS OrderIds, finished_session AS FinishedSession, cancelled_at IS NOT NULL AS Cancelled FROM aurora_planning_draft WHERE tenant_id=@TenantId AND id=@draftId AND owner_id=@owner FOR UPDATE",new{tenant.TenantId,draftId,owner},tx,cancellationToken:ct)) ?? throw new KeyNotFoundException("Draft not found.");
        if(draft.FinishedSession is not null)
        {
            if(draft.FinishedSession!=session) throw new FormatException("This draft already created manifests from another result.");
            await tx.CommitAsync(ct);
            return (await Manifests(ct)).Where(m=>m.DraftId==draftId).ToArray();
        }
        if(draft.Cancelled) throw new FormatException("This plan was cancelled. Start a new plan from Route Optimization.");
        // Accepting only some routes leaves the other orders Ready to Ship; finishing releases the plan's lock.
        var assignments=Assignments(result,draft.OrderIds).Where(a=>vehicles is not { Length: > 0 } || vehicles.Contains(a.Route.Vehicle,StringComparer.Ordinal)).ToList();
        if(assignments.Count==0) throw new FormatException("Select at least one route to create manifests.");
        var scheduled=assignments.SelectMany(a=>a.Orders).Order(StringComparer.Ordinal).ToArray();
        var states=await db.QueryAsync<string>(new CommandDefinition("SELECT status FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=ANY(@scheduled) AND deleted_at IS NULL ORDER BY id FOR UPDATE",new{tenant.TenantId,draft.SourceId,scheduled},tx,cancellationToken:ct));
        if(states.Count()!=scheduled.Length || states.Any(s=>s!=WorkspaceOrderStatus.Ready)) throw new FormatException("Another plan already manifested some orders. Refresh Orders and plan the remaining orders.");
        foreach(var (route,ids) in assignments)
        {
            var id=Guid.NewGuid(); var data=JsonSerializer.Serialize(route);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest(tenant_id,id,draft_id,vehicle,data) VALUES (@TenantId,@id,@draftId,@Vehicle,CAST(@data AS jsonb))",new{tenant.TenantId,id,draftId,route.Vehicle,data},tx,cancellationToken:ct));
            foreach(var order in ids)
            {
                var stop=route.Pros.First(p=>p.ProNumber==order).StopNumber!.Value;
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) VALUES (@TenantId,@id,@SourceId,@order,@stop); UPDATE aurora_order SET status='Routed',revision=revision+1 WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@order",new{tenant.TenantId,id,draft.SourceId,order,stop},tx,cancellationToken:ct));
            }
        }
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_planning_draft SET finished_session=@session WHERE tenant_id=@TenantId AND id=@draftId",new{tenant.TenantId,session,draftId},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return (await Manifests(ct)).Where(m=>m.DraftId==draftId).ToArray();
    }
    private sealed class DraftRow { public Guid SourceId { get; set; } public string[] OrderIds { get; set; } = []; public Guid? FinishedSession { get; set; } public bool Cancelled { get; set; } }
}
