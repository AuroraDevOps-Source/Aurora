using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    private sealed class ManifestExecutionStop
    {
        public long Id { get; set; }
        public DateTime? Departed { get; set; }
        public DateTime? Delivered { get; set; }
        public DateTime? BackIn { get; set; }
    }
    private static async Task<ManifestExecutionStop[]> ManifestExecution(FbConnection db, FbTransaction tx, long id, CancellationToken ct) =>
        (await db.QueryAsync<ManifestExecutionStop>(new CommandDefinition("SELECT OPS_STOP_ID AS Id,OPS_DEPART_TIME AS Departed,OPS_DELIVERED_TIME AS Delivered,OPS_BACKIN_TIME AS BackIn FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id ORDER BY OPS_STOP_NUMBER,OPS_STOP_ID",new{id},tx,cancellationToken:ct))).ToArray();

    public async Task<NovaDispatchDocument> SaveManifestStatus(long id, NovaManifestStatusEdit input, string actor, CancellationToken ct)
    {
        await using var db=await Open(ct);
        await using var tx=BeginCreate(db);
        var result=await WriteManifestStatus(db,tx,id,input,actor,ct);
        await tx.CommitAsync(ct);
        return result;
    }

    internal async Task<NovaDispatchDocument> WriteManifestStatus(FbConnection db,FbTransaction tx,long id,NovaManifestStatusEdit input,string actor,CancellationToken ct)
    {
        if(input.StatusCode is "ASG" or "DPCH")
            return await WriteDispatch(db,tx,id,new(input.Revision,input.DriverId,input.TruckId),input.StatusCode=="DPCH",actor,ct);
        if(input.StatusCode is not ("ENR" or "CPLT")) throw new FormatException("Choose an available manifest status.");
        var header=await DispatchHeader(db,tx,id,true,ct);
        foreach(var resource in new[]{header.Driver??0,header.Truck??0}.Where(x=>x>0).Distinct().Order())
            await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT EH_ID FROM EQUIP_HEADER WHERE EH_ID=@resource WITH LOCK",new{resource},tx,cancellationToken:ct));
        var current=await DispatchDocument(db,tx,id,true,ct);
        if(header.Cancelled is not null) throw new FormatException("A canceled manifest cannot change status.");
        if(current.DriverId!=input.DriverId || current.TruckId!=input.TruckId || input.DriverId<=0 || input.TruckId<=0)
            throw new FormatException("Driver and truck assignments cannot change after dispatch.");
        var zone=TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId);
        var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).DateTime;
        if(input.OccurredAt is not {} supplied) throw new FormatException("Enter the actual event date and time.");
        var occurred=DateTime.SpecifyKind(supplied,DateTimeKind.Unspecified);
        if(zone.IsInvalidTime(occurred) || zone.IsAmbiguousTime(occurred)) throw new FormatException("Choose an unambiguous local event time outside the daylight-saving clock change.");
        // A lost response can be retried without adding duplicate history.
        if(current.StatusCode==input.StatusCode && (input.StatusCode=="ENR"?current.DepartedAt:current.ClosedAt)==occurred) return current;
        if(current.Revision!=input.Revision) throw new FormatException("This manifest changed. Refresh before saving its status.");
        if(!current.AllowedStatuses.Contains(input.StatusCode)) throw new FormatException("This status transition is not available. Refresh the manifest.");
        var previous=input.StatusCode=="ENR"?current.DispatchedAt:current.DepartedAt;
        if(previous is null || occurred<previous || occurred>now.AddMinutes(1)) throw new FormatException("The event time must follow the previous event and cannot be in the future.");
        if(input.StatusCode=="ENR" && current.Issues.Length>0) throw new FormatException(string.Join(" ",current.Issues));
        if(input.StatusCode=="CPLT" && current.CloseIssue is not null) throw new FormatException(current.CloseIssue);
        var stops=await ManifestExecution(db,tx,id,ct);
        if(stops.Length==0) throw new FormatException("This manifest has no routing stops.");
        var members=await DispatchMembers(db,tx,id,false,ct);
        if(input.StatusCode=="CPLT" && (members.Any(m=>m.Delivered>occurred) || stops.Any(s=>s.Delivered>occurred)))
            throw new FormatException("Closing time cannot precede the recorded delivery or receipt time.");
        var status=await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT CTS_ID FROM CT_STAT WHERE CTS_CODE=@StatusCode",input,tx,cancellationToken:ct))
            ?? throw new FormatException("The selected status is not configured in Nova.");
        var initials=actor.Trim();if(initials.Length>8)initials=initials[..8];
        var terminal=input.StatusCode=="CPLT"?header.Destination:header.Terminal;
        var stop=input.StatusCode=="ENR"?stops[0].Id:stops[^1].Id;
        // Only the route's departure/return event is recorded here. Never invent order deliveries.
        var timeColumn=input.StatusCode=="ENR"?"OPS_DEPART_TIME":"OPS_BACKIN_TIME";
        await db.ExecuteAsync(new CommandDefinition($"UPDATE OPS_STOP_REC SET {timeColumn}=@occurred,OPS_STATUS=@status,OPS_STATUS_TIME=@occurred,OPS_UPDATED_BY=@initials WHERE OPS_STOP_ID=@stop",new{occurred,status,initials,stop},tx,cancellationToken:ct));
        if(input.StatusCode=="ENR" && header.Origin==header.Destination)
            foreach(var member in members)
            {
                await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_STOP_REC SET OPS_STATUS=@status,OPS_STATUS_TIME=@occurred,OPS_UPDATED_BY=@initials WHERE OPS_STOP_ID=@Stop AND OPS_DELIVERED_TIME IS NULL",new{status,occurred,initials,member.Stop},tx,cancellationToken:ct));
                await db.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE SET_ORDER_HDR_STATUS @OrderId",new{member.OrderId},tx,cancellationToken:ct));
            }
        await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_STATUS=@status,PB_STATUS_TIME=@occurred,PB_STATUS_LOC_TYPE='L',PB_STATUS_LOCATION=@terminal,PB_ORDER_MODIFIED_INIT=@initials,PB_ORDER_MODIFIED_TIME=@now WHERE PB_ID=@id",new{status,occurred,terminal,initials,now,id},tx,cancellationToken:ct));
        foreach(var resource in new[]{input.DriverId,input.TruckId})
            await db.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO EQUIP_STATUS(ES_ID,ES_STATUS,ES_STATUS_DATE,ES_LOC_TYPE,ES_LOCATION,ES_MANIFEST_NUMBER,ES_INITIALS) VALUES(@resource,@status,@occurred,'L',@terminal,@id,@initials) MATCHING(ES_ID)",new{resource,status,occurred,terminal,id,initials},tx,cancellationToken:ct));
        if(input.StatusCode=="CPLT")
            await db.ExecuteAsync(new CommandDefinition("DELETE FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=@id",new{id},tx,cancellationToken:ct));
        return await DispatchDocument(db,tx,id,false,ct);
    }
}
