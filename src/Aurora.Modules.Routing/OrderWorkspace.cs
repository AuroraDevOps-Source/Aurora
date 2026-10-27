using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class OrderWorkspace(NpgsqlConnectionFactory factory, ITenantContext tenant, IPlanningDataSource? firebird = null)
{
    public static string[] OrderIds(string json)
    {
        var root = RoutingInput.Parse(json);
        if (RoutingInput.Items(root["orders"]?["pickupDeliveries"]).Any())
            throw new FormatException("Paired pickup-and-delivery orders are not supported yet.");
        var ids = RoutingInput.Orders(root).Select(o => RoutingInput.Text(o["id"]) ?? throw new FormatException("Every order needs an ID.")).ToArray();
        if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new FormatException("Provide distinct order IDs.");
        return ids;
    }
    public static string SelectOrders(string json, string[] ids)
    {
        var known = OrderIds(json).ToHashSet(StringComparer.Ordinal);
        if (ids.Length == 0 || ids.Length > 5000 || ids.Distinct().Count() != ids.Length || ids.Any(id => !known.Contains(id)))
            throw new FormatException("Select between 1 and 5,000 distinct orders from this import.");
        var root = RoutingInput.Parse(json);
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        foreach(var kind in new[]{"deliveries","pickups"})
            if(root["orders"]?[kind] is JsonArray)
                root["orders"]![kind] = new JsonArray(RoutingInput.Items(root["orders"]?[kind]).Where(o=>selected.Contains(RoutingInput.Text(o["id"])!)).Select(o=>o.DeepClone()).ToArray());
        root.Remove("routes");
        // Source snapshots contain every order's location. Keep only locations used by
        // this plan so address lookup cannot visit an unselected order without its report.
        var usedLocations = new HashSet<string>(StringComparer.Ordinal);
        void CollectLocations(JsonNode? node)
        {
            if (node is JsonObject obj)
                foreach (var property in obj)
                {
                    if (property.Key == "locationId" && RoutingInput.Text(property.Value) is { } locationId)
                        usedLocations.Add(locationId);
                    else CollectLocations(property.Value);
                }
            else if (node is JsonArray array)
                foreach (var child in array) CollectLocations(child);
        }
        CollectLocations(root["orders"]);
        CollectLocations(root["vehicles"]);
        CollectLocations(root["depots"]);
        root["locations"] = new JsonArray(RoutingInput.Items(root["locations"])
            .Where(l => usedLocations.Contains(RoutingInput.Text(l["id"])!))
            .Select(l => l.DeepClone()).ToArray());
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
        return await db.QueryAsync<OrderSourceDto>(new CommandDefinition("SELECT id, name FROM aurora_order_source WHERE tenant_id=@TenantId AND aurora_source_in_workspace(tenant_id,id) ORDER BY name",new {tenant.TenantId},cancellationToken:ct));
    }
    public async Task<IEnumerable<WorkspaceOrderDto>> Orders(Guid source, DateTimeOffset? from, DateTimeOffset? to, string? status, CancellationToken ct)
    {
        if(from > to) throw new FormatException("End must be after start.");
        if(!string.IsNullOrEmpty(status) && !WorkspaceOrderStatus.All.Contains(status)) throw new FormatException("Unknown order status.");
        await using var db = await factory.OpenConnectionAsync(ct);
        var rows = await db.QueryAsync<OrderRow>(new CommandDefinition("""
            SELECT id, scheduled_at AS ScheduledAt, customer, city, status FROM aurora_order
            WHERE tenant_id=@TenantId AND deleted_at IS NULL AND aurora_source_in_workspace(tenant_id,source_id) AND source_id=@source AND (@from IS NULL OR scheduled_at >= @from)
            AND (@to IS NULL OR scheduled_at <= @to) AND (@status IS NULL OR @status='' OR status=@status)
            ORDER BY scheduled_at,id
            """,new {tenant.TenantId,source,from=from?.ToUniversalTime(),to=to?.ToUniversalTime(),status},cancellationToken:ct));
        return rows.Select(r=>new WorkspaceOrderDto(r.Id,new DateTimeOffset(r.ScheduledAt),r.Customer,r.City,r.Status));
    }
    private sealed record OrderRow(string Id, DateTime ScheduledAt, string Customer, string City, string Status);
    public async Task<OrderDraftDto> CreateDraft(CreateOrderDraftDto input, string owner, CancellationToken ct)
    {
        var drafts = await CreatePlans(input, owner, ct, single: true);
        return drafts[0];
    }
    public async Task<IReadOnlyList<OrderDraftDto>> CreatePlans(CreateOrderDraftDto input, string owner, CancellationToken ct, bool single = false)
    {
        if (input.Batch is not null && !PlanningBatch.All.Contains(input.Batch)) throw new FormatException("Unknown planning batch.");
        if (input.ToTerminalId is not null && input.Batch is not (PlanningBatch.LineHaul or PlanningBatch.Consolidation)) throw new FormatException("A receiving terminal is only valid for a transfer plan.");
        if (input.OrderIds is not { Length: > 0 } || input.OrderIds.Length > 5000 || input.OrderIds.Distinct().Count() != input.OrderIds.Length)
            throw new FormatException("Select between 1 and 5,000 distinct orders.");
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var source = firebird ?? throw new FormatException("Configure a Nova connection first.");
        if (source.Enabled) await source.Lock(db, tx, ct);
        var orders = (await new TmsStore(factory, tenant).OrdersById(input.SourceId, input.OrderIds, ct)).ToArray();
        if (orders.Any(o => o.Status != WorkspaceOrderStatus.Ready || o.ManifestId is not null || o.OptimizationDraftId is not null))
            throw new FormatException("Some selected orders are no longer available. Refresh Orders and review your selection.");
        var nodes = await source.ReadTerminals(ct);
        var codes = orders.Select(o => o.TerminalCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (codes.Length != 1 || codes[0] is null) throw new FormatException("Select orders from one terminal to plan routes.");
        var origin = nodes.SingleOrDefault(n => n.Code.Equals(codes[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new FormatException("The orders' terminal is no longer saved.");
        // Preserve the existing explicit-transfer API; Orders uses server-classified automatic groups.
        var groups = single && input.ToTerminalId is not null ? new[] { input } : PlanningBatches.GroupPlans(input, orders, origin, nodes);
        if (single && groups.Length != 1)
            throw new FormatException("These orders need separate plans for local deliveries and receiving terminals. Start them together from Orders.");
        var drafts = new List<OrderDraftDto>();
        foreach (var group in groups) drafts.Add(await CreateDraftCore(group, owner, db, tx, ct));
        await tx.CommitAsync(ct);
        return drafts;
    }
    private async Task<OrderDraftDto> CreateDraftCore(CreateOrderDraftDto input, string owner, Npgsql.NpgsqlConnection db, Npgsql.NpgsqlTransaction tx, CancellationToken ct)
    {
        if (input.Batch is not null && !PlanningBatch.All.Contains(input.Batch)) throw new FormatException("Unknown planning batch.");
        if(input.Batch==PlanningBatch.LineHaul && input.ToTerminalId is null)throw new FormatException("Select the receiving terminal for Line haul.");
        if(input.ToTerminalId is not null && input.Batch is not (PlanningBatch.LineHaul or PlanningBatch.Consolidation))throw new FormatException("A receiving terminal is only valid for a transfer plan.");
        if (input.OrderIds is not { Length: > 0 }) throw new FormatException("Select at least one order to plan.");
        var json = firebird is ILocalPlanningDataSource localSource ? await localSource.Snapshot(input.SourceId,input.OrderIds,ct)
            : await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT request::text FROM aurora_order_source WHERE tenant_id=@TenantId AND id=@SourceId",new{tenant.TenantId,input.SourceId},cancellationToken:ct)) ?? throw new FormatException("Import not found.");
        var selectedJson = RoutingInput.Parse(SelectOrders(json,input.OrderIds));
        var terminals = (await db.QueryAsync<string?>(new CommandDefinition("SELECT COALESCE(o.details->>'TerminalCode',t.code) FROM aurora_order o LEFT JOIN aurora_terminal t ON (t.tenant_id,t.id)=(o.tenant_id,o.terminal_id) AND t.deleted_at IS NULL WHERE o.tenant_id=@TenantId AND o.source_id=@SourceId AND o.id=ANY(@OrderIds) AND o.deleted_at IS NULL", new {tenant.TenantId,input.SourceId,input.OrderIds},cancellationToken:ct))).Distinct().ToArray();
        if (terminals.Length != 1 || terminals[0] is null) throw new FormatException("Select orders assigned to one terminal. Set Terminal on the orders first.");
        var importedTerminal = RoutingInput.Text(selectedJson["reporting"]?["terminal"])?.Trim();
        if (!string.Equals(importedTerminal, terminals[0], StringComparison.OrdinalIgnoreCase))
            throw new FormatException("These orders use an imported depot for a different terminal. Import matching terminal/depot data before planning them.");
        if (selectedJson["reporting"] is not JsonObject) selectedJson["reporting"] = new JsonObject();
        selectedJson["reporting"]!["terminal"] = terminals[0];
        // Re-classify on the server: the batch decides which orders go to a node instead of the consignee.
        var nodes = await (firebird ?? throw new FormatException("Configure a Nova connection first.")).ReadTerminals(ct);
        var active = nodes.FirstOrDefault(n => string.Equals(n.Code, terminals[0], StringComparison.OrdinalIgnoreCase)) ?? throw new FormatException("The orders' terminal is no longer saved.");
        var receiving=input.ToTerminalId is {} toId ? nodes.SingleOrDefault(n=>n.Id==toId) ?? throw new FormatException("The receiving terminal is no longer saved.") : null;
        await firebird.ResolveNodeForPlanning(active, ct);
        if (receiving is not null) await firebird.ResolveNodeForPlanning(receiving, ct);
        await firebird.RejectConsolidationOrders(selectedJson.ToJsonString(),ct);
        var planningDate=input.ShipDate??firebird.PlanningToday;
        var replan=await firebird.IncludeOpenManifests(selectedJson.ToJsonString(),active,receiving,planningDate,ct);
        selectedJson=RoutingInput.Parse(replan.Request);
        if(replan.Manifests.Length>0)
        {
            var reserved=await db.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM aurora_planning_draft d WHERE d.tenant_id=@TenantId AND d.source_id=@SourceId
                    AND d.finished_session IS NULL AND d.cancelled_at IS NULL
                    AND EXISTS(SELECT 1 FROM jsonb_array_elements(COALESCE(d.request#>'{reporting,replanManifests}','[]'::jsonb)) m WHERE (m->>'Id')::bigint=ANY(@manifestIds))
                """,new{tenant.TenantId,input.SourceId,manifestIds=replan.Manifests.Select(m=>m.Id).ToArray()},tx,cancellationToken:ct));
            if(reserved>0)throw new FormatException("An open manifest for this trip is already in another plan. Finish or cancel that plan first.");
        }
        if(receiving is not null)selectedJson=RoutingInput.Parse(LineHaulPlanning.Prepare(selectedJson.ToJsonString(),active,receiving));
        if (RoutingInput.Text(selectedJson["reporting"]?["firebirdSource"]) is not null)
            selectedJson = RoutingInput.Parse(await firebird.ResolveOrdersForPlanning(selectedJson.ToJsonString(), ct));
        var orders = await new TmsStore(factory, tenant).OrdersById(input.SourceId, input.OrderIds, ct);
        var assignments = orders.Select(o => receiving is null ? PlanningBatches.Classify(o, active, nodes) : new BatchAssignment(o.Id,PlanningBatch.TransferTo(receiving),receiving.Id,$"Transfer to {receiving.Code}")).ToArray();
        var batch = PlanningBatch.For(input.Batch, assignments);
        if (input.Batch is not null && assignments.FirstOrDefault(a => a.Batch != input.Batch) is { } moved)
            throw new FormatException($"Order {moved.OrderId} now belongs to the {PlanningBatch.Label(moved.Batch)} batch. Refresh Optimization and start again.");
        var destinations = assignments.Where(a => a.DestinationNodeId is not null).ToDictionary(a => a.OrderId, a => nodes.Single(n => n.Id == a.DestinationNodeId));
        foreach (var destination in destinations.Values.DistinctBy(n => n.Id)) await firebird.ResolveNodeForPlanning(destination, ct);
        selectedJson = RoutingInput.Parse(PlanningBatches.ApplyDestinations(selectedJson.ToJsonString(), destinations));
        selectedJson = RoutingInput.Parse(firebird.PrepareDraft(selectedJson.ToJsonString(), active));
        var fleet = await (firebird ?? throw new FormatException("Configure a Nova connection first.")).ReadFleet(ct);
        var selected = FleetPlanning.Build(selectedJson.ToJsonString(), fleet);
        selected=LineHaulPlanning.SetFleetDestination(selected);
        selected = PlannerEdits.FleetDay(selected, planningDate, input.DepartAt ?? "06:00", input.ReturnBy ?? "18:00",input.ReturnDate);
        selected = MultiDayPlanning.Apply(selected,planningDate,input.ReturnDate??planningDate,input.ReturnBy??"18:00",input.RestRules);
        selected = await firebird.ExcludeBookedTrucks(selected,planningDate,ct);
        var driverSetup=RoutingInput.Parse(selected);
        driverSetup["reporting"]!["drivers"]=new JsonObject();
        driverSetup["reporting"]!["driverCostBasis"]=DriverCostBasis.Separate;
        selected=FleetPlanning.DefaultDrivers(driverSetup.ToJsonString(),fleet);
        var name = $"{PlanningBatch.Label(batch)} · {terminals[0]}" + (receiving is not null ? $" → {receiving.Code}" : "") + (input.ShipDate is { } day ? $" · {day:MMM d}" : "");
        // Lock the order rows so two planners cannot open plans on the same freight at once.
        var available = await db.QueryAsync<string>(new CommandDefinition("SELECT id FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=ANY(@OrderIds) AND status='ReadyToRoute' AND deleted_at IS NULL FOR UPDATE",new{tenant.TenantId,input.SourceId,input.OrderIds},tx,cancellationToken:ct));
        if(available.Count()!=input.OrderIds.Length) throw new FormatException("Some selected orders are already manifested. Refresh the grid.");
        var inProcess = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT count(*) FROM aurora_order o WHERE o.tenant_id=@TenantId AND o.source_id=@SourceId AND o.id=ANY(@OrderIds) AND " + PlanningLock.DraftForOrder + " IS NOT NULL",new{tenant.TenantId,input.SourceId,input.OrderIds},tx,cancellationToken:ct));
        if(inProcess>0) throw new FormatException($"{inProcess} of these orders are already in another open plan (Optimization in Process). Refresh Optimization.");
        var id=Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_planning_draft(tenant_id,id,owner_id,source_id,order_ids,request,name,batch) VALUES (@TenantId,@id,@owner,@SourceId,@OrderIds,CAST(@selected AS jsonb),@name,@batch)",new{tenant.TenantId,id,owner,input.SourceId,OrderIds=OrderIds(selected),selected,name,batch},tx,cancellationToken:ct));
        return new(id,input.SourceId,name,selected,false,batch);
    }
    // Releases the plan's orders for other plans. Finished plans cannot be cancelled.
    public async Task<IEnumerable<OpenOrderDraftDto>> OpenDrafts(string owner, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        return await db.QueryAsync<OpenOrderDraftDto>(new CommandDefinition(
            "SELECT d.id, d.name, cardinality(d.order_ids) AS OrderCount FROM aurora_planning_draft d WHERE d.tenant_id=@TenantId AND d.owner_id=@owner AND aurora_source_in_workspace(d.tenant_id,d.source_id) AND " + PlanningLock.Open + " ORDER BY d.created_at DESC",
            new { tenant.TenantId, owner }, cancellationToken: ct));
    }
    public async Task CancelDraft(Guid id,string owner,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var changed=await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_planning_draft SET cancelled_at=now() WHERE tenant_id=@TenantId AND id=@id AND owner_id=@owner AND finished_session IS NULL AND cancelled_at IS NULL",new{tenant.TenantId,id,owner},cancellationToken:ct));
        if(changed==0 && (await Draft(id,owner,ct)).Finished) throw new FormatException("This plan already created manifests and cannot be cancelled.");
    }
    public async Task<OrderDraftDto> Draft(Guid id,string owner,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var draft = await db.QuerySingleOrDefaultAsync<OrderDraftDto>(new CommandDefinition("SELECT id, source_id AS SourceId, name AS FileName, request::text AS RequestJson, finished_session IS NOT NULL AS Finished, batch AS Batch, cancelled_at IS NOT NULL AS Cancelled FROM aurora_planning_draft WHERE tenant_id=@TenantId AND id=@id AND owner_id=@owner",new{tenant.TenantId,id,owner},cancellationToken:ct)) ?? throw new KeyNotFoundException("Planning draft not found.");
        if(!draft.Finished && !draft.Cancelled)
        {
            var source=firebird ?? throw new FormatException("Configure a Nova connection first.");
            source.ValidateSource(draft.RequestJson);
            var terminal=RoutingInput.Text(RoutingInput.Parse(draft.RequestJson)["reporting"]?["terminal"]) ?? "";
            // Do not display the retired demo fleet when reopening a pre-migration draft.
            FleetPlanning.ApplySavedFleet(draft.RequestJson,await source.ReadFleet(ct),terminal);
            var setup=RoutingInput.Parse(draft.RequestJson);
            setup["reporting"]!["driverCostBasis"]=DriverCostBasis.Separate;
            draft=draft with {RequestJson=setup.ToJsonString()};
        }
        return draft;
    }
    public async Task<string> ValidateDraft(Guid id,string owner,string request,CancellationToken ct)
    {
        var draft=await Draft(id,owner,ct);
        if(draft.Finished) throw new FormatException("This draft has already been finished. Start a new plan from Optimization.");
        if(draft.Cancelled) throw new FormatException("This plan was cancelled. Start a new plan from Optimization.");
        if(!OrderIds(draft.RequestJson).ToHashSet(StringComparer.Ordinal).SetEquals(OrderIds(request))) throw new FormatException("The plan must keep the orders it started with. Start a new plan from Optimization to change them.");
        (firebird ?? throw new FormatException("Configure a Nova connection first.")).ValidateSource(draft.RequestJson);
        var terminal=RoutingInput.Text(RoutingInput.Parse(draft.RequestJson)["reporting"]?["terminal"]) ?? "";
        request=LineHaulPlanning.RestoreDestination(draft.RequestJson,request);
        request=ManifestReplanning.Protect(draft.RequestJson,request);
        await firebird!.RejectConsolidationOrders(draft.RequestJson,ct);
        var root=RoutingInput.Parse(request);var saved=RoutingInput.Parse(draft.RequestJson);
        root["reporting"]!["tripRestRules"]=saved["reporting"]?["tripRestRules"]?.DeepClone();

        root["reporting"]!["driverCostBasis"]=DriverCostBasis.Separate;
        if(root["reporting"]?["drivers"] is JsonObject choices)
            foreach(var choice in choices.Select(p=>p.Value).OfType<JsonObject>())choice.Remove("maximumWorkingSeconds");
        var rebuilt=MultiDayPlanning.ApplyRest(PlannerEdits.FitTrafficMode(FleetPlanning.ApplySavedFleet(root.ToJsonString(),await firebird!.ReadFleet(ct),terminal)));
        return await firebird.ApplyDrivers(rebuilt,DriverCostBasis.Separate,false,ct);
    }
    public async Task<IReadOnlyList<SavedManifestDto>> Manifests(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<ManifestRow>(new CommandDefinition("SELECT id, draft_id AS DraftId, vehicle, created_at AS CreatedAt, data::text AS Data, number,details::text AS Details,revision FROM aurora_manifest WHERE tenant_id=@TenantId AND draft_id IS NOT NULL AND deleted_at IS NULL ORDER BY created_at DESC,vehicle",new{tenant.TenantId},cancellationToken:ct));
        return rows.Select(Saved).ToArray();
    }
    // Only manifests created by optimization carry a route; manual manifests report not found.
    public async Task<SavedManifestDto> Manifest(Guid id, CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var row=await db.QuerySingleOrDefaultAsync<ManifestRow>(new CommandDefinition("SELECT id, draft_id AS DraftId, vehicle, created_at AS CreatedAt, data::text AS Data, number,details::text AS Details,revision FROM aurora_manifest WHERE tenant_id=@TenantId AND id=@id AND draft_id IS NOT NULL AND deleted_at IS NULL",new{tenant.TenantId,id},cancellationToken:ct));
        return row is null ? throw new KeyNotFoundException("This manifest was not created by optimization.") : Saved(row);
    }
    private static SavedManifestDto Saved(ManifestRow r)=>new(r.Id,r.DraftId,r.Vehicle,new DateTimeOffset(r.CreatedAt),JsonSerializer.Deserialize<RouteSummaryDto>(r.Data)!, JsonSerializer.Deserialize<ManifestDetailsDto>(r.Details),r.Revision) { Number=r.Number };
    public async Task<SavedManifestDto> UpdateManifest(Guid id, UpdateManifestDto input, CancellationToken ct)
    {
        if (input.Details is null) throw new FormatException("Manifest details are required.");
        input.Details.Validate();
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await OperationalOutbox.Lock(db,tx,tenant.TenantId,ct);
        var current=await db.QuerySingleOrDefaultAsync<ManifestEditRow>(new CommandDefinition("SELECT vehicle,status,manifest_date AS Date FROM aurora_manifest WHERE tenant_id=@TenantId AND id=@id AND draft_id IS NOT NULL AND deleted_at IS NULL FOR UPDATE",new{tenant.TenantId,id},tx,cancellationToken:ct)) ?? throw new KeyNotFoundException("Manifest not found.");
        var date=input.Details.ManifestDate??current.Date;
        if(firebird is ILocalPlanningDataSource) await ManifestReservations.Validate(tenant.TenantId,id,current.Vehicle,current.Status,date,input.Details,db,tx,ct);
        var changed=await db.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_manifest SET data=jsonb_set(jsonb_set(data,'{ManifestDetails}',CAST(@details AS jsonb)),
                '{ManifestRevision}',to_jsonb(@nextRevision::int)), details=CAST(@details AS jsonb),manifest_date=@date,revision=revision+1
            WHERE tenant_id=@TenantId AND id=@id AND revision=@Revision AND draft_id IS NOT NULL AND deleted_at IS NULL
            """,new {tenant.TenantId,id,date=date.ToDateTime(TimeOnly.MinValue),details=JsonSerializer.Serialize(input.Details),input.Revision,nextRevision=input.Revision+1},tx,cancellationToken:ct));
        if(changed==0) {
            if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest WHERE tenant_id=@TenantId AND id=@id)",new{tenant.TenantId,id},tx,cancellationToken:ct))) throw new KeyNotFoundException("Manifest not found.");
            throw new FormatException("This manifest changed in another window. Cancel and refresh before editing again.");
        }
        await OperationalOutbox.Manifest(db,tx,tenant.TenantId,id,ct);
        await tx.CommitAsync(ct);
        return (await Manifests(ct)).Single(m=>m.Id==id);
    }
    private sealed record ManifestEditRow(string Vehicle,string Status,DateOnly Date);
    private sealed record ManifestRow(Guid Id, Guid DraftId,string Vehicle,DateTime CreatedAt,string Data,string? Number,string Details,int Revision);
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
    public async Task<IReadOnlyList<SavedManifestDto>> Finish(Guid draftId,Guid session,string owner,OptimizationResultDto result,CancellationToken ct,string[]? vehicles=null,string? acceptedRequest=null)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        if (firebird?.Enabled == true) await firebird.Lock(db, tx, ct);
        var draft=await db.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition("SELECT source_id AS SourceId, order_ids AS OrderIds, request::text AS RequestJson, finished_session AS FinishedSession, cancelled_at IS NOT NULL AS Cancelled FROM aurora_planning_draft WHERE tenant_id=@TenantId AND id=@draftId AND owner_id=@owner FOR UPDATE",new{tenant.TenantId,draftId,owner},tx,cancellationToken:ct)) ?? throw new KeyNotFoundException("Draft not found.");
        if(draft.FinishedSession is not null)
        {
            if(draft.FinishedSession!=session) throw new FormatException("This draft already created manifests from another result.");
            await tx.CommitAsync(ct);
            return (await Manifests(ct)).Where(m=>m.DraftId==draftId).ToArray();
        }
        if(draft.Cancelled) throw new FormatException("This plan was cancelled. Start a new plan from Optimization.");
        // Accepting only some routes leaves the other orders Ready to Route; finishing releases the plan's lock.
        var assignments=Assignments(result,draft.OrderIds).Where(a=>vehicles is not { Length: > 0 } || vehicles.Contains(a.Route.Vehicle,StringComparer.Ordinal)).ToList();
        if(assignments.Count==0) throw new FormatException("Select at least one route to create manifests.");
        ManifestReplanning.ValidateResult(draft.RequestJson,assignments.Select(a=>a.Route));
        var includedManifests=ManifestReplanning.Included(draft.RequestJson);
        var priorOrders=includedManifests.SelectMany(m=>m.OrderIds).ToHashSet(StringComparer.Ordinal);
        LineHaulPlanning.ValidateResult(draft.RequestJson, assignments.Select(a=>a.Route));
        var transferTo=RoutingInput.Text(RoutingInput.Parse(draft.RequestJson)["reporting"]?["lineHaulTo"]);
        var scheduled=assignments.SelectMany(a=>a.Orders).Order(StringComparer.Ordinal).ToArray();
        var newlyScheduled=scheduled.Where(id=>!priorOrders.Contains(id)).ToArray();
        var states=await db.QueryAsync<string>(new CommandDefinition("SELECT status FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=ANY(@scheduled) AND deleted_at IS NULL ORDER BY id FOR UPDATE",new{tenant.TenantId,draft.SourceId,scheduled=newlyScheduled},tx,cancellationToken:ct));
        if(states.Count()!=newlyScheduled.Length || states.Any(s=>s!=WorkspaceOrderStatus.Ready)) throw new FormatException("Another plan already manifested some orders. Refresh Orders and plan the remaining orders.");
        var exportRequest=draft.RequestJson;
        if(acceptedRequest is not null)
        {
            var exportRoot=RoutingInput.Parse(exportRequest);var accepted=RoutingInput.Parse(acceptedRequest);
            exportRoot["reporting"]!["drivers"]=accepted["reporting"]?["drivers"]?.DeepClone();
            exportRoot["reporting"]!["driverCostBasis"]=accepted["reporting"]?["driverCostBasis"]?.DeepClone();
            if(firebird is ILocalPlanningDataSource)exportRoot["vehicles"]=accepted["vehicles"]?.DeepClone();
            exportRequest=exportRoot.ToJsonString();
        }
        Dictionary<string,long>? external = null;
        if(firebird is ILocalPlanningDataSource localPlanning)
            await localPlanning.ValidateCommit(exportRequest,assignments.Select(a=>a.Route).ToArray(),db,tx,ct);
        if (RoutingInput.Text(RoutingInput.Parse(draft.RequestJson)["reporting"]?["firebirdSource"]) is not null)
            external = await (firebird ?? throw new FormatException("The Firebird connection is unavailable; no manifests were created.")).Export(
                draftId, session, exportRequest, assignments.Select(a => a.Route).ToArray(), ct);
        if(includedManifests.Length>0)
        {
            if(firebird is ILocalPlanningDataSource)
            {
                foreach(var included in includedManifests)
                {
                    var localId=included.LocalId??throw new FormatException("An included local manifest has no identity.");
                    await db.ExecuteAsync(new CommandDefinition("DELETE FROM aurora_manifest_order WHERE tenant_id=@TenantId AND manifest_id=@localId; UPDATE aurora_manifest SET deleted_at=now(),revision=revision+1 WHERE tenant_id=@TenantId AND id=@localId",new{tenant.TenantId,localId},tx,cancellationToken:ct));
                    await OperationalOutbox.Manifest(db,tx,tenant.TenantId,localId,ct);
                }
            }
            else
            {
                var numbers=includedManifests.Select(m=>m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                await db.ExecuteAsync(new CommandDefinition("DELETE FROM aurora_manifest_order WHERE tenant_id=@TenantId AND manifest_id IN(SELECT id FROM aurora_manifest WHERE tenant_id=@TenantId AND number=ANY(@numbers)); UPDATE aurora_manifest SET deleted_at=now(),revision=revision+1 WHERE tenant_id=@TenantId AND number=ANY(@numbers)",new{tenant.TenantId,numbers},tx,cancellationToken:ct));
            }
        }
        foreach(var (route,ids) in assignments)
        {
            var nativeNumber=external?[route.Vehicle].ToString(System.Globalization.CultureInfo.InvariantCulture);
            var existingId=nativeNumber is null?null:await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_manifest WHERE tenant_id=@TenantId AND number=@nativeNumber FOR UPDATE",new{tenant.TenantId,nativeNumber},tx,cancellationToken:ct));
            var id=existingId??Guid.NewGuid(); var data=JsonSerializer.Serialize(route);
            if(existingId is null)
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest(tenant_id,id,draft_id,vehicle,data) VALUES (@TenantId,@id,@draftId,@Vehicle,CAST(@data AS jsonb))",new{tenant.TenantId,id,draftId,route.Vehicle,data},tx,cancellationToken:ct));
            else
                await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_manifest SET draft_id=@draftId,vehicle=@Vehicle,data=CAST(@data AS jsonb),deleted_at=NULL,revision=revision+1 WHERE tenant_id=@TenantId AND id=@id",new{tenant.TenantId,id,draftId,route.Vehicle,data},tx,cancellationToken:ct));
            if (external is not null)
                await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_manifest SET number=@number,details=details || CAST(@details AS jsonb) WHERE tenant_id=@TenantId AND id=@id",
                    new { tenant.TenantId, id, number=external[route.Vehicle].ToString(System.Globalization.CultureInfo.InvariantCulture), details=JsonSerializer.Serialize(new { OriginTerminal=route.Terminal, DestinationTerminal=transferTo ?? "", Notes=$"Created in Nova as unassigned manifest {external[route.Vehicle]}." }) },tx,cancellationToken:ct));
            else if (transferTo is not null)
                await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_manifest SET details=details || CAST(@details AS jsonb) WHERE tenant_id=@TenantId AND id=@id",
                    new { tenant.TenantId, id, details=JsonSerializer.Serialize(new { OriginTerminal=route.Terminal, DestinationTerminal=transferTo }) },tx,cancellationToken:ct));
            foreach(var order in ids)
            {
                if(priorOrders.Contains(order) && firebird is not ILocalPlanningDataSource)
                {
                    var report=RoutingInput.ReportOrder(RoutingInput.Parse(draft.RequestJson),order);
                    var details=JsonSerializer.Serialize(new TmsOrder{Id=order,SourceId=draft.SourceId,TerminalCode=route.Terminal,UseConsigneeAddress=true,WorkType=order.StartsWith("FB-P-",StringComparison.Ordinal)?"Pickup":"Delivery",NativeOrderId=report?["nativeOrderId"]?.GetValue<long>(),Status=WorkspaceOrderStatus.Routed});
                    await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,status,details) VALUES(@TenantId,@SourceId,@order,now(),@customer,@city,'Routed',CAST(@details AS jsonb)) ON CONFLICT(tenant_id,source_id,id) DO UPDATE SET deleted_at=NULL",
                        new{tenant.TenantId,draft.SourceId,order,customer=RoutingInput.Text(report?["shipToName"])??"",city=RoutingInput.Text(report?["city"])??"",details},tx,cancellationToken:ct));
                }
                var stop=route.Pros.First(p=>p.ProNumber==order).StopNumber!.Value;
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) VALUES (@TenantId,@id,@SourceId,@order,@stop); UPDATE aurora_order SET status='Routed',revision=revision+1 WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@order",new{tenant.TenantId,id,draft.SourceId,order,stop},tx,cancellationToken:ct));
                if(firebird is ILocalPlanningDataSource)await OperationalOutbox.Order(db,tx,tenant.TenantId,draft.SourceId,order,ct);
            }
            if(firebird is ILocalPlanningDataSource)
            {
                var root=RoutingInput.Parse(exportRequest);var choice=root["reporting"]!["drivers"]![route.Vehicle];
                var truck=RoutingInput.Items(root["vehicles"]).Single(v=>RoutingInput.Text(v["id"])==route.Vehicle);
                var starts=RoutingInput.Time(truck["start"]?["earliestStartTime"])!.Value;
                var ends=RoutingInput.Time(truck["end"]?["latestEndTime"])!.Value;
                var details=new ManifestDetailsDto{OriginTerminal=route.Terminal,DestinationTerminal=transferTo??"",DriverId=long.Parse(RoutingInput.Text(choice?["id"])!,System.Globalization.CultureInfo.InvariantCulture),Driver=RoutingInput.Text(choice?["name"])??"",ManifestDate=DateOnly.FromDateTime(starts.DateTime),ReturnDate=DateOnly.FromDateTime(ends.DateTime)};
                await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_manifest SET manifest_date=@date,details=CAST(@details AS jsonb) WHERE tenant_id=@TenantId AND id=@id",new{tenant.TenantId,id,date=starts.Date,details=JsonSerializer.Serialize(details)},tx,cancellationToken:ct));
                await OperationalOutbox.Manifest(db,tx,tenant.TenantId,id,ct);
            }
        }
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_planning_draft SET finished_session=@session WHERE tenant_id=@TenantId AND id=@draftId",new{tenant.TenantId,session,draftId},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return (await Manifests(ct)).Where(m=>m.DraftId==draftId).ToArray();
    }
    private sealed class DraftRow { public Guid SourceId { get; set; } public string[] OrderIds { get; set; } = []; public string RequestJson { get; set; } = "{}"; public Guid? FinishedSession { get; set; } public bool Cancelled { get; set; } }
}
