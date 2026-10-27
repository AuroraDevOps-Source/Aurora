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
            throw new RecordErrorException($"The address for terminal {node.Code} could not be located. Check it in Terminals and save again.", new("terminals",node.Code,node.Code));
        var root = RoutingInput.Parse(json);
        var depot = RoutingInput.Items(root["locations"]).Single(l => RoutingInput.Text(l["id"]) == "DEPOT");
        depot["latitude"] = node.Latitude; depot["longitude"] = node.Longitude;
        foreach (var location in RoutingInput.Items(root["locations"]))
        {
            if (!HasCoordinates(location["latitude"]?.GetValue<double>(), location["longitude"]?.GetValue<double>()))
            {
                var id = RoutingInput.Text(location["id"])!;
                var report = root["reporting"]?["orders"]?[id];
                var number = long.TryParse(report?["nativePro"]?.ToString(), out var pro) && pro != 0
                    ? pro.ToString(System.Globalization.CultureInfo.InvariantCulture) : id;
                var address = RoutingInput.Text(report?["nativeStopKind"]) == "P" ? "pickup" : "delivery";
                throw new RecordErrorException($"The {address} address for order {number} could not be located. Check the order's address and save again.", new("orders", report?["nativeOrderId"]?.ToString() ?? id.Replace("FB-P-", "").Replace("FB-", ""), number));
            }
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
            throw new RecordErrorException($"The address for terminal {node.Code} could not be located. Check it in Terminals and save again.", new("terminals",node.Code,node.Code));
    }

    public async Task<string> ResolveOrdersForPlanning(string json, CancellationToken ct)
    {
        ValidateSource(json);
        var root = RoutingInput.Parse(json);
        foreach (var location in RoutingInput.Items(root["locations"]))
        {
            if (RoutingInput.Text(location["id"]) == "DEPOT" ||
                HasCoordinates(location["latitude"]?.GetValue<double>(), location["longitude"]?.GetValue<double>())) continue;
            if(ManifestReplanning.Included(json).SelectMany(m=>m.OrderIds).Contains(RoutingInput.Text(location["id"])!))
                throw new FormatException("An existing manifest order needs valid coordinates. Correct its address before starting a new plan.");
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
        await RejectConsolidationOrders(draftJson,ct);
        await using var configDb = await factory.OpenConnectionAsync(ct);
        var readyStatuses = await ReadyStatuses(configDb, null, ct);
        var pickupStatuses = await PickupReadyStatuses(configDb,null,ct);
        await using var fb = await Open(ct);
        await using var tx = fb.BeginTransaction(new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.Write | FbTransactionBehavior.ReadCommitted |
                FbTransactionBehavior.NoRecVersion | FbTransactionBehavior.Wait,
            WaitTimeout = TimeSpan.FromSeconds(15)
        });
        return await ExportCore(fb,tx,draftId,sessionId,draftJson,routes,readyStatuses,pickupStatuses,ct,rollbackOnly);
    }

    internal async Task<Dictionary<string,long>> ExportCore(FbConnection fb,FbTransaction tx,Guid draftId,Guid sessionId,string draftJson,
        IReadOnlyList<RouteSummaryDto> routes,int[] readyStatuses,int[] pickupStatuses,CancellationToken ct,bool rollbackOnly=false)
    {
        ValidateSource(draftJson);
        if (routes.Count == 0 || routes.Select(r => r.Vehicle).Distinct().Count() != routes.Count)
            throw new FormatException("Export requires distinct nonempty routes.");
        var root = RoutingInput.Parse(draftJson);
        var endpoints = ExportEndpoints(draftJson);
        LineHaulPlanning.ValidateResult(draftJson, routes);
        ManifestReplanning.ValidateResult(draftJson,routes);
        PickupPlanning.ValidateReturn(draftJson,routes);
        var terminal = endpoints.Origin;
        var prefix = ExportPrefix(tenant.TenantId, draftId);
        var fingerprint = ExportFingerprint(sessionId, routes, endpoints.Transfer ? endpoints.Destination : null, RoutingInput.Text(root["reporting"]?["transferKind"]));
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
        // Serialize bookings against other exports and dispatcher assignments.
        foreach(var unit in routes.Select(r=>r.Vehicle).Order(StringComparer.Ordinal))
            await fb.QueryAsync<long>(new CommandDefinition("SELECT EH_ID FROM EQUIP_HEADER WHERE TRIM(EH_UNIT_ID)=@unit ORDER BY EH_ID WITH LOCK",new{unit},tx,cancellationToken:ct));
        var zone=TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId);
        var plannedDrivers=new Dictionary<string,long?>();
        var driverBasis=RoutingInput.Text(root["reporting"]?["driverCostBasis"]);
        if(driverBasis is not null)
        {
            if(driverBasis!=DriverCostBasis.Separate)throw new FormatException("Run optimization again to include equipment and driver labor before creating manifests.");
            foreach(var route in routes)
            {
                if(!long.TryParse(RoutingInput.Text(root["reporting"]?["drivers"]?[route.Vehicle]?["id"]),out var driverId)||driverId<=0)
                    throw new FormatException("The route is missing its planned driver. Start a new plan.");
                plannedDrivers.Add(route.Vehicle,driverId);
            }
            if(plannedDrivers.Values.Distinct().Count()!=plannedDrivers.Count)throw new FormatException("A driver cannot operate two routes in this export.");
            foreach(var driverId in plannedDrivers.Values.Order())
            {
                await fb.QueryAsync<long>(new CommandDefinition("SELECT EH_ID FROM EQUIP_HEADER WHERE EH_ID=@driverId WITH LOCK",new{driverId},tx,cancellationToken:ct));
                await fb.QueryAsync<long>(new CommandDefinition("SELECT DH_ID FROM DRIVER_HEADER WHERE DH_ID=@driverId WITH LOCK",new{driverId},tx,cancellationToken:ct));
            }
            var profiles=await Drivers(fb,tx,ct);
            foreach(var route in routes)
            {
                var profile=profiles.SingleOrDefault(d=>d.Id==plannedDrivers[route.Vehicle]);
                if(profile is null || profile.Revision!=RoutingInput.Text(root["reporting"]?["drivers"]?[route.Vehicle]?["revision"]))throw new FormatException($"The driver for truck {route.Vehicle} changed. Start a new plan.");
                if(route.Departure is not {} begins || route.Return is not {} ends)throw new FormatException("Driver routes need departure and return times.");
                begins=TimeZoneInfo.ConvertTime(begins,zone);ends=TimeZoneInfo.ConvertTime(ends,zone);
                if(ends<=begins)throw new FormatException("Route return must be after departure.");
                if(DriverPlanning.Unavailable(profile,begins,ends) is {} reason)throw new FormatException($"Driver {profile.Number}: {reason}");
                if(ends-begins<=TimeSpan.FromHours(24))
                {
                    var first=TimeOnly.ParseExact(RoutingInput.Text(root["reporting"]?["drivers"]?[route.Vehicle]?["shiftStart"])??profile.ShiftStart,"HH:mm",System.Globalization.CultureInfo.InvariantCulture);
                    var last=TimeOnly.ParseExact(RoutingInput.Text(root["reporting"]?["drivers"]?[route.Vehicle]?["shiftEnd"])??profile.ShiftEnd,"HH:mm",System.Globalization.CultureInfo.InvariantCulture);
                    var shift=new DateTimeOffset(DateOnly.FromDateTime(begins.DateTime).ToDateTime(first),begins.Offset);
                    if(last<first && TimeOnly.FromDateTime(begins.DateTime)<last)shift=shift.AddDays(-1);
                    var duration=last-first;if(duration<=TimeSpan.Zero)duration+=TimeSpan.FromDays(1);
                    if(begins<shift || ends>shift+duration)throw new FormatException($"Route exceeds driver {profile.Number}'s working hours. Run a new plan.");
                }
                if((await BookedDrivers(fb,tx,ManifestReplanning.Included(draftJson).Select(m=>m.Id).ToArray(),DateOnly.FromDateTime(begins.DateTime),DateOnly.FromDateTime(ends.DateTime),ct)).Contains(profile.Id))throw new FormatException($"Driver {profile.Number} was reserved by another manifest. Start a new plan.");
            }
        }
        foreach(var route in routes)
        {
            var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(route.Departure??DateTimeOffset.UtcNow,zone).DateTime);
            if(ManifestReplanning.Included(draftJson).Any(m=>m.Date!=date))throw new FormatException("The route date differs from the included manifests. Start a new plan.");
            if(RoutingInput.Parse(draftJson)["reporting"]?["tripRestRules"] is not null && (route.Return is null || route.Departure is null))throw new FormatException("The multi-day route is missing departure or return times. Run optimization again before creating manifests.");
            var end=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(route.Return??route.Departure??DateTimeOffset.UtcNow,zone).DateTime);
            if(end<date||end.DayNumber-date.DayNumber>14)throw new FormatException("The route has an invalid planning horizon.");
            for(var day=date;day<=end;day=day.AddDays(1))
                if((await BookedTrucks(fb,tx,ManifestReplanning.Included(draftJson).Select(m=>m.Id).ToArray(),day,ct)).Contains(route.Vehicle))
                    throw new FormatException($"Truck {route.Vehicle} was reserved by another manifest. Nothing was saved; start a new plan.");
        }
        var reused=await PrepareManifestReplacement(fb,tx,draftJson,routes,ct);
        var existingOrders=ManifestReplanning.Included(draftJson).SelectMany(m=>m.OrderIds).Select(id=>Native(root,id).Order).Distinct().ToArray();
        var equipment = new Dictionary<string, PlannedManifestEquipment>();
        foreach (var route in routes)
        {
            var matches = (await fb.QueryAsync<NovaFleetRow>(new CommandDefinition("""
                SELECT h.EH_ID AS NativeId, TRIM(h.EH_UNIT_ID) AS Unit,
                    TRIM(t.CTT_CODE) AS Kind, TRIM(t.CTT_DESC) AS Description
                FROM EQUIP_HEADER h JOIN CT_TYPE t ON t.CTT_ID=h.EH_UNIT_TYPE
                WHERE h.EH_DELETE_DATE IS NULL AND TRIM(h.EH_UNIT_ID)=@unit
                """, new { unit = route.Vehicle }, tx, cancellationToken: ct))).ToArray();
            equipment.Add(route.Vehicle, PlannedEquipment(route.Vehicle, matches));
        }
        // Lock every order/stop in stable order and validate the whole batch before creating
        // any manifest. A stale, canceled or already-manifested order fails the whole export.
        var stops = routes.SelectMany(Ordered).Select(p => Native(root, p.ProNumber)).OrderBy(n => n.Order).ToArray();
        if (stops.Length == 0 || stops.Select(s => s.Stop).Distinct().Count() != stops.Length)
            throw new FormatException("The exported routes contain empty or repeated order stops.");
        foreach (var native in stops)
        {
            var ready = await fb.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
                SELECT PB_ID FROM OPS_HEADER WHERE PB_ID=@Order AND PB_TYPE='O' AND (PB_STATUS IN @readyStatuses OR PB_ID IN @existingOrders)
                    AND PB_LOCATION=@terminal AND PB_DT_CANC IS NULL WITH LOCK
                """, new { native.Order, readyStatuses=native.Kind=="P"?pickupStatuses:readyStatuses, existingOrders=existingOrders.Length==0?new long[]{-1}:existingOrders, terminal }, tx, cancellationToken: ct));
            var stop = await fb.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
                SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_STOP_ID=@Stop AND OPS_ORDER_ID=@Order
                    AND OPS_TYPE=@Kind AND COALESCE(OPS_MANIFEST_NUMBER,0)=0 AND OPS_DELIVERED_TIME IS NULL
                    AND (OPS_TYPE<>'P' OR COALESCE(OPS_PICKED_UP,'N')<>'Y') WITH LOCK
                """, native, tx, cancellationToken: ct));
            var linked = await fb.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_PKUP_DELV_ID=@Stop", native, tx, cancellationToken: ct));
            if (ready is null || stop is null || linked != 0)
                throw new FormatException($"Nova order {native.Order} changed or is already manifested. Refresh its source before finishing.");
        }
        foreach (var route in routes)
        {
            var replacing=reused.TryGetValue(route.Vehicle,out var id);
            if(!replacing)id=await NextId(fb,tx,true,ct);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
            var day = route.Departure is { } departure
                ? TimeZoneInfo.ConvertTime(departure, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).Date : now.Date;
            var oldNotes=replacing?await fb.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@id",new{id},tx,cancellationToken:ct)):null;
            var savedNotes=ManifestNotes.WithTripUntil(ManifestNotes.WithReceipt(oldNotes,fingerprint),DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(route.Return??route.Departure??DateTimeOffset.UtcNow,zone).DateTime));
            if(replacing)
                await fb.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_EXTERNAL_REF=@reference,PB_NOTES=@savedNotes,PB_MFST_DATE=@day,PB_PLANNED_TRAILER=@trailer,PB_PLANNED_TRACTOR=@tractor,PB_PLANNED_DRIVER=@driver WHERE PB_ID=@id",
                    new{id,reference=ExportKey(tenant.TenantId,draftId,route.Vehicle),savedNotes,day,trailer=equipment[route.Vehicle].Trailer,tractor=equipment[route.Vehicle].Tractor,driver=plannedDrivers.GetValueOrDefault(route.Vehicle)},tx,cancellationToken:ct));
            else await fb.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE,PB_EXTERNAL_REF,PB_NOTES,PB_PLANNED_TRAILER,PB_PLANNED_TRACTOR,PB_PLANNED_DRIVER)
                VALUES(@id,'M','M',@terminal,@terminal,@destination,1,@now,@day,@reference,@fingerprint,@trailer,@tractor,@driver)
                """, new { id, terminal, destination=endpoints.Destination, now, day, reference = ExportKey(tenant.TenantId, draftId, route.Vehicle), fingerprint=savedNotes, trailer=equipment[route.Vehicle].Trailer, tractor=equipment[route.Vehicle].Tractor,driver=plannedDrivers.GetValueOrDefault(route.Vehicle) }, tx, cancellationToken: ct));
            long routingStop = 0;
            for (var i = 0; i < endpoints.RoutingTerminals.Length; i++)
            {
                if(replacing)
                {
                    routingStop=await fb.QuerySingleAsync<long>(new CommandDefinition("SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_STOP_NUMBER=@number",new{id,number=i+1},tx,cancellationToken:ct));
                    continue;
                }
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
            var planned = equipment[route.Vehicle];
            var verified = await fb.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=@id
                    AND PB_PLANNED_TRAILER IS NOT DISTINCT FROM @Trailer
                    AND PB_PLANNED_TRACTOR IS NOT DISTINCT FROM @Tractor
                    AND PB_PLANNED_DRIVER IS NOT DISTINCT FROM @driver
                """, new { id, planned.Trailer, planned.Tractor,driver=plannedDrivers.GetValueOrDefault(route.Vehicle) }, tx, cancellationToken: ct));
            if (verified != 1) throw new FormatException("Nova did not retain the planned equipment for this manifest.");
            // Planning only: no active assignment or DRIVER_PKUP_DELV/EQUIP_STATUS writes.
            result.Add(route.Vehicle, id);
        }
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
        return result;
    }

    public sealed record PlannedManifestEquipment(long? Trailer, long? Tractor);
    public static PlannedManifestEquipment PlannedEquipment(string unit, IReadOnlyList<NovaFleetRow> matches)
    {
        if (matches.Count != 1 || matches[0].NativeId <= 0 || matches[0].Unit != unit ||
            string.Equals(matches[0].Kind, "DRV", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Equipment {unit} is missing or ambiguous in Nova. Refresh the fleet before creating manifests.");
        var equipment = matches[0];
        // Nova uses planned trailer for freight-bearing units, including straight trucks.
        // Keep the same tractor classification used by the imported fleet's PowerOnly flag.
        return equipment.Description.Contains("tractor", StringComparison.OrdinalIgnoreCase)
            ? new(null, equipment.NativeId) : new(equipment.NativeId, null);
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
    private static IEnumerable<ManifestProDto> Ordered(RouteSummaryDto route) => route.Pros.OrderBy(p => p.StopNumber).ThenBy(p=>p.WorkType=="Pickup");
    private sealed record NativeOrder(long Order, long Stop, string Kind);
    private static NativeOrder Native(JsonObject root, string id)
    {
        var row = root["reporting"]?["orders"]?[id];
        var order = row?["nativeOrderId"]?.GetValue<long>();
        var stop = row?["nativeStopId"]?.GetValue<long>();
        var kind=RoutingInput.Text(row?["nativeStopKind"])??"D";
        var expected=kind=="P"?$"FB-P-{order}":$"FB-{order}";
        if (kind is not ("P" or "D") || order is null or <= 0 || stop is null or <= 0 || id != expected)
            throw new FormatException($"Order {id} has no verified Nova identity.");
        return new(order.Value, stop.Value,kind);
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
