using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    public bool IsExternal(string json) => RoutingInput.Text(RoutingInput.Parse(json)["reporting"]?["firebirdSource"]) is not null;
    public void ValidateSource(string json)
    {
        RequireEnabled();
        if (RoutingInput.Text(RoutingInput.Parse(json)["reporting"]?["firebirdSource"]) != Options.SourceKey)
            throw new FormatException("This plan belongs to a different Firebird source. Restore its connection before finishing.");
    }
    public string PrepareDraft(string json, TerminalDto node)
    {
        ValidateSource(json);
        if (!HasCoordinates(node.Latitude, node.Longitude))
            throw new FormatException($"The address for {node.Code} could not be located. Check it in Transportation Nodes and save again.");
        var root = RoutingInput.Parse(json);
        var depot = RoutingInput.Items(root["locations"]).Single(l => RoutingInput.Text(l["id"]) == "DEPOT");
        depot["latitude"] = node.Latitude; depot["longitude"] = node.Longitude;
        foreach (var location in RoutingInput.Items(root["locations"]))
        {
            if (!HasCoordinates(location["latitude"]?.GetValue<double>(), location["longitude"]?.GetValue<double>()))
                throw new FormatException($"The delivery address for {RoutingInput.Text(location["id"])} could not be located. Check the order's address and save again.");
        }
        return root.ToJsonString();
    }

    public async Task ResolveNodeForPlanning(TerminalDto node, CancellationToken ct)
    {
        if (HasCoordinates(node.Latitude, node.Longitude)) return;
        var current = await ReadMaster("terminals", node.Code, ct);
        var saved = await SaveMaster("terminals", node.Code,
            new(current.Revision, current.Fields.ToDictionary(f => f.Key, f => f.Value)), ct);
        double? Number(string key) => double.TryParse(saved.Fields.Single(f => f.Key == key).Value,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
        node.Latitude = Number("LM_LATITUDE"); node.Longitude = Number("LM_LONGITUDE");
        if (!HasCoordinates(node.Latitude, node.Longitude))
            throw new FormatException($"The address for {node.Code} could not be located. Check it in Transportation Nodes and save again.");
    }

    public async Task<string> ResolveOrdersForPlanning(string json, CancellationToken ct)
    {
        ValidateSource(json);
        var root = RoutingInput.Parse(json);
        foreach (var location in RoutingInput.Items(root["locations"]))
        {
            if (RoutingInput.Text(location["id"]) == "DEPOT" ||
                HasCoordinates(location["latitude"]?.GetValue<double>(), location["longitude"]?.GetValue<double>())) continue;
            var native = Native(root, RoutingInput.Text(location["id"])!);
            var current = await ReadRecord("orders", native.Order, ct);
            var saved = await SaveRecord("orders", native.Order, new(current.Revision,
                current.Parts.ToDictionary(p => p.Key, p => p.Fields.ToDictionary(f => f.Key, f => f.Value))), ct);
            var stop = saved.Parts.Single(p => p.Key == $"stop-{native.Stop}");
            double? Number(string key) => double.TryParse(stop.Fields.Single(f => f.Key == key).Value,
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
            location["latitude"] = Number("OPS_LATITUDE"); location["longitude"] = Number("OPS_LONGITUDE");
        }
        return root.ToJsonString();
    }
    public static string ExportPrefix(Guid tenantId, Guid draftId) => $"aurora:{tenantId:N}:{draftId:N}:";
    public static string ExportKey(Guid tenantId, Guid draftId, string vehicle) => ExportPrefix(tenantId, draftId) + StableId(vehicle).ToString("N");
    public static string ExportFingerprint(Guid sessionId, IReadOnlyList<RouteSummaryDto> routes, string? destination = null, string? kind = null)
    {
        var ordered = routes.OrderBy(r => r.Vehicle, StringComparer.Ordinal).ToArray();
        // Preserve existing delivery receipts; transfer receipts also bind the receiving terminal.
        var payload = destination is null ? JsonSerializer.Serialize(new { sessionId, routes = ordered })
            : JsonSerializer.Serialize(new { sessionId, routes = ordered, destination });
        return (destination is null ? "Aurora routing export v1 " : kind == PlanningBatch.Consolidation ? "Aurora consolidation export v1 " : "Aurora line haul export v1 ") +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public sealed record ManifestEndpoints(string Origin, string Destination, bool Transfer)
    {
        public string[] RoutingTerminals => Transfer ? [Origin, Destination] : [Origin];
    }
    public static ManifestEndpoints ExportEndpoints(string json)
    {
        var reporting = RoutingInput.Parse(json)["reporting"];
        var origin = RoutingInput.Text(reporting?["terminal"])?.Trim();
        var destination = RoutingInput.Text(reporting?["lineHaulTo"])?.Trim();
        var transfer = reporting?["lineHaulTo"] is not null;
        if (string.IsNullOrWhiteSpace(origin) || origin.Length > 12)
            throw new FormatException("The plan needs a valid Nova origin terminal code.");
        if (transfer && (string.IsNullOrWhiteSpace(destination) || destination.Length > 12 || origin.Equals(destination, StringComparison.OrdinalIgnoreCase)))
            throw new FormatException("Select a different Nova receiving terminal.");
        return new(origin, transfer ? destination! : origin, transfer);
    }

    // All routes commit in one Firebird transaction. The caller holds Aurora's draft/order
    // locks until its own commit. A stable external reference recovers the only uncertainty
    // window: Firebird committed but Aurora's transaction/HTTP response did not.
    public async Task<Dictionary<string, long>> Export(Guid draftId, Guid sessionId, string draftJson,
        IReadOnlyList<RouteSummaryDto> routes, CancellationToken ct, bool rollbackOnly = false)
    {
        ValidateSource(draftJson);
        if (routes.Count == 0 || routes.Select(r => r.Vehicle).Distinct().Count() != routes.Count)
            throw new FormatException("Export requires distinct nonempty routes.");
        var root = RoutingInput.Parse(draftJson);
        var endpoints = ExportEndpoints(draftJson);
        LineHaulPlanning.ValidateResult(draftJson, routes);
        var terminal = endpoints.Origin;
        var prefix = ExportPrefix(tenant.TenantId, draftId);
        var fingerprint = ExportFingerprint(sessionId, routes, endpoints.Transfer ? endpoints.Destination : null, RoutingInput.Text(root["reporting"]?["transferKind"]));
        await using var configDb = await factory.OpenConnectionAsync(ct);
        var readyStatuses = await ReadyStatuses(configDb, null, ct);
        await using var fb = await Open(ct);
        await using var tx = fb.BeginTransaction(new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.Write | FbTransactionBehavior.ReadCommitted |
                FbTransactionBehavior.NoRecVersion | FbTransactionBehavior.Wait,
            WaitTimeout = TimeSpan.FromSeconds(15)
        });
        foreach (var code in endpoints.RoutingTerminals)
            if (!await fb.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM LOCMAST WHERE LM_ID=@code) FROM RDB$DATABASE", new { code }, tx, cancellationToken: ct)))
                throw new FormatException($"Terminal {code} is not defined in Nova. Choose a Nova terminal before creating manifests.");
        var existing = (await fb.QueryAsync<ExportedManifest>(new CommandDefinition("""
            SELECT PB_ID AS Id, PB_EXTERNAL_REF AS Reference, PB_NOTES AS Notes
            FROM OPS_HEADER WHERE PB_TYPE='M' AND PB_EXTERNAL_REF STARTING WITH @prefix
            """, new { prefix }, tx, cancellationToken: ct))).ToArray();
        var result = new Dictionary<string, long>();
        if (existing.Length > 0)
        {
            if (existing.Length != routes.Count || existing.Any(e => ManifestNotes.Receipt(e.Notes) != fingerprint))
                throw new FormatException("This draft already exported a different result. Reconcile its Nova manifests before retrying.");
            foreach (var route in routes)
            {
                var manifest = existing.SingleOrDefault(e => Text(e.Reference) == ExportKey(tenant.TenantId, draftId, route.Vehicle))
                    ?? throw new FormatException("The saved Nova export does not match the selected routes.");
                await VerifyManifest(fb, tx, manifest.Id, endpoints, Ordered(route).Select(p => Native(root, p.ProNumber).Stop).ToArray(), ct);
                result.Add(route.Vehicle, manifest.Id);
            }
            await tx.RollbackAsync(ct);
            return result;
        }
        // Lock every order/stop in stable order and validate the whole batch before creating
        // any manifest. A stale, canceled or already-manifested order fails the whole export.
        var stops = routes.SelectMany(Ordered).Select(p => Native(root, p.ProNumber)).OrderBy(n => n.Order).ToArray();
        if (stops.Length == 0 || stops.Select(s => s.Stop).Distinct().Count() != stops.Length)
            throw new FormatException("The exported routes contain empty or repeated order stops.");
        foreach (var native in stops)
        {
            var ready = await fb.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
                SELECT PB_ID FROM OPS_HEADER WHERE PB_ID=@Order AND PB_TYPE='O' AND PB_STATUS IN @readyStatuses
                    AND PB_LOCATION=@terminal AND PB_DT_CANC IS NULL WITH LOCK
                """, new { native.Order, readyStatuses, terminal }, tx, cancellationToken: ct));
            var stop = await fb.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
                SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_STOP_ID=@Stop AND OPS_ORDER_ID=@Order
                    AND OPS_TYPE='D' AND COALESCE(OPS_MANIFEST_NUMBER,0)=0 AND OPS_DELIVERED_TIME IS NULL WITH LOCK
                """, native, tx, cancellationToken: ct));
            var linked = await fb.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_PKUP_DELV_ID=@Stop", native, tx, cancellationToken: ct));
            if (ready is null || stop is null || linked != 0)
                throw new FormatException($"Nova order {native.Order} changed or is already manifested. Refresh its source before finishing.");
        }
        foreach (var route in routes)
        {
            var id = await NextId(fb, tx, true, ct);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
            var day = route.Departure is { } departure
                ? TimeZoneInfo.ConvertTime(departure, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).Date : now.Date;
            await fb.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE,PB_EXTERNAL_REF,PB_NOTES)
                VALUES(@id,'M','M',@terminal,@terminal,@destination,1,@now,@day,@reference,@fingerprint)
                """, new { id, terminal, destination=endpoints.Destination, now, day, reference = ExportKey(tenant.TenantId, draftId, route.Vehicle), fingerprint }, tx, cancellationToken: ct));
            long routingStop = 0;
            for (var i = 0; i < endpoints.RoutingTerminals.Length; i++)
            {
                routingStop = await NextId(fb, tx, false, ct);
                await fb.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION)
                    VALUES(@routingStop,@id,@number,'L',@location)
                    """, new { routingStop, id, number=i+1, location=endpoints.RoutingTerminals[i] }, tx, cancellationToken: ct));
            }
            // Transfer membership belongs to the receiving-terminal routing stop. The native
            // order's consignee/address, delivery status and completion times are not changed.
            var sequence = 0;
            foreach (var pro in Ordered(route))
            {
                var native = Native(root, pro.ProNumber);
                await fb.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_SEQUENCE,OSM_PKUP_DELV_ID)
                    VALUES(@routingStop,@sequence,@Stop)
                    """, new { routingStop, sequence = ++sequence, native.Stop }, tx, cancellationToken: ct));
                await fb.ExecuteAsync(new CommandDefinition("""
                    UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=@id,OPS_MANIFESTED_TIME=@now WHERE OPS_STOP_ID=@Stop
                    """, new { id, now, native.Stop }, tx, cancellationToken: ct));
            }
            await VerifyManifest(fb, tx, id, endpoints, Ordered(route).Select(p => Native(root, p.ProNumber).Stop).ToArray(), ct);
            // Intentionally unassigned: no DRIVER_PKUP_DELV/EQUIP_STATUS writes.
            result.Add(route.Vehicle, id);
        }
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
        return result;
    }

    private static async Task VerifyManifest(FbConnection fb, FbTransaction tx, long id, ManifestEndpoints endpoints, long[] expected, CancellationToken ct)
    {
        var valid = await fb.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='M'
                AND PB_LOCATION=@Origin AND PBORIG=@Origin AND PBDEST=@Destination AND PB_DT_CANC IS NULL
            """, new { id, endpoints.Origin, endpoints.Destination }, tx, cancellationToken: ct));
        var stops = (await fb.QueryAsync<ManifestRoutingStop>(new CommandDefinition("""
            SELECT OPS_STOP_ID AS Id, OPS_STOP_NUMBER AS Sequence, TRIM(OPS_MFST_LOC_TYPE) AS Kind,
                TRIM(OPS_MFST_LOCATION) AS Location FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id ORDER BY OPS_STOP_NUMBER
            """, new { id }, tx, cancellationToken: ct))).ToArray();
        var members = (await fb.QueryAsync<ManifestMember>(new CommandDefinition("""
            SELECT m.OSM_STOP_ID AS RoutingStop, m.OSM_PKUP_DELV_ID AS OrderStop FROM OPS_MANIFEST_STOPS m
            JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=m.OSM_STOP_ID
            WHERE r.OPS_ORDER_ID=@id ORDER BY r.OPS_STOP_NUMBER,m.OSM_SEQUENCE
            """, new { id }, tx, cancellationToken: ct))).ToArray();
        if (valid != 1 || stops.Length != endpoints.RoutingTerminals.Length ||
            stops.Where((s,i) => s.Sequence != i+1 || s.Kind != "L" || s.Location != endpoints.RoutingTerminals[i]).Any() ||
            !members.Select(m => m.OrderStop).SequenceEqual(expected) || members.Any(m => m.RoutingStop != stops[^1].Id))
            throw new FormatException("The Nova manifest's terminals or membership differ from this plan. Reconcile it before retrying Create Manifest.");
    }
    private sealed class ManifestRoutingStop { public long Id { get; set; } public int Sequence { get; set; } public string Kind { get; set; } = ""; public string Location { get; set; } = ""; }
    private sealed class ManifestMember { public long RoutingStop { get; set; } public long OrderStop { get; set; } }
    private static IEnumerable<ManifestProDto> Ordered(RouteSummaryDto route) => route.Pros.OrderBy(p => p.StopNumber);
    private sealed record NativeOrder(long Order, long Stop);
    private static NativeOrder Native(JsonObject root, string id)
    {
        var row = root["reporting"]?["orders"]?[id];
        var order = row?["nativeOrderId"]?.GetValue<long>();
        var stop = row?["nativeStopId"]?.GetValue<long>();
        if (order is null or <= 0 || stop is null or <= 0 || id != $"FB-{order}")
            throw new FormatException($"Order {id} has no verified Nova identity.");
        return new(order.Value, stop.Value);
    }
    private static async Task<long> NextId(FbConnection fb, FbTransaction tx, bool manifest, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var id = await fb.ExecuteScalarAsync<long>(new CommandDefinition(manifest
                ? "SELECT GEN_ID(ORDER_GEN,1) FROM RDB$DATABASE" : "SELECT GEN_ID(OPS_STOP_GEN,1) FROM RDB$DATABASE", transaction: tx, cancellationToken: ct));
            var count = await fb.ExecuteScalarAsync<int>(new CommandDefinition(manifest
                ? "SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=@id" : "SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_STOP_ID=@id", new { id }, tx, cancellationToken: ct));
            if (count == 0) return id;
        }
        throw new FormatException("Nova could not allocate an unused manifest/stop number.");
    }
    private sealed class ExportedManifest
    {
        public long Id { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }
}
