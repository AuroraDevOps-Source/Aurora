using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;
using Aurora.Integrations.Nova.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;
public sealed partial class FirebirdRoutingSource
{
    public async Task<ExternalReceipt> Push(SyncChange change,FbConnection db,FbTransaction tx,CancellationToken ct)
    {
        RequireEnabled();
        if(change.ConnectionId!=_syncConnection)throw new InvalidOperationException("Wrong synchronization connection.");
        var data=JsonNode.Parse(change.Payload)!.AsObject();
        var mapping=await Mapping(change.EntityType,change.LocalId,false,ct);
        var external=mapping?.ExternalId;
        var deleted=change.Operation=="delete";
        if(mapping is not null)
        {
            var(table,key,numeric)=change.EntityType switch{
                "terminal"=>("LOCMAST","LM_ID",false),"customer"=>("ACCOUNT","AC_CUST",false),
                "equipment" or "driver"=>("EQUIP_HEADER","EH_ID",true),"order" or "manifest"=>("OPS_HEADER","PB_ID",true),
                _=>throw new FormatException("Unknown synchronization entity.")};
            await db.QueryAsync(new CommandDefinition($"SELECT {key} FROM {table} WHERE {key}=@id WITH LOCK",new{id=numeric?(object)long.Parse(external!,CultureInfo.InvariantCulture):external},tx,cancellationToken:ct));
            var snapshots=await Read(new(0,change.EntityType,external!,"U"),db,tx,ct);
            var current=snapshots.SingleOrDefault(s=>s.EntityType==change.EntityType&&s.ExternalId==external);
            if(current is null || current.Deleted)
            {
                if(deleted)return new(external!,SyncHash(data,true));
                throw new SyncConflictException("The record was removed in Nova. Review before restoring it.");
            }
            if(!SyncJson.Equal(current.Data,JsonNode.Parse(mapping.Baseline)))
                throw new SyncConflictException("Nova changed after the last synchronized version. Import and review before exporting.");
        }
        if(deleted)
        {
            if(external is null) return new("local-"+change.Id.ToString("N"),SyncHash(data,true));
            await DeleteSyncRecord(change.EntityType,external,db,tx,ct);
            return new(external,SyncHash(data,true));
        }
        switch(change.EntityType)
        {
            case "terminal":
            case "customer":
                var terminal=change.EntityType=="terminal";
                external??=S(data,"Code");
                if(terminal)data.Deserialize<TerminalDto>()!.Validate();else data.Deserialize<CustomerDto>()!.Validate();
                if(external!=S(data,"Code"))throw new FormatException("Master codes cannot change during synchronization.");
                var masterFields=terminal?SyncTerminalFields:SyncCustomerFields;
                var nativeFields=terminal?TerminalFields:CustomerFields;
                foreach(var field in masterFields)
                    ParseField(NewField(nativeFields.Single(f=>f.Column==field.Column)),NativeValue(data[field.Property])?.ToString());
                if(mapping is null && await db.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {(terminal?"LOCMAST":"ACCOUNT")} WHERE {(terminal?"LM_ID":"AC_CUST")}=@id",new{id=external},tx,cancellationToken:ct))>0)
                    throw new SyncConflictException("This code already exists in Nova. Import it before editing.");
                await WriteProjection(db,tx,terminal?"LOCMAST":"ACCOUNT",terminal?"LM_ID":"AC_CUST",external,masterFields,data,mapping is not null,ct);
                break;
            case "driver":external=await WriteSyncDriver(db,tx,external,data,ct);break;
            case "equipment":external=await WriteSyncEquipment(db,tx,external,data,ct);break;
            case "order":external=await WriteSyncOrder(db,tx,external,data,change,ct);break;
            case "manifest":external=await WriteSyncManifest(db,tx,external,data,change,ct);break;
            default:throw new FormatException("Unsupported synchronization entity.");
        }
        return new(external!,SyncHash(data));
    }
    private async Task DeleteSyncRecord(string kind,string id,FbConnection db,FbTransaction tx,CancellationToken ct)
    {
        if(kind is "terminal" or "customer")
        {
            // Physical master deletion has application-specific dependency rules; use
            // Nova's existing reference checks rather than cascading any relationships.
            var table=kind=="terminal"?"LOCMAST":"ACCOUNT";var key=kind=="terminal"?"LM_ID":"AC_CUST";
            if(kind=="terminal" && await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_LOCATION=@id AND PB_DT_CANC IS NULL",new{id},tx,cancellationToken:ct))>0)
                throw new SyncConflictException("The terminal still has native orders or manifests.");
            if(kind=="customer" && await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ACCTID=@id",new{id},tx,cancellationToken:ct))>0)
                throw new SyncConflictException("The customer still has native order references.");
            await db.ExecuteAsync(new CommandDefinition($"DELETE FROM {table} WHERE {key}=@id",new{id},tx,cancellationToken:ct));return;
        }
        var native=long.Parse(id,CultureInfo.InvariantCulture);
        if(kind is "driver" or "equipment")
        {
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM OPS_HEADER h LEFT JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS WHERE h.PB_TYPE='M'
                AND h.PB_DT_CANC IS NULL AND COALESCE(s.CTS_CODE,'') NOT IN ('CPLT','VOID','DONE')
                AND (@native IN (h.PB_CURRENT_DRIVER,h.PB_PLANNED_DRIVER,h.PB_CURRENT_TRACTOR,h.PB_PLANNED_TRACTOR,h.PB_PLANNED_TRAILER))
                """,new{native},tx,cancellationToken:ct))>0)throw new SyncConflictException("The resource is assigned in Nova.");
            await db.ExecuteAsync(new CommandDefinition("UPDATE EQUIP_HEADER SET EH_DELETE_DATE=CURRENT_TIMESTAMP WHERE EH_ID=@native",new{native},tx,cancellationToken:ct));return;
        }
        if(kind=="manifest")
        {
            var current=await DispatchDocument(db,tx,native,true,ct);
            if(!current.CanEdit)throw new SyncConflictException("An active or completed Nova manifest cannot be deleted.");
            await ReplaceSyncMembership(db,tx,native,[],"","",ct);
        }
        else if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@native AND (COALESCE(OPS_MANIFEST_NUMBER,0)>0 OR OPS_DELIVERED_TIME IS NOT NULL)",new{native},tx,cancellationToken:ct))>0)
            throw new SyncConflictException("The order is manifested or delivered in Nova.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE OPS_HEADER SET PB_DT_CANC=CURRENT_TIMESTAMP WHERE PB_ID=@native",new{native},tx,cancellationToken:ct));
    }
    private async Task<long> NewSyncEquipment(FbConnection db,FbTransaction tx,string number,string kind,CancellationToken ct)
    {
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EQUIP_HEADER WHERE EH_UNIT_ID=@number",new{number},tx,cancellationToken:ct))>0)throw new SyncConflictException("This unit number already exists in Nova.");
        var type=await db.QuerySingleAsync<long>(new CommandDefinition("SELECT CTT_ID FROM CT_TYPE WHERE CTT_CODE=@kind",new{kind},tx,cancellationToken:ct));
        long id;
        do{id=await db.ExecuteScalarAsync<long>(new CommandDefinition("SELECT GEN_ID(EQ_GEN,1) FROM RDB$DATABASE",transaction:tx,cancellationToken:ct));}
        while(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EQUIP_HEADER WHERE EH_ID=@id",new{id},tx,cancellationToken:ct))>0);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE) VALUES(@id,@number,@type)",new{id,number,type},tx,cancellationToken:ct));return id;
    }
    private async Task<string> WriteSyncDriver(FbConnection db,FbTransaction tx,string? external,JsonObject data,CancellationToken ct)
    {
        var input=data.Deserialize<NovaDriver>()!;input.Validate();await ValidTerminal(db,tx,input.Terminal,ct);
        var id=external is null?await NewSyncEquipment(db,tx,input.Number,"DRV",ct):long.Parse(external,CultureInfo.InvariantCulture);
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM DRIVER_HEADER WHERE DH_ID=@id",new{id},tx,cancellationToken:ct))==0)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO DRIVER_HEADER(DH_ID) VALUES(@id)",new{id},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("UPDATE EQUIP_HEADER SET EH_DOMICILE=@Terminal WHERE EH_ID=@id",new{id,input.Terminal},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE DRIVER_HEADER SET DH_FIRST_NAME=@FirstName,DH_LAST_NAME=@LastName,DH_CELL_PHONE=@Phone,DH_EMAIL=@Email,
            DH_HIRE_DATE=@HireDate,DH_TERM_DATE=@TerminationDate,DH_DL_EXPIRY=@LicenseExpiry,DH_FULL_BURDENED_HR=@PerHour WHERE DH_ID=@id
            """,new{id,input.FirstName,input.LastName,input.Phone,input.Email,input.HireDate,input.TerminationDate,input.LicenseExpiry,input.PerHour},tx,cancellationToken:ct));
        return id.ToString(CultureInfo.InvariantCulture);
    }
    private async Task<string> WriteSyncEquipment(FbConnection db,FbTransaction tx,string? external,JsonObject data,CancellationToken ct)
    {
        var unit=data["Unit"]!.Deserialize<EquipmentUnitDto>()!;var type=data["Type"]!.Deserialize<EquipmentTypeDto>()!;
        unit.TypeCode="sync";type.Code="sync";unit.Validate();type.Validate();await ValidTerminal(db,tx,unit.Terminal,ct);
        var id=external is null?await NewSyncEquipment(db,tx,unit.Id,"TRK",ct):long.Parse(external,CultureInfo.InvariantCulture);
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE EQUIP_HEADER SET EH_DOMICILE=@Terminal,EH_MAX_WEIGHT=@Weight,EH_MAX_CUBE=@Cubes,
            EH_TOT_FIXED_COST_HR=@PerHour,EH_TOT_VAR_COST_MI=@perMile,EH_VIN=@Vin,EH_TAG_NUMBER=@Plate,EH_TAG_STATE=@PlateState WHERE EH_ID=@id
            """,new{id,unit.Terminal,type.Weight,type.Cubes,type.PerHour,perMile=type.PerKilometer*1.609344,unit.Vin,unit.Plate,unit.PlateState},tx,cancellationToken:ct));
        return id.ToString(CultureInfo.InvariantCulture);
    }
}
