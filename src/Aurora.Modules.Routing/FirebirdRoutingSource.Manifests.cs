using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    public async Task<NovaOrderDetails> ReadOrderDetails(long manifestId, long orderId, CancellationToken ct)
    {
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions { TransactionBehavior=FbTransactionBehavior.Read | FbTransactionBehavior.Concurrency | FbTransactionBehavior.Wait });
        var args=new { manifestId, orderId };
        var detail=await db.QuerySingleOrDefaultAsync<NovaOrderDetails>(new CommandDefinition("""
            SELECT TRIM(h.PBBNAME) AS BillTo,
                TRIM(COALESCE(h.PBBADDR1,'') || ' ' || COALESCE(h.PBBADDR2,'') || ' ' || COALESCE(h.PBBCITY,'') || ' ' || COALESCE(h.PBBSTATE,'') || ' ' || COALESCE(h.PBBZIP,'')) AS BillToAddress,
                TRIM(h.PBPURORD) AS PurchaseOrder, TRIM(h.PB_CUST_ORD) AS CustomerOrder,
                TRIM(h.PB_COMMODITY) AS Commodity, h.PB_NOTES AS Notes, TRIM(h.PB_OTHER_CARRIERS) AS CarrierReferences
            FROM OPS_HEADER h WHERE h.PB_ID=@orderId AND h.PB_TYPE='O' AND EXISTS (
                SELECT 1 FROM OPS_HEADER m JOIN OPS_STOP_REC r ON r.OPS_ORDER_ID=m.PB_ID
                JOIN OPS_MANIFEST_STOPS link ON link.OSM_STOP_ID=r.OPS_STOP_ID
                JOIN OPS_STOP_REC s ON s.OPS_STOP_ID=link.OSM_PKUP_DELV_ID
                WHERE m.PB_ID=@manifestId AND m.PB_TYPE='M' AND m.PB_DT_CANC IS NULL AND s.OPS_ORDER_ID=h.PB_ID)
            """,args,tx,cancellationToken:ct)) ?? throw new KeyNotFoundException("This order is no longer on this manifest. Refresh the manifest list.");
        detail.Stops=(await db.QueryAsync<NovaOrderParty>(new CommandDefinition("""
            SELECT TRIM(OPS_TYPE) AS Kind, TRIM(OPS_NAME) AS Name,
                TRIM(COALESCE(OPS_ADDR1,'') || ' ' || COALESCE(OPS_ADDR2,'') || ' ' || COALESCE(OPS_ADDR3,'')) AS Address,
                TRIM(OPS_CITY) AS City, TRIM(OPS_STATE) AS State, TRIM(OPS_ZIP) AS PostalCode,
                TRIM(OPS_CONTACT) AS Contact, TRIM(OPS_PHONE) AS Phone, TRIM(OPS_CONTACT_EMAIL) AS Email,
                OPS_NOTES AS Notes, OPS_APPT_LOW AS AppointmentFrom, OPS_APPT_HIGH AS AppointmentTo
            FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@orderId ORDER BY OPS_STOP_NUMBER,OPS_STOP_ID
            """,args,tx,cancellationToken:ct))).ToArray();
        detail.Items=(await db.QueryAsync<NovaOrderItem>(new CommandDefinition("SELECT TRIM(OPL_DESC) AS Description, TRIM(OPL_SKU) AS Sku, OPL_PIECES AS Pieces, OPL_WEIGHT AS Weight, OPL_CUBE AS Cube FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@orderId ORDER BY OPL_SEQUENCE",args,tx,cancellationToken:ct))).ToArray();
        detail.Agents=(await db.QueryAsync<string>(new CommandDefinition("SELECT TRIM(OPA_AGENT_ID) FROM OPS_AGENTS WHERE OPA_ORDER_ID=@orderId ORDER BY OPA_SEQUENCE",args,tx,cancellationToken:ct))).ToArray();
        await tx.RollbackAsync(ct);
        return detail;
    }
    public async Task<NovaManifestList> ReadManifests(DateOnly? from, DateOnly? to, int? status, bool allDates, CancellationToken ct, bool manifestDates = false)
    {
        RequireEnabled();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime);
        if (!allDates) { from ??= today; to ??= today; }
        if (from > to) throw new FormatException("The end date must be on or after the start date.");
        await using var db = await Open(ct);
        await using var tx = db.BeginTransaction(new FbTransactionOptions { TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.Concurrency | FbTransactionBehavior.Wait });
        var conditions = new List<string> { "h.PB_TYPE='M'", "h.PB_DT_CANC IS NULL" };
        var args = new DynamicParameters();
        var dateColumn = manifestDates ? "h.PB_MFST_DATE" : "h.PB_DT_ENT";
        if (from is {} first) { conditions.Add(dateColumn+">=@First"); args.Add("First", first.ToDateTime(TimeOnly.MinValue)); }
        if (to is {} last) { conditions.Add(dateColumn+"<@EndExclusive"); args.Add("EndExclusive", last.ToDateTime(TimeOnly.MinValue).AddDays(1)); }
        if (status is {} state) { conditions.Add("h.PB_STATUS=@Status"); args.Add("Status", state); }
        var rows = (await db.QueryAsync<NovaManifest>(new CommandDefinition("""
            SELECT h.PB_ID AS Id, h.PB_DT_ENT AS CreatedAt, h.PB_MFST_DATE AS ManifestDate,
                h.PB_STATUS AS StatusId, CASE TRIM(st.CTS_CODE) WHEN 'DPCH' THEN 'Dispatched' WHEN 'ENR' THEN 'In transit' WHEN 'CPLT' THEN 'Closed' ELSE COALESCE(TRIM(st.CTS_DESC),'Unknown') END AS Status,
                TRIM(h.PB_LOCATION) AS Terminal, COALESCE(TRIM(tr.EH_UNIT_ID),'') AS Truck,
                COALESCE(TRIM(dr.EH_UNIT_ID),'') AS Driver,
                COALESCE(NULLIF(TRIM(h.PBORIG),''),TRIM(h.PB_LOCATION)) AS StartTerminal,
                depot.LM_LATITUDE AS DepotLatitude, depot.LM_LONGITUDE AS DepotLongitude,
                TRIM(h.PBDEST) AS EndTerminal, destination.LM_LATITUDE AS EndLatitude, destination.LM_LONGITUDE AS EndLongitude,
                (SELECT COUNT(DISTINCT delivery.OPS_ORDER_ID) FROM OPS_STOP_REC route
                 JOIN OPS_MANIFEST_STOPS link ON link.OSM_STOP_ID=route.OPS_STOP_ID
                 JOIN OPS_STOP_REC delivery ON delivery.OPS_STOP_ID=link.OSM_PKUP_DELV_ID
                 WHERE route.OPS_ORDER_ID=h.PB_ID) AS OrderCount
            FROM OPS_HEADER h LEFT JOIN CT_STAT st ON st.CTS_ID=h.PB_STATUS
            LEFT JOIN EQUIP_HEADER tr ON tr.EH_ID=h.PB_CURRENT_TRACTOR
            LEFT JOIN EQUIP_HEADER dr ON dr.EH_ID=h.PB_CURRENT_DRIVER
            LEFT JOIN LOCMAST depot ON depot.LM_ID=COALESCE(NULLIF(TRIM(h.PBORIG),''),TRIM(h.PB_LOCATION))
            LEFT JOIN LOCMAST destination ON destination.LM_ID=h.PBDEST
            WHERE
            """ + " " + string.Join(" AND ", conditions) + " ORDER BY h.PB_DT_ENT DESC,h.PB_ID DESC", args, tx, cancellationToken:ct))).ToArray();
        var statuses = (await db.QueryAsync<NovaManifestStatus>(new CommandDefinition("SELECT CAST(CTS_ID AS INTEGER) AS Id, CASE TRIM(CTS_CODE) WHEN 'DPCH' THEN 'Dispatched' WHEN 'ENR' THEN 'In transit' WHEN 'CPLT' THEN 'Closed' ELSE TRIM(CTS_DESC) END AS Name FROM CT_STAT WHERE CTS_MFST_VISIBLE='Y' OR CTS_ID=1 OR CTS_CODE IN ('ASG','DPCH','ENR','CPLT') ORDER BY CTS_DESC", transaction:tx, cancellationToken:ct))).ToArray();
        await tx.RollbackAsync(ct);
        return new(today, statuses, rows);
    }

    public async Task<NovaManifestOrder[]> ReadManifestOrders(long id, CancellationToken ct)
    {
        await using var db = await Open(ct);
        return (await db.QueryAsync<NovaManifestOrder>(new CommandDefinition("""
            SELECT h.PB_ID AS Id, COALESCE(CAST(NULLIF(h.PBNUM,0) AS VARCHAR(30)), 'FB-' || CAST(h.PB_ID AS VARCHAR(30))) AS Pro,
                link.OSM_SEQUENCE AS Sequence, TRIM(s.OPS_NAME) AS Customer,
                TRIM(s.OPS_ADDR1) AS Address, TRIM(s.OPS_CITY) AS City,
                TRIM(s.OPS_STATE) AS State, TRIM(s.OPS_ZIP) AS PostalCode,
                s.OPS_LATITUDE AS Latitude, s.OPS_LONGITUDE AS Longitude,
                COALESCE(TRIM(status.CTS_DESC),'Unknown') AS Status, TRIM(h.PBBOL) AS Reference,
                h.PBTPIECE AS Pieces, h.PBTWEIGH AS Weight, h.PB_TTL_CUBE AS Cube,
                s.OPS_APPT_LOW AS AppointmentFrom, s.OPS_APPT_HIGH AS AppointmentTo
            FROM OPS_HEADER manifest JOIN OPS_STOP_REC route ON route.OPS_ORDER_ID=manifest.PB_ID
            JOIN OPS_MANIFEST_STOPS link ON link.OSM_STOP_ID=route.OPS_STOP_ID
            JOIN OPS_STOP_REC s ON s.OPS_STOP_ID=link.OSM_PKUP_DELV_ID
            JOIN OPS_HEADER h ON h.PB_ID=s.OPS_ORDER_ID
            LEFT JOIN CT_STAT status ON status.CTS_ID=h.PB_STATUS
            WHERE manifest.PB_TYPE='M' AND manifest.PB_ID=@id AND manifest.PB_DT_CANC IS NULL
            ORDER BY route.OPS_STOP_NUMBER,link.OSM_SEQUENCE
            """, new { id }, cancellationToken:ct))).ToArray();
    }
}
