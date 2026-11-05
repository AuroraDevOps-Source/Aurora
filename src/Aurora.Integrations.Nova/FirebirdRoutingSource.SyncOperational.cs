using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    private async Task WriteOperationalOrder(FbConnection db,FbTransaction tx,long id,JsonObject data,JsonObject baseline,CancellationToken ct)
    {
        LykesNovaOperationalChanges.ValidateOrder(data,baseline);
        var order=data.Deserialize<TmsOrder>()!;
        var stops=(await db.QueryAsync<SyncStop>(new CommandDefinition("SELECT OPS_STOP_ID AS Id,TRIM(OPS_TYPE) AS Kind,OPS_DELIVERED_TIME AS Delivered FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id WITH LOCK",new{id},tx,cancellationToken:ct))).ToArray();
        if(stops.Length!=2||stops.Count(s=>s.Kind=="P")!=1||stops.Count(s=>s.Kind=="D")!=1)
            throw new SyncConflictException("Operational sync requires one pickup and one delivery.");
        if(stops.Any(s=>s.Delivered is not null))throw new SyncConflictException("A delivered native order requires review before editing.");
        foreach(var stop in stops)
        {
            var fields=(stop.Kind=="P"?SyncPickupFields:SyncDeliveryFields)
                .Where(f=>!SyncJson.Equal(data[f.Property],baseline[f.Property],f.Property)).ToArray();
            if(fields.Length==0)continue;
            // Validate native column limits before any updates; the outer transaction includes the receipt.
            foreach(var field in fields)
            {
                var definition=StopFields.SingleOrDefault(f=>f.Column==field.Column);
                if(definition is not null)ParseField(NewField(definition),NativeValue(data[field.Property])?.ToString());
            }
            await WriteProjection(db,tx,"OPS_STOP_REC","OPS_STOP_ID",stop.Id,fields,data,true,ct);
        }
        var zone=TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId);
        var active=stops.Single(s=>s.Kind==(order.WorkType=="Pickup"?"P":"D"));
        if(!SyncJson.Equal(data["AppointmentFrom"],baseline["AppointmentFrom"],"AppointmentFrom")||!SyncJson.Equal(data["AppointmentTo"],baseline["AppointmentTo"],"AppointmentTo"))
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_STOP_REC SET OPS_APPT_LOW=@low,OPS_APPT_HIGH=@high WHERE OPS_STOP_ID=@id",new{id=active.Id,
            low=order.AppointmentFrom is {} a?TimeZoneInfo.ConvertTime(a,zone).DateTime:(DateTime?)null,
            high=order.AppointmentTo is {} b?TimeZoneInfo.ConvertTime(b,zone).DateTime:(DateTime?)null},tx,cancellationToken:ct));
        if(!SyncJson.Equal(data["Notes"],baseline["Notes"],"Notes"))
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_NOTES=@notes WHERE PB_ID=@id",new{id,notes=order.Notes},tx,cancellationToken:ct));
    }
}
