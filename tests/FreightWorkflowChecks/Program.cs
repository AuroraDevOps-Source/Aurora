using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using Npgsql;

var count = 0;
var user = Guid.NewGuid();
var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); count++; }
void Reject(Action action, string message)
{
    try { action(); } catch (Exception e) when (e is FormatException or FreightConflictException) { Check(true, message); return; }
    throw new Exception("Expected rejection: " + message);
}
async Task RejectAsync(Func<Task> action, string message)
{
    try { await action(); } catch (Exception e) when (e is FormatException or FreightConflictException or KeyNotFoundException) { Check(true, message); return; }
    throw new Exception("Expected rejection: " + message);
}
T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
FreightActivity Move() => new() { Origin = new() { Name = "Manufacturer", Address = "1 Origin Rd" }, Destination = new() { Name = "Consumer", Address = "2 Destination Rd" }, EstimatedCost = 40, PlannedCharge = 100 };
FreightExecution Command(FreightWorkflow w, FreightActivity a, string action, Guid? task = null) => new() { Revision = w.Revision, ActivityId = a.Id, Action = action, TaskId = task };

var leg = Move();
leg.Tasks = [new() { Type = "Inspect", EstimatedCost = 5, PlannedCharge = 10 }, new() { Type = "Blanket wrap", EstimatedCost = 3, PlannedCharge = 8 }];
var next = Move();
var w = new FreightWorkflow { Activities = [leg, next] };
w.Validate();
Check(w.EstimatedCost == 88 && w.PlannedCharge == 218, "activity and task estimates are separate and summed");
Reject(() => w.Execute(Command(w, next, "Start"), user, now), "later legs cannot start before earlier work finishes");
Reject(() => w.Execute(Command(w, leg, "Complete"), user, now), "unstarted work cannot complete");
var start = Command(w, leg, "Start");
Check(w.Execute(start, user, now), "start records an event");
w.Revision++;
Check(!w.Execute(start, user, now.AddMinutes(1)) && w.Events.Count == 1, "network retry is idempotent even with an old revision");
var changedCommand = Clone(start); changedCommand.Note = "Changed";
Reject(() => w.Execute(changedCommand, user, now), "command identifiers cannot be reused with changed content");
Reject(() => w.Execute(Command(w, leg, "Complete"), user, now), "required tasks block activity completion");
Reject(() => w.Execute(Command(w, leg, "Complete task", leg.Tasks[1].Id), user, now), "required task sequence is enforced");
var taskDone = Command(w, leg, "Complete task", leg.Tasks[0].Id); taskDone.ActualCost = 6;
w.Execute(taskDone, user, now.AddMinutes(5));
Reject(() => w.Execute(Command(w, leg, "Complete task", leg.Tasks[0].Id), user, now), "completed tasks cannot be charged twice with a different key");
w.Execute(Command(w, leg, "Complete task", leg.Tasks[1].Id), user, now.AddMinutes(10));
var done = Command(w, leg, "Complete"); done.ActualCost = 39;
w.Execute(done, user, now.AddMinutes(15));
Check(w.Status(leg.Id) == "Completed" && w.RecordedCost == 45, "actual cost includes only recorded amounts");
Check(w.Events.All(e => e.RecordedBy == user), "execution records the authenticated actor");
Reject(() => w.Execute(Command(w, leg, "Start"), user, now), "completed activities cannot be reopened");
var edited = Clone(w); edited.Activities[0].Origin.Address = "Changed address";
Reject(() => edited.ValidateReplacement(w), "historical locations cannot be rewritten");
edited = Clone(w); edited.Activities.Reverse();
Reject(() => edited.ValidateReplacement(w), "historical sequence cannot be reordered");
edited = Clone(w); edited.Activities[1].Destination!.Address = "New delivery address"; edited.Events.Clear(); edited.ValidateReplacement(w);
Check(edited.Events.Count == w.Events.Count, "plan edits preserve server-owned events and allow future changes");
edited.Currency = "CAD";
Reject(() => edited.ValidateReplacement(w), "currency cannot reinterpret historical costs");
w.Execute(Command(w, next, "Start"), user, now.AddHours(1));
Reject(() => w.Execute(Command(w, next, "Fail"), user, now), "failure requires a reason");
var failure = Command(w, next, "Fail"); failure.Note = "Customer absent"; failure.Responsibility = "Customer";
w.Execute(failure, user, now.AddHours(2));
var retry = Move(); retry.RetryOf = next.Id; w.Activities.Add(retry); w.Validate();
Check(w.Status(next.Id) == "Failed" && w.Status(retry.Id) == "Planned", "redelivery adds a new attempt without reopening the failed leg");
Check(w.Events.Last().Responsibility == "Customer", "failure responsibility is retained for billing review");
var badRetry = Clone(w); badRetry.Activities.Last().RetryOf = leg.Id;
Reject(badRetry.Validate, "a new attempt must reference a failed movement");
var malformed = Clone(w); malformed.Activities.Last().Id = leg.Id;
Reject(malformed.Validate, "duplicate activity ids are rejected");
malformed = Clone(w); malformed.Activities.Last().Tasks = [new() { Id = leg.Id }];
Reject(malformed.Validate, "task and activity ids cannot collide");
malformed = Clone(w); malformed.Activities.Last().EstimatedCost = -1;
Reject(malformed.Validate, "negative monetary amounts are rejected");
malformed = Clone(w); malformed.Activities.Last().PlannedCharge = 0.001m;
Reject(malformed.Validate, "fractional cents are rejected");
malformed = Clone(w); malformed.Activities.Last().Origin = null!;
Reject(malformed.Validate, "malformed locations produce a validation error");
malformed = Clone(w); malformed.Activities.Last().ProviderType = "Agent";
Reject(malformed.Validate, "outside providers must be identified");
var storage = new FreightActivity { Kind = "Storage", Destination = null, Origin = new() { Name = "Warehouse" }, StorageFreeHours = 72, StorageDailyCharge = 25 };
var stored = new FreightWorkflow { Activities = [storage] }; stored.Validate();
Check(stored.StorageCharge(storage, now) == 0, "unstarted storage does not accrue charges");
stored.Execute(Command(stored, storage, "Start"), user, now);
Check(stored.StorageCharge(storage, now.AddHours(72)) == 0, "storage grace boundary is inclusive");
Check(stored.StorageCharge(storage, now.AddHours(72).AddSeconds(1)) == 25, "first started storage period accrues after grace");
Check(stored.StorageCharge(storage, now.AddHours(96)) == 25, "exact 24-hour period is not double charged");
Check(stored.StorageCharge(storage, now.AddHours(96).AddSeconds(1)) == 50, "next started period accrues");
stored.Execute(Command(stored, storage, "Complete"), user, now.AddHours(100));
Check(stored.StorageCharge(storage, now.AddDays(30)) == 50, "completed storage stops accruing");
var node = new TerminalDto { Id = Guid.NewGuid(), Name = "Agent terminal", Address = "3 Agent Rd" };
var order = new TmsOrder { Id = "PRO-1", Customer = "Consumer", Shipper = "Factory", OriginAddress = "1 Factory Rd", Address = "2 Consumer Rd", DeliverToNodeId = node.Id };
var seed = FreightWorkflow.FromOrder(order, [node]); seed.Validate();
Check(seed.Activities[0].Destination!.NodeId == node.Id && order.Customer == "Consumer" && order.Address == "2 Consumer Rd", "agent destination does not overwrite consignee identity");
var cancelled = new FreightWorkflow { Activities = [Move()] };
var cancel = Command(cancelled, cancelled.Activities[0], "Cancel"); cancel.Note = "Customer cancelled before pickup";
cancelled.Execute(cancel, user, now);
Reject(() => cancelled.Execute(Command(cancelled, cancelled.Activities[0], "Start"), user, now), "cancelled work cannot be started");
var outdated = Command(w, retry, "Start"); outdated.Revision--;
Reject(() => w.Execute(outdated, user, now), "stale execution cannot overwrite concurrent work");
var forged = new FreightWorkflow { Activities = [Move()], Events = [new(Guid.NewGuid(), leg.Id, null, "Complete", now, user, "Forged", "Unknown", 900)] };
forged.ValidateReplacement(new());
Check(forged.Events.Count == 0 && forged.RecordedCost == 0, "plan writes cannot inject execution history or actual costs");

