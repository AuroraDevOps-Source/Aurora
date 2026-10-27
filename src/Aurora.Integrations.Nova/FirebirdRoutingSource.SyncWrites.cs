using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;
public sealed partial class FirebirdRoutingSource
{
    private async Task<string> WriteSyncOrder(FbConnection db,FbTransaction tx,string? external,JsonObject data,SyncChange change,CancellationToken ct)
    {
        var order=data.Deserialize<TmsOrder>()!;order.Validate();await ValidTerminal(db,tx,order.TerminalCode,ct);
        await using var pgConfig=await factory.OpenConnectionAsync(ct);
        var readyStatuses=order.WorkType=="Pickup"?await PickupReadyStatuses(pgConfig,null,ct):await ReadyStatuses(pgConfig,null,ct);
        if(readyStatuses.Length==0 && (external is null || order.Status==WorkspaceOrderStatus.Ready))throw new FormatException("Configure a ready status for this work type.");
        var ready=readyStatuses.FirstOrDefault();
        var id=external is null?await NextId(db,tx,true,ct):long.Parse(external,CultureInfo.InvariantCulture);
        if(external is null)
        {
            if(!long.TryParse(order.Id,NumberStyles.None,CultureInfo.InvariantCulture,out var pro)||pro<=0)throw new FormatException("New Nova orders require a positive numeric PRO.");
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_TYPE='O' AND PBNUM=@pro",new{pro},tx,cancellationToken:ct))>0)throw new SyncConflictException("The PRO already exists in Nova.");
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PBNUM,PB_STATUS,PB_LOCATION,PB_DT_ENT,PB_EXTERNAL_REF)
                VALUES(@id,'O','O',@pro,@status,@location,CURRENT_TIMESTAMP,@reference)
                """,new{id,pro,status=ready,location=order.TerminalCode,reference=$"aurora-sync:{change.Id:N}"},tx,cancellationToken:ct));
            foreach(var kind in new[]{"P","D"})
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_TYPE) VALUES(@stop,@id,@sequence,@kind)",new{stop=await NextId(db,tx,false,ct),id,sequence=kind=="P"?1:2,kind},tx,cancellationToken:ct));
        }
        var stops=(await db.QueryAsync<SyncStop>(new CommandDefinition("SELECT OPS_STOP_ID AS Id,TRIM(OPS_TYPE) AS Kind,OPS_APPT_LOW AS AppointmentFrom,OPS_APPT_HIGH AS AppointmentTo,OPS_DELIVERED_TIME AS Delivered,OPS_MANIFEST_NUMBER AS Manifest FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE IN ('P','D') WITH LOCK",new{id},tx,cancellationToken:ct))).ToArray();
        if(stops.Length!=2||stops.Select(s=>s.Kind).Distinct().Count()!=2)throw new SyncConflictException("The native order has unsupported multiple stops.");
        if(stops.Any(s=>s.Delivered is not null))throw new SyncConflictException("A delivered native order is read-only.");
        await WriteProjection(db,tx,"OPS_HEADER","PB_ID",id,SyncOrderFields,data,true,ct);
        foreach(var stop in stops)
            await WriteProjection(db,tx,"OPS_STOP_REC","OPS_STOP_ID",stop.Id,stop.Kind=="P"?SyncPickupFields:SyncDeliveryFields,data,true,ct);
        var zone=TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId);
        var delivery=stops.Single(s=>s.Kind==(order.WorkType=="Pickup"?"P":"D"));
        await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_STOP_REC SET OPS_APPT_LOW=@low,OPS_APPT_HIGH=@high WHERE OPS_STOP_ID=@id",new{id=delivery.Id,low=order.AppointmentFrom is {} a?TimeZoneInfo.ConvertTime(a,zone).DateTime:(DateTime?)null,high=order.AppointmentTo is {} b?TimeZoneInfo.ConvertTime(b,zone).DateTime:(DateTime?)null},tx,cancellationToken:ct));
        // Execution statuses and membership are exported by the manifest transaction,
        // never by guessing a native event timestamp from an order status label.
        if(order.Status==WorkspaceOrderStatus.Ready && stops.All(s=>s.Manifest is null or 0))
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_STATUS=@ready WHERE PB_ID=@id",new{id,ready},tx,cancellationToken:ct));
        return id.ToString(CultureInfo.InvariantCulture);
    }
    private sealed record SyncAssignment(Guid SourceId,string Id,int StopNumber);
    private async Task<string> WriteSyncManifest(FbConnection db,FbTransaction tx,string? external,JsonObject data,SyncChange change,CancellationToken ct)
    {
        var manifest=data.Deserialize<TmsManifest>()!;manifest.Validate();
        if(manifest.Status is not ("Planning" or "Dispatched" or "En Route" or "Complete"))
            throw new FormatException("Nova synchronization supports Planning, Dispatched, En Route and Complete manifest statuses.");
        await ValidTerminal(db,tx,manifest.Details.OriginTerminal,ct);await ValidTerminal(db,tx,manifest.Details.DestinationTerminal,ct);
        if(string.IsNullOrWhiteSpace(manifest.Details.OriginTerminal)||string.IsNullOrWhiteSpace(manifest.Details.DestinationTerminal))throw new FormatException("Set both manifest terminals.");
        var members=data["Orders"]?.Deserialize<SyncAssignment[]>()??throw new FormatException("Manifest membership is required.");
        var stops=new List<long>();
        foreach(var member in members.OrderBy(m=>m.StopNumber).ThenBy(m=>m.Id))
        {
            var mapped=await Mapping("order",$"{member.SourceId:D}/{member.Id}",false,ct)??throw new InvalidOperationException("The manifest's orders have not synchronized yet.");
            var baseline=JsonNode.Parse(mapped.Baseline)!.AsObject();var work=S(baseline,"WorkType")=="Pickup"?"P":"D";
            var stop=await db.QuerySingleAsync<long>(new CommandDefinition("SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE=@work",new{id=long.Parse(mapped.ExternalId,CultureInfo.InvariantCulture),work},tx,cancellationToken:ct));stops.Add(stop);
        }
        var driver=manifest.Details.DriverId is {} localDriver?await Mapping("driver",localDriver.ToString(CultureInfo.InvariantCulture),false,ct):null;
        var truck=string.IsNullOrWhiteSpace(manifest.Vehicle)?null:await Mapping("equipment",manifest.Vehicle,false,ct)??throw new InvalidOperationException("The manifest's truck has not synchronized yet.");
        var driverId=driver is null?(long?)null:long.Parse(driver.ExternalId,CultureInfo.InvariantCulture);
        var truckId=truck is null?(long?)null:long.Parse(truck.ExternalId,CultureInfo.InvariantCulture);
        var id=external is null?await NextId(db,tx,true,ct):long.Parse(external,CultureInfo.InvariantCulture);
        if(external is null)
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE,PB_EXTERNAL_REF,PB_NOTES,PB_PLANNED_TRACTOR,PB_PLANNED_DRIVER)
                VALUES(@id,'M','M',@origin,@origin,@destination,1,CURRENT_TIMESTAMP,@day,@reference,@notes,@truckId,@driverId)
                """,new{id,origin=manifest.Details.OriginTerminal,destination=manifest.Details.DestinationTerminal,day=manifest.ManifestDate.ToDateTime(TimeOnly.MinValue),reference=$"aurora-sync:{change.Id:N}",notes=manifest.Details.Notes,truckId,driverId},tx,cancellationToken:ct));
        var storedNotes=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@id",new{id},tx,cancellationToken:ct));
        var notes=ManifestNotes.Write(storedNotes,manifest.Details.Notes);
        if(ManifestNotes.OptimizationLocked(storedNotes)!=manifest.Details.OptimizationLocked)notes=ManifestNotes.SetPlanningLock(notes,manifest.Details.OptimizationLocked);
        if(manifest.Details.ReturnDate is {} until && ManifestNotes.TripUntil(storedNotes)!=until)notes=ManifestNotes.WithTripUntil(notes,until);
        var current=external is null?null:await DispatchDocument(db,tx,id,true,ct);
        if(current is null || current.CanEdit)
        {
            if(current is not null && (current.Origin!=manifest.Details.OriginTerminal || current.Destination!=manifest.Details.DestinationTerminal)
                && await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id",new{id},tx,cancellationToken:ct))>0)
                throw new FormatException("Change the terminals of an existing native route in Nova so its routing stops stay consistent.");
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_LOCATION=@origin,PBORIG=@origin,PBDEST=@destination,PB_MFST_DATE=@day,PB_PLANNED_TRACTOR=@truckId,PB_PLANNED_DRIVER=@driverId,PB_NOTES=@notes WHERE PB_ID=@id",new{id,origin=manifest.Details.OriginTerminal,destination=manifest.Details.DestinationTerminal,day=manifest.ManifestDate.ToDateTime(TimeOnly.MinValue),truckId,driverId,notes},tx,cancellationToken:ct));
            await ReplaceSyncMembership(db,tx,id,stops.ToArray(),manifest.Details.OriginTerminal,manifest.Details.DestinationTerminal,ct);
        }
        else
        {
            if(current.Origin!=manifest.Details.OriginTerminal || current.Destination!=manifest.Details.DestinationTerminal || current.DriverId!=driverId || current.TruckId!=truckId || DateOnly.FromDateTime(current.Date??DateTime.MinValue)!=manifest.ManifestDate)
                throw new SyncConflictException("The assignment and date of an active manifest cannot be changed.");
            if(current.Notes!=manifest.Details.Notes)
            {
                if(!current.CanEditNotes)throw new FormatException("Notes on this native manifest are read-only.");
                await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_NOTES=@notes WHERE PB_ID=@id",new{id,notes},tx,cancellationToken:ct));
            }
            var existing=(await DispatchMembers(db,tx,id,false,ct)).Select(m=>m.Stop).ToArray();
            if(!existing.Order().SequenceEqual(stops.Order()))throw new SyncConflictException("An active native manifest cannot change membership.");
        }
        if(manifest.Status=="Dispatched" && current?.StatusCode!="DPCH")
        {
            if(driverId is null || truckId is null)throw new FormatException("Dispatch requires a synchronized driver and truck.");
            var dispatch=await DispatchDocument(db,tx,id,true,ct);
            await WriteDispatch(db,tx,id,new(dispatch.Revision,driverId.Value,truckId.Value),true,"Aurora",ct);
        }
        else if(manifest.Status is "En Route" or "Complete" && current?.StatusCode!=(manifest.Status=="En Route"?"ENR":"CPLT"))
        {
            if(driverId is null || truckId is null)throw new FormatException("Execution requires a synchronized driver and truck.");
            var dispatch=await DispatchDocument(db,tx,id,true,ct);
            var occurred=manifest.Details.StatusOccurredAt??throw new FormatException("Enter the actual status event time.");
            await WriteManifestStatus(db,tx,id,new(dispatch.Revision,manifest.Status=="En Route"?"ENR":"CPLT",driverId.Value,truckId.Value,occurred),"Aurora",ct);
        }
        else if(current is {CanEdit:false} && manifest.Status=="Planning")throw new SyncConflictException("An active native manifest cannot return to planning.");
        return id.ToString(CultureInfo.InvariantCulture);
    }
    private sealed record SyncMemberRoute(long Stop,long Route);
    private async Task ReplaceSyncMembership(FbConnection db,FbTransaction tx,long id,long[] members,string origin,string destination,CancellationToken ct)
    {
        var previous=(await db.QueryAsync<long>(new CommandDefinition("SELECT m.OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS m JOIN OPS_STOP_REC route ON route.OPS_STOP_ID=m.OSM_STOP_ID WHERE route.OPS_ORDER_ID=@id",new{id},tx,cancellationToken:ct))).ToArray();
        foreach(var stop in previous.Concat(members).Distinct().Order())
        {
            var owner=await db.QuerySingleAsync<long?>(new CommandDefinition("SELECT OPS_MANIFEST_NUMBER FROM OPS_STOP_REC WHERE OPS_STOP_ID=@stop WITH LOCK",new{stop},tx,cancellationToken:ct));
            if(members.Contains(stop) && owner is >0 && owner!=id)throw new SyncConflictException("A manifest order was assigned elsewhere in Nova.");
            if(members.Contains(stop) && await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS m JOIN OPS_STOP_REC route ON route.OPS_STOP_ID=m.OSM_STOP_ID WHERE m.OSM_PKUP_DELV_ID=@stop AND route.OPS_ORDER_ID<>@id",new{stop,id},tx,cancellationToken:ct))>0)throw new SyncConflictException("A manifest order was assigned elsewhere in Nova.");
        }
        var priorRoutes=(await db.QueryAsync<SyncMemberRoute>(new CommandDefinition("SELECT OSM_PKUP_DELV_ID AS Stop,OSM_STOP_ID AS Route FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID IN (SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id)",new{id},tx,cancellationToken:ct))).ToDictionary(r=>r.Stop,r=>r.Route);
        var routeStops=(await db.QueryAsync<long>(new CommandDefinition("SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id ORDER BY OPS_STOP_NUMBER",new{id},tx,cancellationToken:ct))).ToArray();
        if(previous.SequenceEqual(members) && routeStops.Length>0)return;
        await db.ExecuteAsync(new CommandDefinition("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID IN (SELECT OPS_STOP_ID FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id)",new{id},tx,cancellationToken:ct));
        foreach(var removed in previous.Except(members))
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=NULL,OPS_MANIFESTED_TIME=NULL WHERE OPS_STOP_ID=@removed AND OPS_MANIFEST_NUMBER=@id",new{removed,id},tx,cancellationToken:ct));
        if(string.IsNullOrEmpty(origin))return;
        // Keep existing routing stops and their timing/address metadata. Retained
        // orders stay on their existing stop; new members join the receiving stop.
        long receiving=routeStops.LastOrDefault();var number=0;
        if(routeStops.Length==0)
            foreach(var terminal in origin==destination?new[]{origin}:new[]{origin,destination})
            {
                receiving=await NextId(db,tx,false,ct);
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION) VALUES(@receiving,@id,@sequence,'L',@terminal)",new{receiving,id,sequence=++number,terminal},tx,cancellationToken:ct));
            }
        number=0;
        foreach(var stop in members)
        {
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_SEQUENCE,OSM_PKUP_DELV_ID) VALUES(@receiving,@sequence,@stop)",new{receiving=priorRoutes.GetValueOrDefault(stop,receiving),sequence=++number,stop},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_STOP_REC SET OPS_MANIFEST_NUMBER=@id,OPS_MANIFESTED_TIME=CURRENT_TIMESTAMP WHERE OPS_STOP_ID=@stop",new{id,stop},tx,cancellationToken:ct));
        }
    }
}
