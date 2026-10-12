using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Modules.Routing;

public sealed partial class FirebirdRoutingSource
{
    private sealed record Field(string Column,string Label,string Kind="text",int Length=80,bool ReadOnly=false);
    private static readonly Field[] FleetFields = [
        new("EH_UNIT_ID","Unit number",Length:14,ReadOnly:true),new("EH_UNIT_TYPE","Equipment type ID","number",ReadOnly:true),
        new("EH_DOMICILE","Terminal code",Length:12),new("EH_MAKE","Make",Length:50),new("EH_MODEL","Model",Length:50),new("EH_YEAR","Year","integer"),
        new("EH_VIN","VIN",Length:50),new("EH_TAG_NUMBER","License plate",Length:15),new("EH_TAG_STATE","Plate state",Length:2),
        new("EH_EMPTY_WEIGHT","Empty weight (Nova units)","number"),new("EH_MAX_WEIGHT","Maximum weight (Nova units)","number"),new("EH_MAX_CUBE","Cubic capacity (cu ft)","integer"),
        new("EH_LENGTH","Exterior length (Nova units)","dimension"),new("EH_WIDTH","Exterior width (Nova units)","dimension"),new("EH_HEIGHT","Exterior height (Nova units)","dimension"),
        new("EH_INT_LENGTH","Interior length (Nova units)","dimension"),new("EH_INT_WIDTH","Interior width (Nova units)","dimension"),new("EH_INT_HEIGHT","Interior height (Nova units)","dimension"),
        new("EH_AXLES","Axles","integer"),new("EH_STD_START_TM","Default start (HHmm)","clock",4),new("EH_STD_END_TM","Default end (HHmm)","clock",4),
        new("EH_MAX_OPS_MINS_PER_DAY","Maximum operating minutes per day","integer"),new("EH_MAX_ROUTE_DURATION","Maximum route duration (Nova units)","integer"),
        new("EH_BREAK_DURATION","Break duration (Nova units)","integer"),new("EH_START_BREAK_AFTER_MINS","Break after (minutes)","integer")];
    private static readonly Field[] OrderFields = [
        new("PBNUM","PRO number","integer",ReadOnly:true),new("PB_STATUS","Status code","integer",ReadOnly:true),new("PB_LOCATION","Terminal",ReadOnly:true),
        new("PBBOL","BOL / reference"),new("PBPURORD","Purchase order"),new("PB_CUST_ORD","Customer order"),
        new("PBTPIECE","Total pieces","number"),new("PBTWEIGH","Total weight (Nova units)","number"),new("PB_TTL_CUBE","Total cubes","number"),
        new("PBBNAME","Bill-to name",Length:40),new("PBBADDR1","Bill-to address",Length:50),new("PBBADDR2","Bill-to address 2",Length:50),
        new("PBBCITY","Bill-to city",Length:35),new("PBBSTATE","Bill-to state",Length:2),new("PBBZIP","Bill-to ZIP",Length:9),new("PBBCOUNTRY","Bill-to country",Length:4)];
    private static readonly Field[] StopFields = [
        new("OPS_NAME","Name",Length:40),new("OPS_ADDR1","Address",Length:50),new("OPS_ADDR2","Address 2",Length:50),new("OPS_ADDR3","Address 3",Length:50),
        new("OPS_CITY","City",Length:35),new("OPS_STATE","State",Length:2),new("OPS_ZIP","ZIP",Length:9),new("OPS_COUNTRY","Country",Length:4),
        new("OPS_CONTACT","Contact",Length:40),new("OPS_PHONE","Phone",Length:32),new("OPS_CONTACT_EMAIL","Email",Length:150),
        new("OPS_BOL","BOL"),new("OPS_PO","Purchase order"),new("OPS_APPT_LOW","Appointment start","datetime-local"),new("OPS_APPT_HIGH","Appointment end","datetime-local"),
        new("OPS_LATITUDE","Latitude","latitude"),new("OPS_LONGITUDE","Longitude","longitude")];
    private static void RecordKind(string kind) { if(kind is not ("fleet" or "orders"))throw new FormatException("Unknown Nova record type."); }
    public async Task<NovaRecordOptions> RecordOptions(CancellationToken ct)
    {
        await using var db=await Open(ct);
        var terminals=(await db.QueryAsync<string>(new CommandDefinition("SELECT TRIM(LM_ID) FROM LOCMAST ORDER BY LM_ID",cancellationToken:ct))).ToArray();
        var statuses=(await db.QueryAsync<NovaManifestStatus>(new CommandDefinition("SELECT CAST(CTS_ID AS INTEGER) AS Id,TRIM(CTS_DESC) AS Name FROM CT_STAT WHERE CTS_ORDER_VISIBLE='Y' OR CTS_ID=110 ORDER BY CTS_DESC",cancellationToken:ct))).ToArray();
        return new(terminals,statuses);
    }
    public async Task<NovaRecordPage> RecordList(string kind,string? search,int page,CancellationToken ct,string? terminal=null,int? status=null)
    {
        RecordKind(kind);
        if(page<0 || page>100000 || search?.Length>100)throw new FormatException("Invalid search or page.");
        await using var db=await Open(ct);
        var filter=string.IsNullOrWhiteSpace(search)?"":kind=="fleet"
            ? " AND (h.EH_UNIT_ID CONTAINING @search OR h.EH_DOMICILE CONTAINING @search OR h.EH_MAKE CONTAINING @search)"
            : " AND (CAST(h.PB_ID AS VARCHAR(30)) CONTAINING @search OR CAST(h.PBNUM AS VARCHAR(30)) CONTAINING @search OR h.PBBOL CONTAINING @search OR EXISTS(SELECT 1 FROM OPS_STOP_REC s WHERE s.OPS_ORDER_ID=h.PB_ID AND (s.OPS_NAME CONTAINING @search OR s.OPS_CITY CONTAINING @search)))";
        var sql=kind=="fleet" ? """
            SELECT h.EH_ID AS Id, TRIM(h.EH_UNIT_ID) AS Number, TRIM(t.CTT_DESC) AS Description,
                COALESCE(TRIM(h.EH_DOMICILE),'') AS Terminal, 'Active' AS Status,
                h.EH_MAX_WEIGHT AS Weight, h.EH_MAX_CUBE AS Cubes,
                l.LM_ID AS KnownTerminal, l.LM_LATITUDE AS Latitude, l.LM_LONGITUDE AS Longitude
            FROM EQUIP_HEADER h JOIN CT_TYPE t ON t.CTT_ID=h.EH_UNIT_TYPE
            LEFT JOIN LOCMAST l ON l.LM_ID=h.EH_DOMICILE
            WHERE h.EH_DELETE_DATE IS NULL AND t.CTT_CODE<>'DRV'
            """ : """
            SELECT h.PB_ID AS Id, COALESCE(CAST(NULLIF(h.PBNUM,0) AS VARCHAR(30)), 'FB-' || CAST(h.PB_ID AS VARCHAR(30))) AS Number,
                COALESCE((SELECT FIRST 1 TRIM(s.OPS_NAME) FROM OPS_STOP_REC s WHERE s.OPS_ORDER_ID=h.PB_ID AND s.OPS_TYPE='D' ORDER BY s.OPS_STOP_NUMBER),'') AS Description,
                TRIM(h.PB_LOCATION) AS Terminal, COALESCE(TRIM(t.CTS_DESC),'Unknown') AS Status,
                l.LM_ID AS KnownTerminal,
                (SELECT COUNT(*) FROM OPS_STOP_REC s WHERE s.OPS_ORDER_ID=h.PB_ID AND s.OPS_TYPE='D') AS Deliveries,
                (SELECT COUNT(*) FROM OPS_STOP_REC s WHERE s.OPS_ORDER_ID=h.PB_ID AND s.OPS_TYPE='D'
                    AND (s.OPS_LATITUDE IS NULL OR s.OPS_LONGITUDE IS NULL OR s.OPS_LATITUDE NOT BETWEEN -90 AND 90
                    OR s.OPS_LONGITUDE NOT BETWEEN -180 AND 180 OR (s.OPS_LATITUDE=0 AND s.OPS_LONGITUDE=0))) AS MissingCoordinates
            FROM OPS_HEADER h LEFT JOIN CT_STAT t ON t.CTS_ID=h.PB_STATUS
            LEFT JOIN LOCMAST l ON l.LM_ID=h.PB_LOCATION WHERE h.PB_TYPE='O' AND h.PB_DT_CANC IS NULL
            """;
        if(!string.IsNullOrWhiteSpace(terminal))filter+=kind=="fleet"?" AND h.EH_DOMICILE=@terminal":" AND h.PB_LOCATION=@terminal";
        if(kind=="orders" && status is not null)filter+=" AND h.PB_STATUS=@status";
        var rows=(await db.QueryAsync<RecordRow>(new CommandDefinition(sql+filter+(kind=="fleet"?" ORDER BY h.EH_UNIT_ID":" ORDER BY h.PB_ID DESC")+" ROWS @first TO @last",new { search=search?.Trim(),terminal,status,first=page*50+1,last=page*50+51 },cancellationToken:ct))).ToArray();
        return new(rows.Take(50).Select(r=>new NovaRecordSummary(r.Id,r.Number,r.Description,r.Terminal,r.Status)
        {
            Issues=kind=="fleet"
                ? DataRequirements.Fleet(r.Terminal,r.KnownTerminal is not null,r.Latitude,r.Longitude,r.Weight,r.Cubes)
                : DataRequirements.Order(r.Deliveries,r.MissingCoordinates,r.Terminal,r.KnownTerminal is not null)
        }).ToArray(),rows.Length>50);
    }
    private sealed class RecordRow {
        public long Id {get;set;} public string Number {get;set;}=""; public string Description {get;set;}=""; public string Terminal {get;set;}=""; public string Status {get;set;}="";
        public string? KnownTerminal {get;set;} public double? Latitude {get;set;} public double? Longitude {get;set;}
        public double? Weight {get;set;} public double? Cubes {get;set;} public int Deliveries {get;set;} public int MissingCoordinates {get;set;}
    }
    private sealed class StopIdentity { public long Id {get;set;} public string Kind {get;set;}=""; }
    private static string? Value(object? value)=>value is null or DBNull?null:value is DateTime date?date.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture)?.TrimEnd();
    private static string Revision(NovaRecordPart[] parts)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    private static async Task<NovaRecordPart> ReadPart(FbConnection db,FbTransaction tx,string table,string key,long id,string part,string title,Field[] fields,CancellationToken ct)
    {
        var row=(IDictionary<string,object>)await db.QuerySingleAsync(new CommandDefinition($"SELECT {string.Join(',',fields.Select(f=>f.Column))} FROM {table} WHERE {key}=@id",new{id},tx,cancellationToken:ct));
        return new(part,title,fields.Select(f=>new NovaField(f.Column,f.Label,f.Kind,f.Length,f.ReadOnly,Value(row[f.Column]))).ToArray());
    }
    public async Task<NovaRecordDocument> ReadRecord(string kind,long id,CancellationToken ct)
    {
        RecordKind(kind);await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions { TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait });
        return await ReadRecord(db,tx,kind,id,false,ct);
    }
    private static async Task<NovaRecordDocument> ReadRecord(FbConnection db,FbTransaction tx,string kind,long id,bool locking,CancellationToken ct)
    {
        var sql=kind=="fleet"?"SELECT EH_ID FROM EQUIP_HEADER WHERE EH_ID=@id AND EH_DELETE_DATE IS NULL AND EH_UNIT_TYPE IN (SELECT CTT_ID FROM CT_TYPE WHERE CTT_CODE<>'DRV')":"SELECT PB_ID FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='O' AND PB_DT_CANC IS NULL";
        if(await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(sql+(locking?" WITH LOCK":""),new{id},tx,cancellationToken:ct)) is null)throw new KeyNotFoundException("Record no longer exists or is not editable here.");
        var parts=new List<NovaRecordPart>();
        parts.Add(await ReadPart(db,tx,kind=="fleet"?"EQUIP_HEADER":"OPS_HEADER",kind=="fleet"?"EH_ID":"PB_ID",id,"header",kind=="fleet"?"Vehicle":"Order & bill-to",kind=="fleet"?FleetFields:OrderFields,ct));
        if(kind=="orders")
        {
            var stops=await db.QueryAsync<StopIdentity>(new CommandDefinition("SELECT OPS_STOP_ID AS Id, TRIM(OPS_TYPE) AS Kind FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id AND OPS_TYPE IN ('P','D') ORDER BY OPS_STOP_ID"+(locking?" WITH LOCK":""),new{id},tx,cancellationToken:ct));
            foreach(var stop in stops)parts.Add(await ReadPart(db,tx,"OPS_STOP_REC","OPS_STOP_ID",stop.Id,$"stop-{stop.Id}",stop.Kind=="P"?"Shipper / pickup":"Consignee / delivery",StopFields,ct));
        }
        var array=parts.ToArray();
        return new(id,kind=="fleet"?$"Vehicle · {array[0].Fields[0].Value}":$"Order · {id}",Revision(array),array);
    }
    private static object? ParseField(NovaField field,string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        if(field.Kind is "number" or "dimension" or "integer" or "latitude" or "longitude")
        {
            if(!decimal.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n) ||
                (field.Kind=="latitude" ? n is < -90 or >90 : field.Kind=="longitude" ? n is < -180 or >180 : n<0) ||
                (field.Kind=="integer" && (n!=decimal.Truncate(n) || n>int.MaxValue)) ||
                (field.Kind=="dimension" && (n>99999999 || decimal.Round(n,1)!=n)) ||
                (field.Kind=="number" && (n>999999999999 || decimal.Round(n,3)!=n)))throw new FormatException($"Enter a valid {field.Label.ToLowerInvariant()}.");
            return n;
        }
        if(field.Kind=="datetime-local")
        {
            if(!DateTime.TryParseExact(value,["yyyy-MM-ddTHH:mm","yyyy-MM-ddTHH:mm:ss"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var dt))throw new FormatException($"Enter a valid {field.Label.ToLowerInvariant()}.");
            return DateTime.SpecifyKind(dt,DateTimeKind.Unspecified);
        }
        if(value.Length>field.MaxLength)throw new FormatException($"{field.Label} allows {field.MaxLength} characters.");
        if(field.Kind=="clock" && !TimeOnly.TryParseExact(value,"HHmm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new FormatException("Use four digits for shift times, such as 0600 or 1800.");
        return value.Trim();
    }
    public async Task<NovaRecordDocument> SaveRecord(string kind,long id,NovaRecordEdit input,CancellationToken ct,bool rollbackOnly=false)
    {
        RecordKind(kind);await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait,WaitTimeout=TimeSpan.FromSeconds(10)});
        var current=await ReadRecord(db,tx,kind,id,true,ct);
        if(input.Revision!=current.Revision)throw new FormatException("This record changed in Nova. Close and reopen it before saving.");
        if(input.Values is null || input.Values.Count!=current.Parts.Length)throw new FormatException("Incomplete record. Reload before saving.");
        foreach(var part in current.Parts)
        {
            if(!input.Values.TryGetValue(part.Key,out var values) || values.Count!=part.Fields.Length || part.Fields.Any(f=>!values.ContainsKey(f.Key)))throw new FormatException("Incomplete fields. Reload before saving.");
            var edits=new Dictionary<string,object?>();
            foreach(var field in part.Fields)
            {
                var value=values[field.Key];
                if(value==field.Value)continue;
                if(field.ReadOnly)throw new FormatException($"{field.Label} cannot be changed here.");
                edits[field.Key]=ParseField(field,value);
            }
            if(part.Key.StartsWith("stop-"))
            {
                var start=(DateTime?)ParseField(part.Fields.Single(f=>f.Key=="OPS_APPT_LOW"),values["OPS_APPT_LOW"]);
                var end=(DateTime?)ParseField(part.Fields.Single(f=>f.Key=="OPS_APPT_HIGH"),values["OPS_APPT_HIGH"]);
                if((edits.ContainsKey("OPS_APPT_LOW") || edits.ContainsKey("OPS_APPT_HIGH")) && ((start is null)!=(end is null) || end<start))throw new FormatException("Provide both appointment times, with end at or after start.");
                if(edits.Keys.Any(k=>k is "OPS_ADDR1" or "OPS_ADDR2" or "OPS_ADDR3" or "OPS_CITY" or "OPS_STATE" or "OPS_ZIP" or "OPS_COUNTRY") && !edits.ContainsKey("OPS_LATITUDE") && !edits.ContainsKey("OPS_LONGITUDE"))
                { edits["OPS_LATITUDE"]=null;edits["OPS_LONGITUDE"]=null; }
                var lat=edits.TryGetValue("OPS_LATITUDE",out var a)?a:ParseField(part.Fields.Single(f=>f.Key=="OPS_LATITUDE"),values["OPS_LATITUDE"]);
                var lon=edits.TryGetValue("OPS_LONGITUDE",out var b)?b:ParseField(part.Fields.Single(f=>f.Key=="OPS_LONGITUDE"),values["OPS_LONGITUDE"]);
                if((lat is null)!=(lon is null))throw new FormatException("Provide both latitude and longitude, or leave both blank.");
            }
            if(edits.TryGetValue("EH_DOMICILE",out var terminal) && terminal is string code && !await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM LOCMAST WHERE LM_ID=@code) FROM RDB$DATABASE",new{code},tx,cancellationToken:ct)))throw new FormatException("Choose an existing Nova terminal code.");
            if(edits.Count==0)continue;
            var isStop=part.Key.StartsWith("stop-");var table=isStop?"OPS_STOP_REC":kind=="fleet"?"EQUIP_HEADER":"OPS_HEADER";var pk=isStop?"OPS_STOP_ID":kind=="fleet"?"EH_ID":"PB_ID";
            var args=new DynamicParameters();args.Add("id",isStop?long.Parse(part.Key[5..],CultureInfo.InvariantCulture):id);
            var sets=edits.Select((e,i)=>{args.Add("v"+i,e.Value);return e.Key+"=@v"+i;}).ToArray();
            if(await db.ExecuteAsync(new CommandDefinition($"UPDATE {table} SET {string.Join(',',sets)} WHERE {pk}=@id",args,tx,cancellationToken:ct))!=1)throw new FormatException("Record changed while saving.");
        }
        var saved=await ReadRecord(db,tx,kind,id,false,ct);
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
        return saved;
    }
}