var connection = Environment.GetEnvironmentVariable("FREIGHT_CHECK_CONNECTION");
if (connection is not null)
{
    var settings = new NpgsqlConnectionStringBuilder(connection);
    if (settings.Host != "127.0.0.1" || !settings.Database!.StartsWith("aurora_workspace_checks_")) throw new Exception("Use an isolated local checks database.");
    await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
    var tenant = new TestTenant(Guid.NewGuid(), user); var other = new TestTenant(Guid.NewGuid(), Guid.NewGuid());
    foreach (var t in new[] { tenant, other }) await admin.ExecuteAsync("INSERT INTO tenant(id,name,slug) VALUES(@Id,'Freight checks',@Slug)", new { Id = t.TenantIdOrNull, Slug = Guid.NewGuid().ToString("N") });
    settings.Options = "-c role=aurora_app";
    await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
    var factory = new NpgsqlConnectionFactory(source, tenant); var foreignFactory = new NpgsqlConnectionFactory(source, other);
    var store = new FreightWorkflowStore(factory, tenant); var foreign = new FreightWorkflowStore(foreignFactory, other);
    var tms = new TmsStore(factory, tenant);
    order.DeliverToNodeId = null;
    await tms.SaveOrder(order, true, default);
    Check((await store.Get(order.SourceId, order.Id, default)).Revision == 0, "existing orders need no workflow backfill");
    var plan = FreightWorkflow.FromOrder(order, []);
    var saved = await store.Save(order.SourceId, order.Id, plan, default);
    Check(saved.Revision == 1 && (await store.Get(order.SourceId, order.Id, default)).Activities.Count == 1, "workflow persists independently of order details");
    var stale = Clone(saved); stale.Revision = 0;
    await RejectAsync(() => store.Save(order.SourceId, order.Id, stale, default), "stale plan writes are rejected");
    await RejectAsync(() => foreign.Get(order.SourceId, order.Id, default), "another tenant cannot read a workflow");
    await RejectAsync(() => foreign.Save(order.SourceId, order.Id, Clone(saved), default), "another tenant cannot write a workflow");
    var foreignNode = Guid.NewGuid();
    await admin.ExecuteAsync("INSERT INTO aurora_terminal(tenant_id,id,code,name) VALUES(@TenantId,@Id,'FOREIGN','Foreign node')", new { TenantId = other.TenantIdOrNull, Id = foreignNode });
    var foreignPlan = Clone(saved); foreignPlan.Activities[0].Origin.NodeId = foreignNode;
    await RejectAsync(() => store.Save(order.SourceId, order.Id, foreignPlan, default), "foreign node links are rejected");
    await using (var db = await foreignFactory.OpenConnectionAsync())
    {
        Check(await db.ExecuteScalarAsync<string>("SELECT current_user") == "aurora_app", "database isolation checks use the runtime role");
        Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_freight_workflow") == 0, "RLS blocks reads even without an explicit tenant predicate");
    }
    var command = Command(saved, saved.Activities[0], "Start");
    var raced = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => store.Execute(order.SourceId, order.Id, Clone(command), default)));
    Check(raced.All(r => r.Revision == 2 && r.Events.Count == 1), "concurrent duplicate execution is recorded once");
    var current = await store.Get(order.SourceId, order.Id, default);
    var historyRewrite = Clone(current); historyRewrite.Activities.Clear(); historyRewrite.Events.Clear();
    await RejectAsync(() => store.Save(order.SourceId, order.Id, historyRewrite, default), "API persistence rejects historical activity removal");
    var finish = Command(current, current.Activities[0], "Complete");
    await RejectAsync(() => foreign.Execute(order.SourceId, order.Id, finish, default), "another tenant cannot execute a workflow");
    await store.Execute(order.SourceId, order.Id, finish, default);
    var changedOrder = (await tms.OrdersById(order.SourceId, [order.Id], default)).Single(); changedOrder.Notes = "Ordinary edit";
    await tms.SaveOrder(changedOrder, false, default);
    Check((await store.Get(order.SourceId, order.Id, default)).Events.Count == 2, "ordinary order edits retain execution history");
    await RejectAsync(() => tms.DeleteOrder(order.SourceId, order.Id, changedOrder.Revision + 1, default), "executed freight cannot be deleted");
    var importer = new WorkspaceImportStore(factory, tenant);
    await RejectAsync(() => importer.Execute(new() { Version = 1, Trucks = [], Terminals = [], Customers = [], Orders = [], Manifests = [] }, true, default), "workspace replacement cannot erase freight workflows");
    Check((await store.Get(order.SourceId, order.Id, default)).Events.Count == 2, "failed replacement leaves history intact");
    var missingId = "MISSING";
    await RejectAsync(() => store.Get(order.SourceId, missingId, default), "missing orders return not found");
    var dispatchedOrder = new TmsOrder { Id = "DISPATCH-CHECK", Customer = "Consignee", Shipper = "Factory", Address = "2 Delivery Rd", OriginAddress = "1 Origin Rd" };
    await tms.SaveOrder(dispatchedOrder, true, default);
    var manifest = new TmsManifest { Number = "WORKFLOW-CHECK" };
    await tms.SaveManifest(manifest, true, default);
    await tms.ChangeOrders(manifest.Id, new([new(dispatchedOrder.SourceId, dispatchedOrder.Id)], 0), false, false, default);
    manifest = (await tms.Manifests(default)).Single(); manifest.Status = "Dispatched";
    await tms.SaveManifest(manifest, false, default);
    var dispatchPlan = await store.Save(dispatchedOrder.SourceId, dispatchedOrder.Id, FreightWorkflow.FromOrder(dispatchedOrder, []), default);
    await store.Execute(dispatchedOrder.SourceId, dispatchedOrder.Id, Command(dispatchPlan, dispatchPlan.Activities[0], "Start"), default);
    Check((await store.Get(dispatchedOrder.SourceId, dispatchedOrder.Id, default)).Status(dispatchPlan.Activities[0].Id) == "In progress", "workflow execution remains available after manifest dispatch");
    Check((await tms.OrdersById(dispatchedOrder.SourceId, [dispatchedOrder.Id], default)).Single().Status == "Dispatched", "manual workflow events do not rewrite manifest-controlled order status");
}
else Console.WriteLine("SKIP database integration checks: set FREIGHT_CHECK_CONNECTION to an isolated local checks database.");
Console.WriteLine($"{count} freight workflow checks passed.");

sealed record TestTenant(Guid Id, Guid User) : ITenantContext
{
    public Guid? TenantIdOrNull => Id;
    public Guid? UserIdOrNull => User;
}
