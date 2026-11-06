using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    static string ImportRevision(NovaRecordDocument record,string notes)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.Revision+"\n"+notes)));

    public async Task<ImportedOrderEdit> ReadImportedOrder(string pro,CancellationToken ct)
    {
        var id=await ResolveOrderPro(pro,ct);
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        var record=await ReadRecord(db,tx,"orders",id,false,ct);
        var notes=await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@id",new{id},tx,cancellationToken:ct))??"";
        var draft=ImportedOrderMapping.Read(record,notes,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId));
        var lines=await db.QueryAsync<(decimal? Pieces, string? Description,decimal? Weight,string? Cube)>(new CommandDefinition(
            "SELECT OPL_PIECES,OPL_DESC,OPL_WEIGHT,OPL_CUBE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@id ORDER BY OPL_SEQUENCE",new{id},tx,cancellationToken:ct));
        draft.FreightLines=lines.Select(l=>new DocumentFreightLine{Pieces=l.Pieces,Description=l.Description,WeightLb=l.Weight,
            CubeFt3=decimal.TryParse(l.Cube,NumberStyles.Number,CultureInfo.InvariantCulture,out var cube)?cube:null}).ToArray();
        return new(ImportRevision(record,notes),draft,true);
    }

    public async Task SaveImportedOrder(string pro,ImportedOrderEdit input,CancellationToken ct,bool rollbackOnly=false)
    {
        if(input.Draft.Order.Id!=pro || OrderDocumentDraft.SplitTag(input.Draft.TagNumber).Pro!=pro)throw new FormatException("The PRO number cannot be changed on an existing order.");
        var id=await ResolveOrderPro(pro,ct);
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        var current=await ReadRecord(db,tx,"orders",id,true,ct);
        var notes=await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@id",new{id},tx,cancellationToken:ct))??"";
        if(input.Revision!=ImportRevision(current,notes))throw new FormatException("This order changed in Nova. Close and reopen it before saving.");
        var values=ImportedOrderMapping.Edit(current,input.Draft,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId),notes,input.ClearSchedule);
        var parties=ImportedOrderMapping.Parts(current);
        foreach(var (part,_,_) in NovaImportAddress.Parties){
            var key=part=="header"?"PB_BILL_ID":"OPS_ACCTID";
            var code=input.Draft.AccountId(part)?.Trim();
            if(string.Equals(code,values[parties[part].Key][key]?.Trim(),StringComparison.OrdinalIgnoreCase))continue;
            var linked=string.IsNullOrWhiteSpace(code)?null:await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT AC_CUST FROM ACCOUNT WHERE UPPER(TRIM(AC_CUST))=@code",new{code=code.ToUpperInvariant()},tx,cancellationToken:ct));
            values[parties[part].Key][key]=linked?.Trim();
        }
        var header=current.Parts.Single(p=>p.Key=="header");
        var terminalField=header.Fields.Single(f=>f.Key=="PB_LOCATION");
        if(values["header"]["PB_LOCATION"]!=terminalField.Value)await ValidTerminal(db,tx,ParseField(terminalField,values["header"]["PB_LOCATION"]),ct);
        var statusField=header.Fields.Single(f=>f.Key=="PB_STATUS");
        if(values["header"]["PB_STATUS"]!=statusField.Value && await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM CT_STAT WHERE CTS_ID=@status AND (CTS_ORDER_VISIBLE='Y' OR CTS_ID=110)",new{status=ParseField(statusField,values["header"]["PB_STATUS"])},tx,cancellationToken:ct))!=1)throw new FormatException("Choose a valid order status.");
        foreach(var part in current.Parts)
        {
            var edits=part.Fields.Where(f=>f.Key!="PBNUM" && values[part.Key][f.Key]!=f.Value).ToDictionary(f=>f.Key,f=>ParseField(f,values[part.Key][f.Key]));
            if(part.Key.StartsWith("stop-") && edits.Keys.Any(k=>k is "OPS_ADDR1" or "OPS_ADDR2" or "OPS_CITY" or "OPS_STATE" or "OPS_ZIP" or "OPS_COUNTRY"))
            { edits["OPS_LATITUDE"]=null;edits["OPS_LONGITUDE"]=null; }
            if(part.Key=="header")edits["PB_NOTES"]=input.Draft.NotesWithReferences();
            if(edits.Count==0)continue;
            var isStop=part.Key.StartsWith("stop-");
            var args=new DynamicParameters();args.Add("id",isStop?long.Parse(part.Key[5..],CultureInfo.InvariantCulture):id);
            var sets=edits.Select((e,i)=>{args.Add("v"+i,e.Value);return e.Key+"=@v"+i;}).ToArray();
            await db.ExecuteAsync(new CommandDefinition($"UPDATE {(isStop?"OPS_STOP_REC":"OPS_HEADER")} SET {string.Join(',',sets)} WHERE {(isStop?"OPS_STOP_ID":"PB_ID")}=@id",args,tx,cancellationToken:ct));
        }
        var pickup=ImportedOrderMapping.Parts(current)["pickup"];
        await WriteImportReferences(db,tx,id,long.Parse(pickup.Key[5..],CultureInfo.InvariantCulture),input.Draft,
            ImportedOrderMapping.Read(current,notes,TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)),ct);
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
    }
}
