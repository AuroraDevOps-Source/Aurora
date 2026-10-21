using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource(NpgsqlConnectionFactory factory, ITenantContext tenant, IOptions<FirebirdRoutingOptions> configuration, MasterAddressLookup? addressLookup = null)
{
    private FirebirdRoutingOptions Options => configuration.Value;
    public bool Enabled => Options.Enabled && Options.TenantId != Guid.Empty && tenant.TenantId == Options.TenantId;
    public FirebirdRoutingStatus Status => new(Enabled, Enabled ? Options.ReadyStatus : null);
    private void RequireEnabled()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(Options.ConnectionString) || string.IsNullOrWhiteSpace(Options.SourceKey))
            throw new FormatException("The Firebird routing source is not configured for this company.");
    }
    public static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    public Guid SourceId(string terminal) => StableId($"firebird:{tenant.TenantId}:{Options.SourceKey}:{terminal}");
    private static string Text(string? value) => value?.Trim() ?? "";
    public static bool HasCoordinates(double? latitude, double? longitude) => DataRequirements.HasCoordinates(latitude, longitude);
    private DateTimeOffset LocalTime(DateTime value)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId);
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw new FormatException("A Firebird appointment falls inside a daylight-saving time gap.");
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
    private async Task<FbConnection> Open(CancellationToken ct)
    {
        RequireEnabled();
        var db = new FbConnection(Options.ConnectionString);
        try { await db.OpenAsync(ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    public async Task<FirebirdSyncResult> Synchronize(CancellationToken ct)
    {
        RequireEnabled();
        // Serialize refresh against draft creation/export for this company. Network reads happen
        // inside this lock so a stale snapshot cannot overwrite a just-finished manifest.
        await using var pg = await factory.OpenConnectionAsync(ct);
        await using var tx = await pg.BeginTransactionAsync(ct);
        await Lock(pg, tx, ct);
        var readyStatuses = await ReadyStatuses(pg, tx, ct);
        await using var fb = await Open(ct);
        await using var read = fb.BeginTransaction(new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.Concurrency | FbTransactionBehavior.Wait
        });
        var rows = (await fb.QueryAsync<SourceOrder>(new CommandDefinition("""
            SELECT h.PB_ID AS NativeId, h.PBNUM AS Pro, h.PB_LOCATION AS Terminal,
                h.PBTPIECE AS Pieces, h.PBTWEIGH AS Weight, h.PB_TTL_CUBE AS Cube,
                h.PBBOL AS Reference, s.OPS_STOP_ID AS StopId, s.OPS_NAME AS Customer,
                s.OPS_ADDR1 AS Address, s.OPS_CITY AS City, s.OPS_STATE AS State, s.OPS_ZIP AS PostalCode,
                s.OPS_LATITUDE AS Latitude, s.OPS_LONGITUDE AS Longitude,
                s.OPS_APPT_LOW AS AppointmentFrom, s.OPS_APPT_HIGH AS AppointmentTo
            FROM OPS_HEADER h JOIN OPS_STOP_REC s ON s.OPS_ORDER_ID=h.PB_ID AND s.OPS_TYPE='D'
            WHERE h.PB_TYPE='O' AND h.PB_STATUS IN @readyStatuses AND h.PB_DT_CANC IS NULL
                AND COALESCE(s.OPS_MANIFEST_NUMBER,0)=0 AND s.OPS_DELIVERED_TIME IS NULL
                AND NOT EXISTS (SELECT 1 FROM OPS_MANIFEST_STOPS m WHERE m.OSM_PKUP_DELV_ID=s.OPS_STOP_ID)
            ORDER BY h.PB_ID,s.OPS_STOP_NUMBER
            """, new { readyStatuses }, read, cancellationToken: ct))).ToArray();
        var terminals = await ReadTerminals(ct);
        await read.RollbackAsync(ct);
        var warnings = new List<string>();
        var duplicateIds = rows.GroupBy(r => r.NativeId).Where(g => g.Count() != 1).Select(g => g.Key).ToHashSet();
        if (duplicateIds.Count > 0) warnings.Add($"{duplicateIds.Count} orders have multiple delivery stops and need review before routing.");
        rows = rows.Where(r => !duplicateIds.Contains(r.NativeId)).ToArray();
        var valid = new List<SourceOrder>();
        foreach (var group in rows.GroupBy(r => Text(r.Terminal)))
        {
            var node = terminals.FirstOrDefault(t => Text(t.Code) == group.Key);
            if (node is null) { warnings.Add($"{group.Count()} orders have an unknown terminal '{group.Key}'."); continue; }
            node.Code = Text(node.Code); node.Name = Text(node.Name); node.Address = Text(node.Address);
            node.City = Text(node.City); node.State = Text(node.State); node.PostalCode = Text(node.PostalCode); node.Country = Text(node.Country);
            if (!HasCoordinates(node.Latitude, node.Longitude)) { node.Latitude = null; node.Longitude = null; }
            var orders = group.ToArray();
            var source = SourceId(node.Code);
            var request = BuildRequest(node, orders);
            await pg.ExecuteAsync(new CommandDefinition("""
                INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES(@TenantId,@source,@name,CAST(@json AS jsonb))
                ON CONFLICT(tenant_id,id) DO UPDATE SET request=excluded.request
                """, new { tenant.TenantId, source, name = $"Firebird {Options.SourceKey} · {node.Code}", json = request.ToJsonString() }, tx, cancellationToken: ct));
            // Rebind old snapshots (including reserved orders) to the native terminal identity.
            // Appointment and order snapshots stay intact; no local terminal master is maintained.
            await pg.ExecuteAsync(new CommandDefinition("""
                UPDATE aurora_order SET terminal_id=NULL,
                    details=details || jsonb_build_object('TerminalId',CAST(@nativeId AS text),'TerminalCode',CAST(@code AS text))
                WHERE tenant_id=@TenantId AND source_id=@source
                """,new{tenant.TenantId,source,nativeId=node.Id.ToString(),code=node.Code},tx,cancellationToken:ct));
            foreach (var row in orders)
            {
                var order = new TmsOrder
                {
                    SourceId = source, Id = row.Id, TerminalId = node.Id, TerminalCode = node.Code,
                    UseConsigneeAddress = true,
                    Customer = Text(row.Customer), Address = Text(row.Address), City = Text(row.City), State = Text(row.State), PostalCode = Text(row.PostalCode),
                    Reference = Text(row.Reference), Pieces = row.Pieces ?? 0, Weight = row.Weight ?? 0, Cube = row.Cube ?? 0,
                    ScheduledAt = row.AppointmentFrom is { } start ? LocalTime(start) : LocalTime(DateTime.Today)
                };
                await pg.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO aurora_order AS o(tenant_id,source_id,id,scheduled_at,customer,city,status,terminal_id,details)
                    VALUES(@TenantId,@source,@Id,@time,@Customer,@City,'ReadyToRoute',NULL,CAST(@json AS jsonb))
                    ON CONFLICT(tenant_id,source_id,id) DO UPDATE SET scheduled_at=excluded.scheduled_at,
                        customer=excluded.customer,city=excluded.city,terminal_id=NULL,details=excluded.details,deleted_at=NULL,
                        revision=o.revision+1
                    WHERE o.status='ReadyToRoute' AND NOT EXISTS(SELECT 1 FROM aurora_planning_draft d
                        WHERE d.tenant_id=o.tenant_id AND d.source_id=o.source_id AND o.id=ANY(d.order_ids)
                        AND d.finished_session IS NULL AND d.cancelled_at IS NULL)
                    """, new { tenant.TenantId, source, order.Id, time = order.ScheduledAt.ToUniversalTime(), order.Customer, order.City, terminal = node.Id, json = JsonSerializer.Serialize(order) }, tx, cancellationToken: ct));
                valid.Add(row);
            }
        }
        // Withdraw no-longer-eligible external orders, but keep every open draft's snapshot
        // available for retry/reconciliation. Finish always rechecks the live Firebird rows.
        var keys = valid.Select(r => r.Id).ToArray();
        await pg.ExecuteAsync(new CommandDefinition("""
            UPDATE aurora_order o SET deleted_at=now(),revision=o.revision+1 FROM aurora_order_source s
            WHERE (s.tenant_id,s.id)=(o.tenant_id,o.source_id) AND o.tenant_id=@TenantId
                AND s.request#>>'{reporting,firebirdSource}'=@SourceKey AND o.status='ReadyToRoute'
                AND o.deleted_at IS NULL AND NOT(o.id=ANY(@keys))
                AND NOT EXISTS(SELECT 1 FROM aurora_planning_draft d WHERE d.tenant_id=o.tenant_id
                    AND d.source_id=o.source_id AND o.id=ANY(d.order_ids) AND d.finished_session IS NULL AND d.cancelled_at IS NULL)
            """, new { tenant.TenantId, Options.SourceKey, keys }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        var codes = valid.Select(r => Text(r.Terminal)).Distinct().Order().ToArray();
        return new(valid.Count, codes, warnings.ToArray(), codes.Select(SourceId).ToArray());
    }
    internal Task Lock(Npgsql.NpgsqlConnection db, Npgsql.NpgsqlTransaction tx, CancellationToken ct) =>
        db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", new { key = $"firebird-routing:{tenant.TenantId}" }, tx, cancellationToken: ct));

    private JsonObject BuildRequest(TerminalDto node, SourceOrder[] rows)
    {
        var locations = new JsonArray(new JsonObject { ["id"] = "DEPOT", ["latitude"] = node.Latitude, ["longitude"] = node.Longitude });
        var deliveries = new JsonArray(); var reports = new JsonObject();
        foreach (var row in rows)
        {
            locations.Add(new JsonObject { ["id"] = row.Id, ["latitude"] = row.Latitude, ["longitude"] = row.Longitude });
            deliveries.Add(new JsonObject { ["id"] = row.Id, ["delivery"] = new JsonObject { ["locationId"] = row.Id, ["duration"] = Options.ServiceSeconds },
                ["properties"] = new JsonObject { ["loads"] = new JsonArray(
                    new JsonObject { ["dimension"] = "weight", ["value"] = row.Weight ?? 0 },
                    new JsonObject { ["dimension"] = "volume", ["value"] = row.Cube ?? 0 }) } });
            var report = new JsonObject { ["shipToName"] = Text(row.Customer), ["address1"] = Text(row.Address), ["city"] = Text(row.City),
                ["state"] = Text(row.State), ["postalCode"] = Text(row.PostalCode), ["units"] = row.Pieces ?? 0,
                ["nativeOrderId"] = row.NativeId, ["nativeStopId"] = row.StopId, ["nativePro"] = row.Pro };
            if (row.AppointmentFrom is { } start) report["appointment"] = new JsonObject
            {
                ["blockBegin"] = LocalTime(start).ToString("O"), ["blockEnd"] = LocalTime(row.AppointmentTo ?? start).ToString("O"), ["confirmed"] = true
            };
            reports[row.Id] = report;
        }
        var startTime = LocalTime(DateTime.Today.AddHours(6));
        return new JsonObject
        {
            ["settings"] = new JsonObject { ["duration"] = 10 }, ["locations"] = locations,
            ["depots"] = new JsonArray(new JsonObject { ["id"] = "DEPOT", ["locationId"] = "DEPOT" }),
            ["orders"] = new JsonObject { ["deliveries"] = deliveries },
            // Date seed only: FleetPlanning replaces this with the company's saved fleet.
            ["vehicles"] = new JsonArray(new JsonObject { ["id"] = "DATE_SEED", ["start"] = new JsonObject { ["locationId"] = "DEPOT", ["earliestStartTime"] = startTime.ToString("O") } }),
            ["reporting"] = new JsonObject { ["terminal"] = node.Code, ["firebirdSource"] = Options.SourceKey, ["orders"] = reports }
        };
    }
    public sealed class SourceOrder
    {
        public long NativeId { get; set; }
        public long Pro { get; set; }
        public string Id => $"FB-{NativeId.ToString(CultureInfo.InvariantCulture)}";
        public long StopId { get; set; }
        public string? Terminal { get; set; }
        public string? Customer { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Reference { get; set; }
        public decimal? Pieces { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Cube { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? AppointmentFrom { get; set; }
        public DateTime? AppointmentTo { get; set; }
    }
}
