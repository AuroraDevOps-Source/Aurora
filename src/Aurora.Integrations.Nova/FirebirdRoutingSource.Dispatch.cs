using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    public async Task<NovaDispatchDocument> ReadDispatch(long id, CancellationToken ct)
    {
        await using var db = await Open(ct);
        await using var tx = db.BeginTransaction(new FbTransactionOptions { TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.Concurrency | FbTransactionBehavior.Wait });
        var result = await DispatchDocument(db, tx, id, false, ct);
        var header = await DispatchHeader(db, tx, id, false, ct);
        await tx.RollbackAsync(ct);
        var parts = header.Reference.Split(':');
        if (parts.Length == 4 && parts[0] == "aurora" && parts[1] == tenant.TenantId.ToString("N") && Guid.TryParseExact(parts[2], "N", out var draft))
        {
            await using var pg = await factory.OpenConnectionAsync(ct);
            var routes = await pg.QueryAsync<string>(new CommandDefinition("SELECT data::text FROM aurora_manifest WHERE tenant_id=@TenantId AND draft_id=@draft AND number=@number AND deleted_at IS NULL", new { tenant.TenantId, draft, number=id.ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken:ct));
            var route = routes.Select(json=>JsonSerializer.Deserialize<RouteSummaryDto>(json)).FirstOrDefault(r=>r is not null && ExportKey(tenant.TenantId,draft,r.Vehicle)==header.Reference);
            result = result with { PlannedRoute=route };
        }
        return result;
    }

    public async Task<NovaDispatchDocument> SaveManifestNotes(long id, NovaManifestNotesEdit input, string actor, CancellationToken ct)
    {
        await using var db = await Open(ct);
        await using var tx = BeginCreate(db);
        var result = await WriteManifestNotes(db, tx, id, input, actor, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    internal async Task<NovaDispatchDocument> WriteManifestNotes(FbConnection db, FbTransaction tx, long id, NovaManifestNotesEdit input, string actor, CancellationToken ct)
    {
        var header = await DispatchHeader(db, tx, id, true, ct);
        var current = await DispatchDocument(db, tx, id, false, ct);
        if (!current.CanEditNotes) throw new FormatException("Notes cannot change on a closed or canceled manifest.");
        if (current.Revision != input.Revision) throw new FormatException("This manifest changed. Refresh it before saving notes.");
        var notes = ManifestNotes.Write(header.Notes, input.Notes);
        var initials = actor.Trim();
        if (initials.Length > 8) initials = initials[..8];
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
        await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_NOTES=@notes,PB_ORDER_MODIFIED_INIT=@initials,PB_ORDER_MODIFIED_TIME=@now WHERE PB_ID=@id",new{id,notes,initials,now},tx,cancellationToken:ct));
        return await DispatchDocument(db, tx, id, false, ct);
    }

    public async Task<NovaDispatchDocument> SaveDispatch(long id, NovaDispatchEdit input, bool release, string actor, CancellationToken ct, bool rollbackOnly = false)
    {
        await using var db = await Open(ct);
        await using var tx = BeginCreate(db);
        var result = await WriteDispatch(db, tx, id, input, release, actor, ct);
        if (rollbackOnly) await tx.RollbackAsync(ct); else await tx.CommitAsync(ct);
        return result;
    }

    internal async Task<NovaDispatchDocument> WriteDispatch(FbConnection db, FbTransaction tx, long id, NovaDispatchEdit input, bool release, string actor, CancellationToken ct)
    {
        // Serialize changes to a manifest, then lock resources in stable order. This also
        // serializes two dispatchers trying to book the same driver/truck on different loads.
        var original = await DispatchHeader(db, tx, id, true, ct);
        foreach (var resource in new[] { input.DriverId, input.TruckId, original.Driver ?? 0, original.Truck ?? 0 }.Where(x => x > 0).Distinct().Order())
            if (await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT EH_ID FROM EQUIP_HEADER WHERE EH_ID=@resource AND EH_DELETE_DATE IS NULL WITH LOCK", new { resource }, tx, cancellationToken: ct)) is null)
                throw new FormatException("Select an active driver and truck.");
        var current = await DispatchDocument(db, tx, id, true, ct);
        // A lost response can safely be retried. Never repeat history or move a released load backwards.
        if (release && current.Dispatched && current.DriverId == input.DriverId && current.TruckId == input.TruckId) return current;
        if (!current.CanEdit) throw new FormatException("This manifest has already started or is closed. Refresh the manifest to see its current status.");
        if (current.Revision != input.Revision) throw new FormatException("This manifest changed. Refresh it before assigning or dispatching.");
        if (current.Issues.Length > 0) throw new FormatException(string.Join(" ", current.Issues));
        var driver = current.Drivers.SingleOrDefault(d => d.Id == input.DriverId) ?? throw new FormatException("Select an active driver.");
        var truck = current.Trucks.SingleOrDefault(t => t.Id == input.TruckId) ?? throw new FormatException("Select an active truck.");
        if (driver.Unavailable is not null) throw new FormatException($"{driver.Name}: {driver.Unavailable}");
        if (truck.Unavailable is not null) throw new FormatException($"{truck.Name}: {truck.Unavailable}");
        if (current.PlannedTruckId is {} planned && planned != input.TruckId)
            throw new FormatException("Use the truck from the accepted optimization plan. Changing trucks requires a new plan.");
        if(original.PlannedDriver is >0 && original.PlannedDriver!=input.DriverId)throw new FormatException("Use the driver from the accepted optimization plan. Changing drivers requires a new plan.");
        var header = await DispatchHeader(db, tx, id, false, ct);
        var links = await DispatchLinks(db, tx, id, ct);
        if (links.Length > 1 || links.Any(l => l.Driver != current.DriverId || l.Active != "A"))
            throw new FormatException("This manifest has a different Nova equipment assignment. Reconcile it in Nova before dispatching here.");
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
        var initials = actor.Trim();
        if (initials.Length > 8) initials = initials[..8];
        var status = await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT CTS_ID FROM CT_STAT WHERE CTS_CODE=@code", new { code = release ? "DPCH" : "ASG" }, tx, cancellationToken: ct))
            ?? throw new FormatException("Nova needs an Assigned (ASG) and Dispatched (DPCH) status before using Dispatch.");
        if (links.Length == 1 && links[0].Driver != input.DriverId)
            await db.ExecuteAsync(new CommandDefinition("DELETE FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=@id AND DPD_ID=@oldDriver", new { id, oldDriver = links[0].Driver }, tx, cancellationToken: ct));
        if (links.Length == 0 || links[0].Driver != input.DriverId)
        {
            var sequence = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COALESCE(MAX(DPD_SEQUENCE),0)+1 FROM DRIVER_PKUP_DELV WHERE DPD_ID=@DriverId", input, tx, cancellationToken: ct));
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO DRIVER_PKUP_DELV(DPD_ID,DPD_SEQUENCE,DPD_LOAD_NUMBER,DPD_ACTIVE_PLAN) VALUES(@DriverId,@sequence,@id,'A')", new { input.DriverId, sequence, id }, tx, cancellationToken: ct));
        }
        var args = new { id, input.DriverId, input.TruckId, status, now, initials, terminal = header.Terminal };
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE OPS_HEADER SET PB_CURRENT_DRIVER=@DriverId,PB_CURRENT_TRACTOR=@TruckId,
                PB_STATUS=@status,PB_STATUS_TIME=@now,PB_STATUS_LOC_TYPE='L',PB_STATUS_LOCATION=@terminal,
                PB_ORDER_MODIFIED_INIT=@initials,PB_ORDER_MODIFIED_TIME=@now WHERE PB_ID=@id
            """, args, tx, cancellationToken: ct));
        if (release)
        {
            // Release is not departure, delivery or completion. Route sequence and membership stay intact.
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE OPS_STOP_REC SET OPS_DRIVER1=@DriverId,OPS_TRACTOR=@TruckId,
                    OPS_DISPATCH_TIME=@now,OPS_STATUS=@status,OPS_STATUS_TIME=@now,
                    OPS_LOC_TYPE='L',OPS_LOCATION=@terminal,OPS_UPDATED_BY=@initials
                WHERE OPS_ORDER_ID=@id
                """, args, tx, cancellationToken: ct));
            if (header.Origin == header.Destination)
            {
                foreach (var member in await DispatchMembers(db, tx, id, false, ct))
                {
                    await db.ExecuteAsync(new CommandDefinition("""
                        UPDATE OPS_STOP_REC SET OPS_DRIVER1=@DriverId,OPS_TRACTOR=@TruckId,
                            OPS_DISPATCH_TIME=@now,OPS_STATUS=@status,OPS_STATUS_TIME=@now,
                            OPS_LOC_TYPE='L',OPS_LOCATION=@terminal,OPS_UPDATED_BY=@initials WHERE OPS_STOP_ID=@Stop
                        """, new { input.DriverId, input.TruckId, status, now, initials, terminal = header.Terminal, member.Stop }, tx, cancellationToken: ct));
                    // Nova's stop trigger uses SQL <> and misses a NULL -> value transition.
                    await db.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE SET_ORDER_HDR_STATUS @OrderId", new { member.OrderId }, tx, cancellationToken: ct));
                    await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_CURRENT_DRIVER=@DriverId,PB_CURRENT_TRACTOR=@TruckId,PB_ORDER_MODIFIED_INIT=@initials,PB_ORDER_MODIFIED_TIME=@now WHERE PB_ID=@OrderId", new { input.DriverId, input.TruckId, initials, now, member.OrderId }, tx, cancellationToken: ct));
                }
            }
            foreach (var resource in new[] { input.DriverId, input.TruckId })
                await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE OR INSERT INTO EQUIP_STATUS(ES_ID,ES_STATUS,ES_STATUS_DATE,ES_LOC_TYPE,ES_LOCATION,
                        ES_MANIFEST_NUMBER,ES_INITIALS) VALUES(@resource,@status,@now,'L',@terminal,@id,@initials) MATCHING(ES_ID)
                    """, new { resource, status, now, terminal = header.Terminal, id, initials }, tx, cancellationToken: ct));
            // EQUIP_STATUS clears its manifest pointer after writing history; DRIVER_PKUP_DELV
            // plus the manifest header remain the authoritative active assignment.
        }
        return await DispatchDocument(db, tx, id, false, ct);
    }

    internal async Task<NovaDispatchDocument> DispatchDocument(FbConnection db, FbTransaction tx, long id, bool locked, CancellationToken ct)
    {
        var h = await DispatchHeader(db, tx, id, false, ct);
        var consolidation=ManifestNotes.Consolidation(h.Notes);
        var consolidationLoad=consolidation is null?null:await ConsolidationLoad(db,tx,id,consolidation,ct);
        var members = await DispatchMembers(db, tx, id, locked, ct);
        var links = await DispatchLinks(db, tx, id, ct);
        var equipment = (await db.QueryAsync<DispatchEquipment>(new CommandDefinition("""
            SELECT e.EH_ID AS Id,TRIM(e.EH_UNIT_ID) AS Name,TRIM(e.EH_DOMICILE) AS Terminal,
                TRIM(t.CTT_CODE) AS Kind,e.EH_MAX_WEIGHT AS Weight,e.EH_MAX_CUBE AS Cubes
            FROM EQUIP_HEADER e JOIN CT_TYPE t ON t.CTT_ID=e.EH_UNIT_TYPE
            WHERE e.EH_DELETE_DATE IS NULL AND t.CTT_CODE IN ('DRV','TRK') ORDER BY e.EH_UNIT_ID
            """, transaction: tx, cancellationToken: ct))).ToArray();
        var busy = (await db.QueryAsync<DispatchBooking>(new CommandDefinition("""
            SELECT m.PB_ID AS ManifestId,m.PB_CURRENT_DRIVER AS Driver,m.PB_CURRENT_TRACTOR AS Truck,
                d.DPD_ID AS LinkedResource,m.PB_MFST_DATE AS Starts,m.PB_NOTES AS Notes,TRIM(s.CTS_CODE) AS Status FROM OPS_HEADER m
            LEFT JOIN CT_STAT s ON s.CTS_ID=m.PB_STATUS
            LEFT JOIN DRIVER_PKUP_DELV d ON d.DPD_LOAD_NUMBER=m.PB_ID
            WHERE m.PB_TYPE='M' AND m.PB_ID<>@id AND m.PB_DT_CANC IS NULL
                AND COALESCE(s.CTS_CODE,'') NOT IN ('CPLT','VOID','DONE')
                AND (CAST(m.PB_MFST_DATE AS DATE) BETWEEN CAST(@day AS DATE) AND CAST(@last AS DATE)
                    OR m.PB_NOTES CONTAINING 'Aurora trip until v1 '
                    OR s.CTS_CODE IN ('DPCH','ENR','OFD','CITY','ONBD','LH','DPT'))
            """, new { id, day = h.Date, last = (ManifestNotes.TripUntil(h.Notes)?.ToDateTime(TimeOnly.MinValue)) ?? h.Date }, tx, cancellationToken: ct))).Where(b=>b.Status is not ("UNAS" or "ASG") || b.Starts?.Date <= ((ManifestNotes.TripUntil(h.Notes)?.ToDateTime(TimeOnly.MinValue)) ?? h.Date) && ((ManifestNotes.TripUntil(b.Notes)?.ToDateTime(TimeOnly.MinValue)) ?? b.Starts?.Date) >= h.Date?.Date).ToArray();
        var reservedTrucks=new HashSet<string>();
        if(h.Date is {} manifestDay)
        {
            var first=DateOnly.FromDateTime(manifestDay);var last=ManifestNotes.TripUntil(h.Notes)??first;
            if(last.DayNumber-first.DayNumber>13)throw new FormatException("Review the manifest trip dates.");
            for(var day=first;day<=last;day=day.AddDays(1))reservedTrucks.UnionWith(await BookedTrucks(db,tx,[id],day,ct));
        }
        var profiles=await Drivers(db,tx,ct);
        var reservedDrivers=h.Date is {} driverDate?await BookedDrivers(db,tx,[id],DateOnly.FromDateTime(driverDate),ManifestNotes.TripUntil(h.Notes)??DateOnly.FromDateTime(driverDate),ct):new HashSet<long>();
        var issues = new List<string>();
        if(consolidation is {Held:true})issues.Add("Release this consolidation manifest before assigning or dispatching it.");
        if(consolidation is not null && consolidationLoad is null)issues.Add("Review the consolidation manifest's order data and capacity before dispatching.");
        if (h.Date is null) issues.Add("Set a manifest date in Nova before dispatching.");
        if (members.Length == 0) issues.Add("Add orders to this manifest before dispatching.");
        if (members.Any(m => m.Cancelled is not null || m.Completed || m.ManifestId != id || m.OrderTerminal != h.Terminal))
            issues.Add("An order was canceled, delivered, moved to another terminal, or removed from this manifest. Refresh and reconcile the manifest in Nova.");
        if (members.Select(m => m.Stop).Distinct().Count() != members.Length) issues.Add("A stop appears more than once on this manifest.");
        if (string.IsNullOrWhiteSpace(h.Terminal) || await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LOCMAST WHERE LM_ID=@Terminal", h, tx, cancellationToken: ct)) != 1)
            issues.Add("The manifest needs a valid terminal.");
        var code = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT TRIM(CTS_CODE) FROM CT_STAT WHERE CTS_ID=@StatusId", h, tx, cancellationToken: ct)) ?? "";
        var dispatchTime = await db.ExecuteScalarAsync<DateTime?>(new CommandDefinition("SELECT MAX(OPS_DISPATCH_TIME) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id", new { id }, tx, cancellationToken: ct));
        var started = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND (OPS_DEPART_TIME IS NOT NULL OR OPS_DELIVERED_TIME IS NOT NULL)", new { id }, tx, cancellationToken: ct));
        var execution = await ManifestExecution(db, tx, id, ct);
        var closeIssue = members.Length == 0 ? "This manifest has no orders." :
            members.Any(m=>m.Cancelled is not null || m.ManifestId != id) ? "Reconcile canceled or reassigned orders in Nova before closing." :
            h.Origin == h.Destination
                ? members.Any(m=>!m.Completed) ? "Record each pickup/delivery or reconcile unfinished freight in Nova before closing." : null
                : execution.LastOrDefault()?.Delivered is null ? "Record receipt at the destination terminal in Nova before closing." : null;
        var canEdit = h.Cancelled is null && (code is "UNAS" or "ASG" || h.StatusId is null or 0) && dispatchTime is null && started == 0;
        var planned = PlannedDispatchTruck(h.Reference, equipment.Where(e => e.Kind == "TRK").Select(e => (e.Id, e.Name)), tenant.TenantId);
        if (h.Reference.StartsWith("aurora:", StringComparison.Ordinal) && planned is null)
            issues.Add("The truck from the accepted optimization plan is no longer available. Review the plan before dispatching.");
        var peak=PickupCapacity.Peak(members.OrderBy(m=>m.RouteSequence).ThenBy(m=>m.Sequence).Select(m=>(m.StopKind=="P",m.Weight??0,m.Cubes??0)));
        NovaDispatchOption Option(DispatchEquipment e)
        {
            var conflict = busy.FirstOrDefault(b => b.Driver == e.Id || b.Truck == e.Id || b.LinkedResource == e.Id);
            var unavailable = conflict is not null ? $"Already assigned to manifest {conflict.ManifestId}." : null;
            if (e.Kind == "TRK" && reservedTrucks.Contains(e.Name)) unavailable ??= "Reserved by another manifest during this trip.";
            if (e.Kind == "TRK" && !string.Equals(e.Terminal, h.Terminal, StringComparison.OrdinalIgnoreCase)) unavailable ??= "This truck belongs to another terminal.";
            if (e.Kind == "TRK" && (!(e.Weight > 0) || !(e.Cubes > 0))) unavailable ??= "Enter weight and cubic capacity in Fleet Management.";
            if (e.Kind == "TRK" && (Math.Max(peak.Weight,(double)(consolidationLoad?.Weight??0)) > e.Weight || Math.Max(peak.Cubes,(double)(consolidationLoad?.Cubes??0)) > e.Cubes)) unavailable ??= "The manifest exceeds this truck's capacity.";
            var name=e.Name;
            if(e.Kind=="DRV")
            {
                var profile=profiles.SingleOrDefault(d=>d.Id==e.Id);
                if(profile is not null)
                {
                    name=$"{e.Name} — {profile.Name}";
                    var end=(ManifestNotes.TripUntil(h.Notes)?.ToDateTime(TimeOnly.MinValue))??h.Date;
                    if(profile.TerminationDate?.Date<=end?.Date || profile.HireDate?.Date>h.Date?.Date)unavailable??="Driver is not employed for the full trip.";
                    if(profile.LicenseExpiry?.Date<end?.Date)unavailable??="License expires before this trip is complete.";
                }
                if(reservedDrivers.Contains(e.Id))unavailable??="Reserved by another manifest during this trip.";
                if(h.PlannedDriver is >0 && h.PlannedDriver!=e.Id)unavailable??="Use the driver from the accepted optimization plan.";
            }
            return new(e.Id, name, e.Terminal, unavailable);
        }
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { h, members, links, dispatchTime, started, execution }))));
        return new(id, revision, code switch { "DPCH"=>"Dispatched", "ENR"=>"In transit", "CPLT"=>"Closed", _=>h.Status }, h.Date, h.Terminal, h.Driver, h.Truck,
            planned?.Id, planned?.Name, canEdit, code == "DPCH" && h.Cancelled is null, dispatchTime,
            equipment.Where(e => e.Kind == "DRV").Select(Option).ToArray(), equipment.Where(e => e.Kind == "TRK").Select(Option).ToArray(), issues.ToArray())
        {
            PlannedDriverId=h.PlannedDriver,
            Consolidation=consolidationLoad,
            StatusCode=code, TimeZone=Options.TimeZoneId,
            OptimizationLocked=ManifestNotes.OptimizationLocked(h.Notes),
            ManifestType=consolidation is not null?"Customer consolidation":ManifestNotes.Receipt(h.Notes).StartsWith("Aurora consolidation ",StringComparison.Ordinal) ? "Consolidation" : h.Origin!=h.Destination ? "Line haul" : "Delivery",
            Origin=h.Origin, Destination=h.Destination,
            Notes=ManifestNotes.Read(h.Notes), CanEditNotes=h.Cancelled is null && code is not ("CPLT" or "VOID" or "DONE"), OrderCount=members.Select(m=>m.OrderId).Distinct().Count(),
            AllowedStatuses=h.Cancelled is not null ? [] : canEdit ? ["ASG","DPCH"] : code == "DPCH" ? ["ENR"] : code == "ENR" ? ["CPLT"] : [],
            DepartedAt=execution.FirstOrDefault()?.Departed,
            ClosedAt=code == "CPLT" ? execution.LastOrDefault()?.BackIn : null,
            CloseIssue=closeIssue,
            History=(await db.QueryAsync<NovaManifestHistory>(new CommandDefinition("""
                SELECT h.OPH_TIME AS RecordedAt,h.OPH_STATUS_TIME AS OccurredAt,COALESCE(TRIM(h.OPH_INITIALS),'') AS Operator,
                    COALESCE(TRIM(prior.CTS_DESC),'â€”') AS PreviousStatus,COALESCE(TRIM(next.CTS_DESC),'Unknown') AS Status
                FROM OPS_HISTORY h LEFT JOIN CT_STAT prior ON prior.CTS_ID=h.OPH_PRIOR_STATUS
                LEFT JOIN CT_STAT next ON next.CTS_ID=h.OPH_STATUS
                WHERE h.OPH_ORDER_ID=@id AND COALESCE(h.OPH_STOP_ID,0)=0
                    AND (COALESCE(h.OPH_PRIOR_STATUS,0)<>COALESCE(h.OPH_STATUS,0) OR h.OPH_REASON='Manifest created')
                ORDER BY h.OPH_TIME DESC,h.OPH_SEQUENCE DESC
                """,new{id},tx,cancellationToken:ct))).ToArray()
        };
    }

    public static (long Id, string Name)? PlannedDispatchTruck(string reference, IEnumerable<(long Id, string Name)> trucks, Guid tenantId)
    {
        var parts = reference.Split(':');
        if (parts.Length != 4 || parts[0] != "aurora" || parts[1] != tenantId.ToString("N") || !Guid.TryParseExact(parts[2], "N", out var draft)) return null;
        foreach (var truck in trucks) if (ExportKey(tenantId, draft, truck.Name) == reference) return truck;
        return null;
    }

    private static async Task<DispatchHeaderRow> DispatchHeader(FbConnection db, FbTransaction tx, long id, bool locked, CancellationToken ct)
    {
        if (locked && await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT PB_ID FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='M' WITH LOCK", new { id }, tx, cancellationToken: ct)) is null)
            throw new KeyNotFoundException("Manifest not found.");
        return await db.QuerySingleOrDefaultAsync<DispatchHeaderRow>(new CommandDefinition("""
            SELECT m.PB_ID AS Id,m.PB_STATUS AS StatusId,COALESCE(TRIM(s.CTS_DESC),'Unassigned') AS Status,
                m.PB_MFST_DATE AS "Date",TRIM(m.PB_LOCATION) AS Terminal,TRIM(m.PBORIG) AS Origin,TRIM(m.PBDEST) AS Destination,
                m.PB_PLANNED_DRIVER AS PlannedDriver,m.PB_CURRENT_DRIVER AS Driver,m.PB_CURRENT_TRACTOR AS Truck,m.PB_DT_CANC AS Cancelled,
                COALESCE(TRIM(m.PB_EXTERNAL_REF),'') AS Reference, m.PB_NOTES AS Notes
            FROM OPS_HEADER m LEFT JOIN CT_STAT s ON s.CTS_ID=m.PB_STATUS WHERE m.PB_ID=@id AND m.PB_TYPE='M'
            """, new { id }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException("Manifest not found.");
    }

    private static async Task<DispatchMember[]> DispatchMembers(FbConnection db, FbTransaction tx, long id, bool locked, CancellationToken ct)
    {
        if (locked)
        {
            await db.QueryAsync<long>(new CommandDefinition("SELECT PB_ID FROM OPS_HEADER WHERE PB_ID IN (SELECT s.OPS_ORDER_ID FROM OPS_STOP_REC s JOIN OPS_MANIFEST_STOPS l ON l.OSM_PKUP_DELV_ID=s.OPS_STOP_ID JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=l.OSM_STOP_ID WHERE r.OPS_ORDER_ID=@id) ORDER BY PB_ID WITH LOCK", new { id }, tx, cancellationToken: ct));
            await db.QueryAsync<long>(new CommandDefinition("SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id OR OPS_STOP_ID IN (SELECT l.OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS l JOIN OPS_STOP_REC r ON r.OPS_STOP_ID=l.OSM_STOP_ID WHERE r.OPS_ORDER_ID=@id) ORDER BY OPS_STOP_ID WITH LOCK", new { id }, tx, cancellationToken: ct));
        }
        return (await db.QueryAsync<DispatchMember>(new CommandDefinition("""
            SELECT r.OPS_STOP_ID AS RouteStop,r.OPS_STOP_NUMBER AS RouteSequence,l.OSM_SEQUENCE AS Sequence,s.OPS_STOP_ID AS Stop,
                s.OPS_ORDER_ID AS OrderId,s.OPS_MANIFEST_NUMBER AS ManifestId,s.OPS_DELIVERED_TIME AS Delivered,TRIM(s.OPS_TYPE) AS StopKind,TRIM(s.OPS_PICKED_UP) AS PickedUp,
                h.PB_DT_CANC AS Cancelled,TRIM(h.PB_LOCATION) AS OrderTerminal,h.PBTWEIGH AS Weight,h.PB_TTL_CUBE AS Cubes
            FROM OPS_STOP_REC r JOIN OPS_MANIFEST_STOPS l ON l.OSM_STOP_ID=r.OPS_STOP_ID
            JOIN OPS_STOP_REC s ON s.OPS_STOP_ID=l.OSM_PKUP_DELV_ID JOIN OPS_HEADER h ON h.PB_ID=s.OPS_ORDER_ID
            WHERE r.OPS_ORDER_ID=@id ORDER BY r.OPS_STOP_NUMBER,l.OSM_SEQUENCE,s.OPS_STOP_ID
            """, new { id }, tx, cancellationToken: ct))).ToArray();
    }
    private static async Task<DispatchLink[]> DispatchLinks(FbConnection db, FbTransaction tx, long id, CancellationToken ct) =>
        (await db.QueryAsync<DispatchLink>(new CommandDefinition("SELECT DPD_ID AS Driver,DPD_SEQUENCE AS Sequence,TRIM(DPD_ACTIVE_PLAN) AS Active FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=@id ORDER BY DPD_ID,DPD_SEQUENCE", new { id }, tx, cancellationToken: ct))).ToArray();
    private sealed class DispatchHeaderRow { public long? PlannedDriver {get;set;} public string? Notes { get; set; } public long Id { get; set; } public long? StatusId { get; set; } public string Status { get; set; } = ""; public DateTime? Date { get; set; } public string Terminal { get; set; } = ""; public string Origin { get; set; } = ""; public string Destination { get; set; } = ""; public long? Driver { get; set; } public long? Truck { get; set; } public DateTime? Cancelled { get; set; } public string Reference { get; set; } = ""; }
    private sealed class DispatchEquipment { public long Id { get; set; } public string Name { get; set; } = ""; public string Terminal { get; set; } = ""; public string Kind { get; set; } = ""; public double? Weight { get; set; } public double? Cubes { get; set; } }
    private sealed class DispatchBooking { public DateTime? Starts {get;set;} public string? Notes {get;set;} public string? Status {get;set;} public long ManifestId { get; set; } public long? Driver { get; set; } public long? Truck { get; set; } public long? LinkedResource { get; set; } }
    private sealed class DispatchLink { public long Driver { get; set; } public int Sequence { get; set; } public string Active { get; set; } = ""; }
    private sealed class DispatchMember { public string StopKind {get;set;}="D"; public string? PickedUp {get;set;} public bool Completed=>Delivered is not null || (StopKind=="P" && PickedUp=="Y"); public long RouteStop { get; set; } public int RouteSequence { get; set; } public int Sequence { get; set; } public long Stop { get; set; } public long OrderId { get; set; } public long? ManifestId { get; set; } public DateTime? Delivered { get; set; } public DateTime? Cancelled { get; set; } public string OrderTerminal { get; set; } = ""; public double? Weight { get; set; } public double? Cubes { get; set; } }
}

