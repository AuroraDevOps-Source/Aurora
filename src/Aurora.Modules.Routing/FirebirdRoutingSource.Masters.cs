using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Globalization;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    public async Task<TerminalDto[]> ReadTerminals(CancellationToken ct)
    {
        await using var db=await Open(ct);
        var rows=(await db.QueryAsync<TerminalDto>(new CommandDefinition("SELECT TRIM(LM_ID) AS Code, TRIM(LM_NAME) AS Name, TRIM(LM_ADDR1) AS Address, TRIM(LM_CITY) AS City, TRIM(LM_STATE) AS State, TRIM(LM_ZIP) AS PostalCode, TRIM(LM_COUNTRY) AS Country, TRIM(LM_REGION) AS Region, LM_LATITUDE AS Latitude, LM_LONGITUDE AS Longitude FROM LOCMAST ORDER BY LM_ID",cancellationToken:ct))).ToArray();
        foreach(var row in rows)
        {
            row.Id=StableId($"firebird-node:{tenant.TenantId}:{Options.SourceKey}:{row.Code}");
            if(!HasCoordinates(row.Latitude,row.Longitude)){row.Latitude=null;row.Longitude=null;}
        }
        return rows;
    }
    public async Task<EquipmentCatalogDto> ReadFleet(CancellationToken ct)
    {
        await using var db=await Open(ct);
        var rows=await db.QueryAsync<NovaFleetRow>(new CommandDefinition("""
            SELECT h.EH_ID AS NativeId, TRIM(h.EH_UNIT_ID) AS Unit, TRIM(h.EH_DOMICILE) AS Terminal,
                TRIM(t.CTT_CODE) AS Kind, TRIM(t.CTT_DESC) AS Description,
                h.EH_MAX_WEIGHT AS Weight, h.EH_MAX_CUBE AS Cubes,
                TRIM(h.EH_STD_START_TM) AS StartTime, TRIM(h.EH_STD_END_TM) AS EndTime
            FROM EQUIP_HEADER h JOIN CT_TYPE t ON t.CTT_ID=h.EH_UNIT_TYPE
            WHERE h.EH_DELETE_DATE IS NULL AND t.CTT_CODE<>'DRV' ORDER BY h.EH_UNIT_ID
            """,cancellationToken:ct));
        return MapFleet(rows,LocalTime(DateTime.Today).ToString("zzz"));
    }
    public sealed class NovaFleetRow
    {
        public long NativeId {get;set;}
        public string Unit {get;set;}="";
        public string Terminal {get;set;}="";
        public string Kind {get;set;}="";
        public string Description {get;set;}="";
        public double? Weight {get;set;}
        public double? Cubes {get;set;}
        public string? StartTime {get;set;}
        public string? EndTime {get;set;}
    }
    public static EquipmentCatalogDto MapFleet(IEnumerable<NovaFleetRow> records,string offset)
    {
        var types=new List<EquipmentTypeDto>();var units=new List<EquipmentUnitDto>();
        foreach(var r in records)
        {
            var code=$"NOVA-{r.NativeId}";
            // Capacity belongs to the individual Nova unit, not the equipment-type dictionary.
            types.Add(new(){Code=code,Description=r.Description,Weight=r.Weight,Cubes=r.Cubes,PowerOnly=r.Description.Contains("tractor",StringComparison.OrdinalIgnoreCase)});
            string Clock(string? v,string fallback)=>TimeOnly.TryParseExact(v,"HHmm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var t)?t.ToString("HH:mm"):fallback;
            units.Add(new(){Id=r.Unit,TypeCode=code,Terminal=r.Terminal??"",Available=true,
                UseForRouting=!string.IsNullOrWhiteSpace(r.Terminal) && r.Weight>0 && r.Cubes>0,
                ShiftStart=Clock(r.StartTime,"06:00"),ShiftEnd=Clock(r.EndTime,"18:00"),UtcOffset=offset});
        }
        if(units.GroupBy(u=>u.Id,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new FormatException("Nova has duplicate equipment unit numbers. Correct them before planning.");
        return new(types,units);
    }
    private static readonly Field[] TerminalFields=[
        new("LM_ID","Terminal",Length:12,ReadOnly:true),new("LM_NAME","Name",Length:40),new("LM_ADDR1","Address",Length:50),new("LM_ADDR2","Address 2",Length:50),
        new("LM_CITY","City",Length:35),new("LM_STATE","State",Length:2),new("LM_ZIP","ZIP",Length:9),new("LM_COUNTRY","Country",Length:4),
        new("LM_REGION","Region",Length:4),new("LM_MANAGER","Manager",Length:40),new("LM_PHONE","Phone",Length:32),new("LM_LATITUDE","Latitude","latitude"),new("LM_LONGITUDE","Longitude","longitude")];
    private static readonly Field[] CustomerFields=[
        new("AC_CUST","Account",Length:12,ReadOnly:true),new("AC_NAME","Name",Length:40),new("AC_ADDR1","Address",Length:50),new("AC_ADDR2","Address 2",Length:50),new("AC_ADDR3","Address 3",Length:50),
        new("AC_CITY","City",Length:35),new("AC_STATE","State",Length:2),new("AC_ZIP","ZIP",Length:9),new("AC_COUNTRY_CD","Country",Length:4),
        new("AC_PHONE","Phone",Length:32),new("AC_TERM_LOC","Terminal",Length:12),new("AC_TYPE","Type",Length:2,ReadOnly:true),new("AC_STATUS","Status",Length:2,ReadOnly:true),new("AC_LATITUDE","Latitude","latitude"),new("AC_LONGITUDE","Longitude","longitude")];
    private static (string Table,string Key,Field[] Fields) Master(string kind)=>kind switch {
        "terminals"=>("LOCMAST","LM_ID",TerminalFields),"customers"=>("ACCOUNT","AC_CUST",CustomerFields),_=>throw new FormatException("Unknown Nova master record type.")};
    public async Task<NovaMasterPage> ReadMasters(string kind,string? search,int page,CancellationToken ct)
    {
        var (table,key,fields)=Master(kind);
        if(page<0 || page>100000 || search?.Length>100)throw new FormatException("Invalid search or page.");
        var name=fields[1].Column;var city=kind=="terminals"?"LM_CITY":"AC_CITY";
        await using var db=await Open(ct);
        var prefix=kind=="terminals"?"LM_":"AC_";
        var rows=(await db.QueryAsync<MasterListRow>(new CommandDefinition($"SELECT TRIM({key}) AS Id, TRIM({name}) AS Name, TRIM({city}) AS City, {prefix}LATITUDE AS Latitude, {prefix}LONGITUDE AS Longitude FROM {table} WHERE {key} CONTAINING @search OR {name} CONTAINING @search OR {city} CONTAINING @search ORDER BY {key} ROWS @first TO @last",new{search=search?.Trim()??"",first=page*50+1,last=page*50+51},cancellationToken:ct))).ToArray();
        return new(rows.Take(50).Select(r=>new NovaMasterSummary(r.Id,r.Name,r.City)
        {
            Issues=kind=="terminals"?DataRequirements.Depot(r.Id,r.Latitude,r.Longitude):[]
        }).ToArray(),rows.Length>50);
    }
    private sealed class MasterListRow
    {
        public string Id {get;set;}=""; public string Name {get;set;}=""; public string City {get;set;}="";
        public double? Latitude {get;set;} public double? Longitude {get;set;}
    }
    public async Task<NovaMasterDocument> ReadMaster(string kind,string id,CancellationToken ct)
    {
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        return await ReadMaster(db,tx,kind,id,false,ct);
    }
    private static async Task<NovaMasterDocument> ReadMaster(FbConnection db,FbTransaction tx,string kind,string id,bool locking,CancellationToken ct)
    {
        var(table,key,fields)=Master(kind);
        var row=(IDictionary<string,object>?)await db.QuerySingleOrDefaultAsync(new CommandDefinition($"SELECT {string.Join(',',fields.Select(f=>f.Column))} FROM {table} WHERE {key}=@id"+(locking?" WITH LOCK":""),new{id},tx,cancellationToken:ct))??throw new KeyNotFoundException("Nova record not found.");
        var part=new NovaRecordPart("header",kind=="terminals"?"Terminal":"Customer account",fields.Select(f=>new NovaField(f.Column,f.Label,f.Kind,f.Length,f.ReadOnly,Value(row[f.Column]))).ToArray());
        return new(id,Revision([part]),part.Fields);
    }
    public async Task<NovaMasterDocument> SaveMaster(string kind,string id,NovaMasterEdit input,CancellationToken ct,bool rollbackOnly=false)
    {
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,WaitTimeout=TimeSpan.FromSeconds(10)});
        var current=await ReadMaster(db,tx,kind,id,true,ct);
        if(current.Revision!=input.Revision)throw new FormatException("This record changed in Nova. Reload it before saving.");
        if(input.Values is null || input.Values.Count!=current.Fields.Length || current.Fields.Any(f=>!input.Values.ContainsKey(f.Key)))throw new FormatException("Incomplete record. Reload before saving.");
        var edits=new Dictionary<string,object?>();
        foreach(var f in current.Fields)
        {
            if(f.Value==input.Values[f.Key])continue;
            if(f.ReadOnly)throw new FormatException($"{f.Label} cannot be changed here.");
            edits[f.Key]=ParseField(f,input.Values[f.Key]);
        }
        var prefix=kind=="terminals"?"LM_":"AC_";
        var latKey=prefix+"LATITUDE";var lonKey=prefix+"LONGITUDE";
        if(edits.Keys.Any(k=>k.Contains("ADDR") || k==prefix+"CITY" || k==prefix+"STATE" || k==prefix+"ZIP" || k.StartsWith(prefix+"COUNTRY")) && !edits.ContainsKey(latKey) && !edits.ContainsKey(lonKey)){edits[latKey]=null;edits[lonKey]=null;}
        object? Coordinate(string key)=>edits.TryGetValue(key,out var v)?v:ParseField(current.Fields.Single(f=>f.Key==key),input.Values[key]);
        if((Coordinate(latKey) is null)!=(Coordinate(lonKey) is null))throw new FormatException("Provide both latitude and longitude, or leave both blank.");
        if(edits.TryGetValue("AC_TERM_LOC",out var terminal) && terminal is string code && !await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM LOCMAST WHERE LM_ID=@code) FROM RDB$DATABASE",new{code},tx,cancellationToken:ct)))throw new FormatException("Choose an existing Nova terminal.");
        if(edits.Count>0)
        {
            var(table,key,_)=Master(kind);var args=new DynamicParameters();args.Add("id",id);
            var sets=edits.Select((e,i)=>{args.Add("v"+i,e.Value);return e.Key+"=@v"+i;});
            if(await db.ExecuteAsync(new CommandDefinition($"UPDATE {table} SET {string.Join(',',sets)} WHERE {key}=@id",args,tx,cancellationToken:ct))!=1)throw new FormatException("Record changed while saving.");
        }
        var saved=await ReadMaster(db,tx,kind,id,false,ct);
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
        return saved;
    }
}
