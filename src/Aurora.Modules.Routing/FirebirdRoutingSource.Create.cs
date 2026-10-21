using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    public async Task<long> CreateManifest(NovaManifestCreate input,CancellationToken ct,bool rollbackOnly=false)
    {
        if(string.IsNullOrWhiteSpace(input.Origin) || string.IsNullOrWhiteSpace(input.Destination) || input.Date==default)throw new FormatException("Choose origin, destination, and manifest date.");
        await using var pg = await factory.OpenConnectionAsync(ct);
        await using var pgTx = await pg.BeginTransactionAsync(ct);
        await Lock(pg, pgTx, ct);
        var destinationNode=(await ReadTerminals(ct)).SingleOrDefault(n=>n.Code.Equals(input.Destination,StringComparison.OrdinalIgnoreCase));
        var notes=input.Origin!=input.Destination && destinationNode?.IsConsolidation==true
            ? ExportFingerprint(Guid.NewGuid(),[],input.Destination,PlanningBatch.Consolidation) : "";
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        await ValidTerminal(db,tx,input.Origin,ct);await ValidTerminal(db,tx,input.Destination,ct);
        var id=await NextId(db,tx,true,ct);
        var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE,PB_EXTERNAL_REF,PB_NOTES)
            VALUES(@id,'M','M',@Origin,@Origin,@Destination,1,@now,@date,@reference,@notes)
            """,new{id,input.Origin,input.Destination,now,date=input.Date.ToDateTime(TimeOnly.MinValue),reference=$"aurora-manual:{tenant.TenantId:N}:{Guid.NewGuid():N}",notes},tx,cancellationToken:ct));
        var terminals=input.Origin==input.Destination?new[]{input.Origin}:new[]{input.Origin,input.Destination};
        for(var i=0;i<terminals.Length;i++)
        {
            var stop=await NextId(db,tx,false,ct);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION) VALUES(@stop,@id,@sequence,'L',@terminal)",new{stop,id,sequence=i+1,terminal=terminals[i]},tx,cancellationToken:ct));
        }
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
        return id;
    }
    private static NovaField NewField(Field f) => new(f.Column,f.Label,f.Kind,f.Length,false,null);
    public NovaMasterDocument NewMaster(string kind)
    {
        RequireEnabled();
        var (_,_,fields)=Master(kind);
        return new("","",fields.Select(NewField).ToArray());
    }
    public NovaRecordDocument NewRecord(string kind)
    {
        RequireEnabled();RecordKind(kind);
        var fields=(kind=="fleet"?FleetFields:OrderFields).Select(NewField).ToArray();
        if(kind=="orders")fields=fields.Select(f=>f.Key=="PB_STATUS"?f with{Value="110"}:f).ToArray();
        return new(0,kind=="fleet"?"Add truck":"Add order","",kind=="fleet"
            ? [new("header","Vehicle",fields)]
            : [new("header","Order & bill-to",fields),new("pickup","Shipper / pickup",StopFields.Select(NewField).ToArray()),new("delivery","Consignee / delivery",StopFields.Select(NewField).ToArray())]);
    }
    private static Dictionary<string,object?> CreationValues(NovaField[] fields,Dictionary<string,string?>? input)
    {
        if(input is null || input.Count!=fields.Length || fields.Any(f=>!input.ContainsKey(f.Key)))throw new FormatException("Incomplete fields. Reload the form before saving.");
        return fields.ToDictionary(f=>f.Key,f=>ParseField(f,input[f.Key]));
    }
    private static void Required(Dictionary<string,object?> values,params string[] keys)
    {
        foreach(var key in keys)if(values.GetValueOrDefault(key) is null or "")throw new FormatException($"Enter {key switch{"EH_UNIT_ID"=>"a unit number","EH_UNIT_TYPE"=>"an equipment type","PB_LOCATION"=>"a terminal","PB_STATUS"=>"an order status","LM_ID"=>"a terminal code","AC_CUST"=>"a customer code",_=>"a name"}}.");
    }
    private static void Coordinates(Dictionary<string,object?> values,string prefix)
    {
        if((values.GetValueOrDefault(prefix+"LATITUDE") is null)!=(values.GetValueOrDefault(prefix+"LONGITUDE") is null))throw new FormatException("Provide both latitude and longitude, or leave both blank.");
    }
    private static async Task ValidTerminal(FbConnection db,FbTransaction tx,object? code,CancellationToken ct)
    {
        if(code is null)return;
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LOCMAST WHERE LM_ID=@code",new{code},tx,cancellationToken:ct))!=1)throw new FormatException("Choose an existing Nova terminal.");
    }
    private static Task<int> Insert(FbConnection db,FbTransaction tx,string table,Dictionary<string,object?> values,CancellationToken ct)
    {
        var args=new DynamicParameters();var parameters=values.Select((v,i)=>{args.Add("v"+i,v.Value);return "@v"+i;}).ToArray();
        return db.ExecuteAsync(new CommandDefinition($"INSERT INTO {table} ({string.Join(',',values.Keys)}) VALUES ({string.Join(',',parameters)})",args,tx,cancellationToken:ct));
    }
    private static FbTransaction BeginCreate(FbConnection db)=>db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,WaitTimeout=TimeSpan.FromSeconds(10)});
    public async Task<NovaMasterDocument> CreateMaster(string kind,NovaMasterEdit input,CancellationToken ct,bool rollbackOnly=false)
    {
        var template=NewMaster(kind);var (table,key,_)=Master(kind);
        var values=CreationValues(template.Fields,input.Values);Required(values,key,kind=="terminals"?"LM_NAME":"AC_NAME");Coordinates(values,kind=="terminals"?"LM_":"AC_");
        string? locationWarning=null;
        if(addressLookup is not null)
        {
            (input,locationWarning)=await addressLookup.Resolve(kind,input,null,ct);
            values=CreationValues(template.Fields,input.Values);
        }
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        var id=(string)values[key]!;
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {table} WHERE {key}=@id",new{id},tx,cancellationToken:ct))>0)throw new FormatException("That code already exists. Choose a different code.");
        if(kind=="customers")await ValidTerminal(db,tx,values.GetValueOrDefault("AC_TERM_LOC"),ct);
        await Insert(db,tx,table,values,ct);
        var saved=await ReadMaster(db,tx,kind,id,false,ct);
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
        return saved with { LocationWarning=locationWarning };
    }
    private static void ValidateRecordFields(NovaRecordDocument template,NovaRecordEdit input,bool editing)
    {
        if(input.Values is null || input.Values.Count!=template.Parts.Length || template.Parts.Any(p=>!input.Values.ContainsKey(p.Key)))throw new FormatException("Incomplete record. Reload before saving.");
        foreach(var part in template.Parts)
        {
            CreationValues(part.Fields,input.Values[part.Key]);
            if(editing)foreach(var field in part.Fields.Where(f=>f.ReadOnly))
                if(input.Values[part.Key][field.Key]!=field.Value)throw new FormatException($"{field.Label} cannot be changed here.");
        }
    }
    public async Task<NovaRecordDocument> CreateRecord(string kind,NovaRecordEdit input,CancellationToken ct,bool rollbackOnly=false)
    {
        var template=NewRecord(kind);
        ValidateRecordFields(template,input,false);
        string[] locationWarnings=[];
        if(kind=="orders" && addressLookup is not null)
            (input,locationWarnings)=await addressLookup.ResolveOrder(input,null,ct);
        var parts=template.Parts.ToDictionary(p=>p.Key,p=>CreationValues(p.Fields,input.Values[p.Key]));var header=parts["header"];
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        long id;
        if(kind=="fleet")
        {
            Required(header,"EH_UNIT_ID","EH_UNIT_TYPE");
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EQUIP_HEADER WHERE EH_UNIT_ID=@unit",new{unit=header["EH_UNIT_ID"]},tx,cancellationToken:ct))>0)throw new FormatException("That unit number already exists.");
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM CT_TYPE WHERE CTT_ID=@type AND CTT_CODE<>'DRV'",new{type=header["EH_UNIT_TYPE"]},tx,cancellationToken:ct))!=1)throw new FormatException("Choose a valid equipment type.");
            await ValidTerminal(db,tx,header["EH_DOMICILE"],ct);
            do{id=await db.ExecuteScalarAsync<long>(new CommandDefinition("SELECT GEN_ID(EQ_GEN,1) FROM RDB$DATABASE",transaction:tx,cancellationToken:ct));}
            while(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EQUIP_HEADER WHERE EH_ID=@id",new{id},tx,cancellationToken:ct))>0);
            header["EH_ID"]=id;await Insert(db,tx,"EQUIP_HEADER",header,ct);
        }
        else
        {
            foreach(var values in parts.Values)
                foreach(var key in new[]{"PB_BILL_ID","OPS_ACCTID"})
                    if(values.GetValueOrDefault(key) is string accountId && !string.IsNullOrWhiteSpace(accountId)
                        && !await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM ACCOUNT WHERE AC_CUST=@accountId) FROM RDB$DATABASE",new{accountId},tx,cancellationToken:ct)))
                        throw new FormatException("Choose an account in this Nova database or enter the name manually.");
            Required(header,"PB_LOCATION","PB_STATUS");await ValidTerminal(db,tx,header["PB_LOCATION"],ct);
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM CT_STAT WHERE CTS_ID=@status AND (CTS_ORDER_VISIBLE='Y' OR CTS_ID=110)",new{status=header["PB_STATUS"]},tx,cancellationToken:ct))!=1)throw new FormatException("Choose a valid order status.");
            if(header["PBNUM"] is not decimal pro || pro<=0)throw new FormatException("Enter a positive PRO number.");
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_TYPE='O' AND PBNUM=@pro",new{pro},tx,cancellationToken:ct))>0)throw new FormatException("That PRO number already exists.");
            foreach(var stop in parts.Where(p=>p.Key!="header").Select(p=>p.Value))
            {
                Required(stop,"OPS_NAME");Coordinates(stop,"OPS_");
                var from=(DateTime?)stop["OPS_APPT_LOW"];var to=(DateTime?)stop["OPS_APPT_HIGH"];
                if((from is null)!=(to is null) || to<from)throw new FormatException("Provide both appointment times, with end at or after start.");
            }
            id=await NextId(db,tx,true,ct);header["PB_ID"]=id;header["PB_TYPE"]="O";header["PB_LOAD_TYPE"]="O";
            header["PB_DT_ENT"]=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).DateTime;
            await Insert(db,tx,"OPS_HEADER",header,ct);
            foreach(var key in new[]{"pickup","delivery"})
            {
                var stop=parts[key];stop["OPS_STOP_ID"]=await NextId(db,tx,false,ct);stop["OPS_ORDER_ID"]=id;stop["OPS_STOP_NUMBER"]=key=="pickup"?1:2;stop["OPS_TYPE"]=key=="pickup"?"P":"D";
                await Insert(db,tx,"OPS_STOP_REC",stop,ct);
            }
        }
        var saved=await ReadRecord(db,tx,kind,id,false,ct);
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
        return saved with { LocationWarnings=locationWarnings };
    }
}
