using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;
public sealed partial class FirebirdRoutingSource
{
    private sealed class DriverBooking
    {
        public long Id {get;set;} public long? Driver {get;set;} public long? Planned {get;set;} public long? SecondDriver {get;set;} public long? Linked {get;set;}
        public DateTime? Starts {get;set;} public string? Notes {get;set;} public string Status {get;set;}="";
    }
    private static async Task<HashSet<long>> BookedDrivers(FbConnection db,FbTransaction tx,long[] excluding,DateOnly first,DateOnly last,CancellationToken ct)
    {
        var rows=await db.QueryAsync<DriverBooking>(new CommandDefinition("""
            SELECT m.PB_ID AS Id,m.PB_CURRENT_DRIVER AS Driver,m.PB_PLANNED_DRIVER AS Planned,m.PB_PLANNED_DRIVER2 AS SecondDriver,
                d.DPD_ID AS Linked,m.PB_MFST_DATE AS Starts,m.PB_NOTES AS Notes,COALESCE(TRIM(s.CTS_CODE),'') AS Status
            FROM OPS_HEADER m LEFT JOIN CT_STAT s ON s.CTS_ID=m.PB_STATUS LEFT JOIN DRIVER_PKUP_DELV d ON d.DPD_LOAD_NUMBER=m.PB_ID
            WHERE m.PB_TYPE='M' AND m.PB_DT_CANC IS NULL AND COALESCE(s.CTS_CODE,'') NOT IN ('CPLT','VOID','DONE')
                AND m.PB_ID NOT IN @excluding
            """,new{excluding=excluding.Length==0?new long[]{-1}:excluding},tx,cancellationToken:ct));
        return rows.Where(b=>b.Starts is null || b.Status is "DPCH" or "ENR" or "OFD" or "CITY" or "ONBD" or "LH" or "DPT"
            || DateOnly.FromDateTime(b.Starts.Value)<=last&&(ManifestNotes.TripUntil(b.Notes)??DateOnly.FromDateTime(b.Starts.Value))>=first)
            .SelectMany(b=>new[]{b.Driver,b.Planned,b.SecondDriver,b.Linked}).Where(id=>id>0).Select(id=>id!.Value).ToHashSet();
    }
    public async Task<string> ApplyDrivers(string json,string basis,bool initialize,CancellationToken ct)
    {
        await using var db=await Open(ct);await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        return await ApplyDrivers(db,tx,json,basis,initialize,ct);
    }
    private static async Task<string> ApplyDrivers(FbConnection db,FbTransaction tx,string json,string basis,bool initialize,CancellationToken ct)
    {
        var root=RoutingInput.Parse(json);var trucks=RoutingInput.Items(root["vehicles"]).ToArray();
        var first=trucks.Min(t=>RoutingInput.Time(t["start"]?["earliestStartTime"]))??throw new FormatException("Set departure times.");
        var last=trucks.Max(t=>RoutingInput.Time(t["end"]?["latestEndTime"]))??throw new FormatException("Set return times.");
        var booked=await BookedDrivers(db,tx,ManifestReplanning.Included(json).Select(m=>m.Id).ToArray(),DateOnly.FromDateTime(first.DateTime),DateOnly.FromDateTime(last.DateTime),ct);
        return DriverPlanning.Apply(json,await Drivers(db,tx,ct),booked,basis,initialize);
    }
}
