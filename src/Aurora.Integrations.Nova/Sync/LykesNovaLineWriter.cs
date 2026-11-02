using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova.Sync;

// Unrated physical freight only. Caller owns transaction and durable receipt.
public static class LykesNovaLineWriter
{
    static string Text(JsonObject obj,string key)=>obj[key]?.GetValue<string>()??"";
    static decimal Number(JsonObject obj,string key)
    {
        try{return obj[key]?.Deserialize<decimal>()??throw new FormatException($"Missing {key}.");}
        catch(JsonException e){throw new FormatException($"Invalid {key}.",e);}
    }
    internal static void Unchanged(JsonObject next,JsonObject prior,params string[] editable)
    {
        static bool Empty(JsonNode? node)=>node is null||node is JsonValue value&&value.TryGetValue<string>(out var text)&&text=="";
        foreach(var key in next.Select(x=>x.Key).Concat(prior.Select(x=>x.Key)).Distinct().Except(editable))
            if(!(Empty(next[key])&&Empty(prior[key]))&&!JsonNode.DeepEquals(next[key],prior[key]))
                throw new FormatException($"{key} requires native review; this writer only corrects physical measurements.");
    }
    sealed class Header { public int Status {get;set;} public DateTime? Cancelled {get;set;} public decimal Bill {get;set;} public decimal Revenue {get;set;} public string Terminal {get;set;}=""; public string? RatingNotes {get;set;} }
    sealed class Stop { public long Id {get;set;} public string Kind {get;set;}=""; public string Rate {get;set;}=""; public long? Manifest {get;set;} public DateTime? Delivered {get;set;} }
    sealed record Edit(NovaSyncLine Line,decimal Pieces,decimal Weight,string Cube,decimal CubeDelta,decimal WeightDelta,decimal PiecesDelta,
        string Operation="update",int PalletsDelta=0,int PositionsDelta=0);

    public static Task Apply(FbConnection db,FbTransaction tx,long orderId,JsonObject data,JsonObject baseline,
        LykesNovaOptions options,IReadOnlyCollection<int> readyStatuses,Guid consumerId,CancellationToken ct)
        => ApplyCore(db,tx,orderId,data,baseline,options,readyStatuses,consumerId,false,ct);

    internal static Task Seed(FbConnection db,FbTransaction tx,long orderId,JsonObject data,
        LykesNovaOptions options,IReadOnlyCollection<int> readyStatuses,Guid consumerId,CancellationToken ct)
    {
        var empty=(JsonObject)data.DeepClone();empty["Lykes"]!["Lines"]=new JsonArray();
        return ApplyCore(db,tx,orderId,data,empty,options,readyStatuses,consumerId,true,ct);
    }

    private static async Task ApplyCore(FbConnection db,FbTransaction tx,long orderId,JsonObject data,JsonObject baseline,
        LykesNovaOptions options,IReadOnlyCollection<int> readyStatuses,Guid consumerId,bool creating,CancellationToken ct)
    {
        LykesNovaMapping.RequireSupportedWrite("order",data,options);
        if(!options.Enabled||!options.WritesEnabled)throw new FormatException("Nova Lykes reverse writes are disabled.");
        if(Text(data,"Status") is not ("Ready" or Aurora.Contracts.WorkspaceOrderStatus.Ready)||Text(data,"WorkType")!="Delivery"||data["FreightOpsParent"] is not null||
           Text(data,"FreightOpsState") is not ("" or "Created"))throw new SyncConflictException("Only unhandled delivery freight can be corrected.");
        Unchanged(data,baseline,"Pieces","Weight","Cube","Lykes","FreightOpsState","FreightOpsParent");
        var profile=data["Lykes"] as JsonObject??throw new FormatException("Complete Lykes profile required.");
        var before=baseline["Lykes"] as JsonObject??throw new FormatException("Import the Nova lines before writing them.");
        Unchanged(profile,before,"Lines");
        if(profile["Version"]?.GetValue<int>()!=2||Text(profile,"WeightUnit")!="lb"||Text(profile,"VolumeUnit")!="ft3")throw new FormatException("Lykes v2 pounds and cubic feet required.");
        var header=await db.QuerySingleOrDefaultAsync<Header>(new CommandDefinition("""
            SELECT PB_STATUS AS Status,PB_DT_CANC AS Cancelled,COALESCE(PBTOTBIL,0) AS Bill,
              COALESCE(PBCREV,0) AS Revenue,TRIM(PB_LOCATION) AS Terminal,PB_RATE_NOTES AS RatingNotes
            FROM OPS_HEADER WHERE PB_ID=@orderId AND PB_TYPE='O' WITH LOCK
            """,new{orderId},tx,cancellationToken:ct));
        if(header is null||header.Cancelled is not null||!readyStatuses.Contains(header.Status))throw new SyncConflictException("Nova order is no longer ready for physical corrections.");
        var stops=(await db.QueryAsync<Stop>(new CommandDefinition("""
            SELECT OPS_STOP_ID AS Id,TRIM(OPS_TYPE) AS Kind,CAST(OPS_RATE AS VARCHAR(10)) AS Rate,
              OPS_MANIFEST_NUMBER AS Manifest,OPS_DELIVERED_TIME AS Delivered
            FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@orderId WITH LOCK
            """,new{orderId},tx,cancellationToken:ct))).ToArray();
        if(stops.Length!=2||stops.Count(x=>x.Kind=="P")!=1||stops.Count(x=>x.Kind=="D")!=1||stops.Any(x=>x.Manifest is >0||x.Delivered is not null))
            throw new SyncConflictException("Assigned, delivered or multi-stop Nova freight needs native review.");
        if((await db.QueryAsync<long>(new CommandDefinition("SELECT OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS WHERE OSM_PKUP_DELV_ID IN @stops WITH LOCK",
            new{stops=stops.Select(x=>x.Id).ToArray()},tx,cancellationToken:ct))).Any())
            throw new SyncConflictException("Actual Nova manifest membership requires native review before line changes.");
        await db.QueryAsync<int>(new CommandDefinition("SELECT OPL_SEQUENCE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@orderId WITH LOCK",new{orderId},tx,cancellationToken:ct));
        var lines=await LykesNovaMapping.ReadLines(db,tx,orderId,ct);
        var native=(JsonObject)baseline.DeepClone();native["TerminalCode"]=header.Terminal;
        if(creating)
        {
            if(lines.Length!=0)throw new SyncConflictException("Cannot seed freight onto an existing order.");
        }
        else
        {
            var current=LykesNovaMapping.Project(native,lines,options);
            if(!SyncJson.Equal(current["Lykes"],baseline["Lykes"])||Text(current,"TerminalCode")!=Text(baseline,"TerminalCode"))
                throw new SyncConflictException("Nova line values or terminal mapping changed; import before writing.");
        }
        var rows=profile["Lines"] as JsonArray??throw new FormatException("Complete Lines array required.");
        if(rows.Count is <1 or >200)throw new FormatException("A complete snapshot of 1–200 lines is required.");
        var previous=before["Lines"]!.AsArray().OfType<JsonObject>().ToDictionary(x=>Guid.Parse(Text(x,"LineId")));
        var seen=new HashSet<Guid>();var skus=new HashSet<string>(StringComparer.Ordinal);var edits=new List<Edit>();decimal totalPieces=0,totalWeight=0,totalCube=0;
        var nextSequence=lines.Select(x=>x.Sequence).DefaultIfEmpty(0).Max();
        foreach(var node in rows)
        {
            var row=node as JsonObject??throw new FormatException("Invalid line.");
            if(!Guid.TryParse(Text(row,"LineId"),out var id)||id==Guid.Empty||!seen.Add(id))throw new FormatException("Unique nonempty line IDs are required.");
            previous.TryGetValue(id,out var prior);
            if(prior is not null&&Text(prior,"Kind")=="charge"){Unchanged(row,prior);continue;}
            if(Text(row,"Kind")!="freight")throw new FormatException("Only freight lines can be added or removed.");
            if(prior is not null)Unchanged(row,prior,"Pieces","TotalWeight","CubePerPiece","HandlingUnits","TotalCube");
            else
            {
                Unchanged(row,new JsonObject(),"LineId","Kind","Sku","PackType","Pieces","TotalWeight","CubePerPiece","HandlingUnits","TotalCube","PO","BOL","Notes");
                if(row.ContainsKey("Nova"))throw new FormatException("New lines cannot claim existing Nova metadata.");
            }
            var sku=Text(row,"Sku");
            if(string.IsNullOrWhiteSpace(sku)||!skus.Add(sku))throw new FormatException("Unique freight SKUs are required.");
            var pieces=Number(row,"Pieces");var weight=Number(row,"TotalWeight");var cube=Number(row,"CubePerPiece");
            if(pieces<1||pieces>500||pieces!=decimal.Truncate(pieces)||weight<0||cube<0||weight>99999999m||cube>99999999m)throw new FormatException("Invalid physical measurements.");
            if(row.ContainsKey("HandlingUnits")&&Number(row,"HandlingUnits")!=pieces||row.ContainsKey("TotalCube")&&Math.Abs(Number(row,"TotalCube")-pieces*cube)>0.000001m)
                throw new FormatException("Inconsistent piece/cube totals.");
            totalPieces+=pieces;totalWeight+=weight;totalCube+=pieces*cube;
            if(prior is not null&&pieces==Number(prior,"Pieces")&&weight==Number(prior,"TotalWeight")&&cube==Number(prior,"CubePerPiece"))continue;
            NovaSyncLine line;
            if(prior is null)
            {
                if(options.NewFreightKeyword is not {} keyword||!options.KeywordKinds.TryGetValue(keyword,out var kind)||kind!="freight")
                    throw new FormatException("Configure an explicit native NewFreightKeyword before adding lines.");
                var units=options.PackTypes.Where(x=>x.Value==Text(row,"PackType")).Select(x=>x.Key).ToArray();
                if(units.Length!=1)throw new FormatException("New freight packaging needs an unambiguous native UOM mapping.");
                if(lines.Any(x=>x.Sku==sku))throw new SyncConflictException("An existing SKU must retain its original line ID, even when removed in the same request.");
                if(nextSequence==int.MaxValue)throw new FormatException("Native line sequence is exhausted.");
                line=new(){LineId=id.ToString(),Sequence=++nextSequence,StopId=stops.Single(x=>x.Kind==options.FreightStopKind).Id,
                    Keyword=keyword,Sku=sku,Uom=units[0],Cube="0"};
            }
            else line=lines.Single(x=>Guid.Parse(x.LineId!.Trim())==id);
            if(options.ScalarCubeUnit!="ft3"||!decimal.TryParse(line.Cube,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var oldCube))
                throw new FormatException("Reverse physical corrections currently require scalar Nova cubic feet; preserve dimensions for native review.");
            var newCube=options.CubeBasis=="per-piece"?cube:cube*pieces;
            if(decimal.Round(weight,3)!=weight||decimal.Round(newCube,4)!=newCube||weight>99999999m||newCube>99999999m)
                throw new FormatException("Nova precision/range would lose measurement data.");
            edits.Add(new(line,pieces,weight,newCube.ToString("0.####",CultureInfo.InvariantCulture),newCube-oldCube,weight-line.Weight,pieces-line.Pieces,prior is null?"add":"update"));
        }
        foreach(var removed in previous.Where(x=>!seen.Contains(x.Key)))
        {
            if(Text(removed.Value,"Kind")!="freight")throw new FormatException("Charge rows cannot be omitted or deleted through freight sync.");
            var line=lines.Single(x=>Guid.Parse(x.LineId!.Trim())==removed.Key);
            if(options.ScalarCubeUnit!="ft3"||!decimal.TryParse(line.Cube,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var oldCube))
                throw new FormatException("Dimension or metric cube deletion requires native review.");
            edits.Add(new(line,0,0,"0",-oldCube,-line.Weight,-line.Pieces,"delete",-line.Pallets,-line.PalletPositions));
        }
        if(totalPieces<1||totalPieces>2000||Number(data,"Pieces")!=totalPieces||Math.Abs(Number(data,"Weight")-totalWeight)>0.0001m||Math.Abs(Number(data,"Cube")-totalCube)>0.000001m)
            throw new FormatException("Shipment totals must agree with freight lines.");
        if(edits.Count==0)return;
        // Zero-dollar orders can still be rated, posted, paid, or consolidated.
        // The header is already locked; never rely on price alone as an edit guard.
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID=@orderId AND
              (PB_DT_RATE IS NOT NULL OR PB_DT_AUD IS NOT NULL OR PB_DT_BILL IS NOT NULL OR PB_INV_DATE IS NOT NULL
               OR COALESCE(PBTOTPD,0)<>0 OR COALESCE(PBILPAY,0)<>0 OR COALESCE(PBILREC,0)<>0
               OR COALESCE(PB_DISCOUNT_AMT,0)<>0 OR COALESCE(PB_DISCOUNT_PERC,0)<>0 OR COALESCE(PB_COD_AMT,0)<>0
               OR COALESCE(PB_MASTER_ID,0)<>0 OR TRIM(PB_NO_CHRG)='Y')
            """,new{orderId},tx,cancellationToken:ct))!=0)
            throw new SyncConflictException("Rated, posted, commercial or consolidated freight requires native review before line changes.");
        if(edits.Any(x=>x.Operation!="update"))
        {
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='AURORA_SYNC_LINE_TOMBSTONE'",transaction:tx,cancellationToken:ct))!=1)
                throw new FormatException("Install the additive line tombstone migration before adding or deleting freight.");
            foreach(var add in edits.Where(x=>x.Operation=="add"))
            {
                if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AURORA_SYNC_LINE_TOMBSTONE WHERE LINE_ID=@id",new{id=add.Line.LineId},tx,cancellationToken:ct))!=0||
                   await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE UPPER(TRIM(OPL_SYNC_ID))=@id",new{id=add.Line.LineId!.ToUpperInvariant()},tx,cancellationToken:ct))!=0)
                    throw new SyncConflictException("Line ID is already in use or retired; supply a new identity.");
            }
        }
        // Do not leave previously computed prices/dimensional ratings stale.
        if(header.Bill!=0||header.Revenue!=0||!string.IsNullOrWhiteSpace(header.RatingNotes)||await db.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@orderId AND
              (COALESCE(OPL_RATE,0)<>0 OR COALESCE(OPL_AMOUNT,0)<>0 OR COALESCE(OPL_DIM_WGT,0)<>0 OR COALESCE(TRIM(OPL_RATE_TYPE),'')<>'')
            """,new{orderId},tx,cancellationToken:ct))!=0)
            throw new SyncConflictException("Rated/commercial freight must be reconciled in Nova before measurement changes.");
        foreach(var edit in edits)
        {
            // Nova totals are keyword-controlled. Restrict this path to ordinary freight
            // included in both piece and weight totals; never reinterpret charge keywords.
            var keyword=edit.Line.Keyword;
            if(keyword!=""&&(await db.QueryAsync<string>(new CommandDefinition("""
                SELECT KW_ID FROM KEYWORDB WHERE KW_ID=@keyword AND KW_TOT_PIECES='Y' AND KW_TOT_WEIGHT='Y' WITH LOCK
                """,new{keyword},tx,cancellationToken:ct))).Count()!=1)
                throw new FormatException("Nova keyword does not contribute ordinary freight totals.");
            if(stops.Single(x=>x.Id==edit.Line.StopId).Rate?.Trim().ToUpperInvariant() is not ("1" or "Y" or "T" or "TRUE" or "0" or "N" or "F" or "FALSE"))
                throw new FormatException("Unknown Nova stop rating flag requires review.");
        }
        foreach(var edit in edits)
        {
            var parameters=new{orderId,lineId=edit.Line.LineId,edit.Pieces,edit.Weight,edit.Cube,
                kilos=decimal.Round(edit.Weight/2.2046226218487757m,3),tons=decimal.Round(edit.Weight/2000m,3),
                metricTons=decimal.Round(edit.Weight/2204.6226218487757m,3),edit.Line.Sequence,edit.Line.StopId,edit.Line.Keyword,edit.Line.Sku,edit.Line.Uom};
            int affected;
            if(edit.Operation=="add")
            {
                // A unique reservation closes the race between an addition and a
                // deletion on another order/connection using the same UUID.
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO AURORA_SYNC_LINE_TOMBSTONE(LINE_ID,ORDER_ID,DELETED_AT) VALUES(@lineId,@orderId,NULL)",parameters,tx,cancellationToken:ct));
                affected=await db.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO OPS_LINE_ITEMS(OPL_ORDER_ID,OPL_SEQUENCE,OPL_STOP_ID,OPL_SYNC_ID,OPL_KEYWORD,OPL_SKU,OPL_UOM,
                      OPL_PIECES,OPL_WEIGHT,OPL_KILOS,OPL_TONS,OPL_MET_TONS,OPL_CUBE,OPL_DESC,OPL_PALLETS,OPL_PALLET_POSITIONS,
                      OPL_RATE,OPL_AMOUNT,OPL_DIM_WGT,OPL_RATE_TYPE)
                    VALUES(@orderId,@Sequence,@StopId,@lineId,@Keyword,@Sku,@Uom,@Pieces,@Weight,@kilos,@tons,@metricTons,@Cube,'',0,0,0,0,0,'')
                    """,parameters,tx,cancellationToken:ct));
            }
            else if(edit.Operation=="delete")
            {
                var retired=await db.ExecuteAsync(new CommandDefinition("UPDATE AURORA_SYNC_LINE_TOMBSTONE SET DELETED_AT=CURRENT_TIMESTAMP WHERE LINE_ID=@id AND ORDER_ID=@orderId AND DELETED_AT IS NULL",
                    new{id=Guid.Parse(edit.Line.LineId!).ToString(),orderId},tx,cancellationToken:ct));
                if(retired==0)await db.ExecuteAsync(new CommandDefinition("INSERT INTO AURORA_SYNC_LINE_TOMBSTONE(LINE_ID,ORDER_ID) VALUES(@id,@orderId)",
                    new{id=Guid.Parse(edit.Line.LineId!).ToString(),orderId},tx,cancellationToken:ct));
                affected=await db.ExecuteAsync(new CommandDefinition("DELETE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@orderId AND OPL_SYNC_ID=@lineId",parameters,tx,cancellationToken:ct));
            }
            else affected=await db.ExecuteAsync(new CommandDefinition("""
                UPDATE OPS_LINE_ITEMS SET OPL_PIECES=@Pieces,OPL_WEIGHT=@Weight,OPL_KILOS=@kilos,
                  OPL_TONS=@tons,OPL_MET_TONS=@metricTons,OPL_CUBE=@Cube
                WHERE OPL_ORDER_ID=@orderId AND OPL_SYNC_ID=@lineId
                """,parameters,tx,cancellationToken:ct));
            if(affected!=1)throw new SyncConflictException("Stable Nova line disappeared during correction.");
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE OPS_STOP_REC SET OPS_TTL_PIECES=COALESCE(OPS_TTL_PIECES,0)+@PiecesDelta,
                  OPS_TTL_WEIGHT=COALESCE(OPS_TTL_WEIGHT,0)+@WeightDelta,OPS_TTL_CUBE=COALESCE(OPS_TTL_CUBE,0)+@CubeDelta
                WHERE OPS_STOP_ID=@stopId AND OPS_ORDER_ID=@orderId
                """,new{orderId,stopId=edit.Line.StopId,edit.PiecesDelta,edit.WeightDelta,edit.CubeDelta},tx,cancellationToken:ct));
            if(stops.Single(x=>x.Id==edit.Line.StopId).Rate.Trim().ToUpperInvariant() is "1" or "Y" or "T" or "TRUE")
                await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE OPS_HEADER SET PBTPIECE=COALESCE(PBTPIECE,0)+@PiecesDelta,
                      PBTWEIGH=COALESCE(PBTWEIGH,0)+@WeightDelta,PB_TTL_CUBE=COALESCE(PB_TTL_CUBE,0)+@CubeDelta
                    WHERE PB_ID=@orderId
                    """,new{orderId,edit.PiecesDelta,edit.WeightDelta,edit.CubeDelta},tx,cancellationToken:ct));
            if(edit.PalletsDelta!=0||edit.PositionsDelta!=0)
            {
                await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE OPS_STOP_REC SET OPS_TTL_PALLETS=COALESCE(OPS_TTL_PALLETS,0)+@PalletsDelta,
                      OPS_TTL_PALLET_POSN=COALESCE(OPS_TTL_PALLET_POSN,0)+@PositionsDelta WHERE OPS_STOP_ID=@stopId AND OPS_ORDER_ID=@orderId
                    """,new{orderId,stopId=edit.Line.StopId,edit.PalletsDelta,edit.PositionsDelta},tx,cancellationToken:ct));
                if(stops.Single(x=>x.Id==edit.Line.StopId).Rate.Trim().ToUpperInvariant() is "1" or "Y" or "T" or "TRUE")
                    await db.ExecuteAsync(new CommandDefinition("""
                        UPDATE OPS_HEADER SET PB_TTL_PALLETS=COALESCE(PB_TTL_PALLETS,0)+@PalletsDelta,
                          PB_TTL_PALLET_POSN=COALESCE(PB_TTL_PALLET_POSN,0)+@PositionsDelta WHERE PB_ID=@orderId
                        """,new{orderId,edit.PalletsDelta,edit.PositionsDelta},tx,cancellationToken:ct));
            }
        }
        // Raw Nova metadata (kilos, cube text, pieces) has changed. Refresh the
        // importing connection once so its canonical baseline does not stay stale.
        // This event and the receipt commit together; receipt retries cannot duplicate it.
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AURORA_SYNC_EVENT(EVENT_ID,CONSUMER_ID,ENTITY_TYPE,ENTITY_ID,CHANGE_KIND)
            VALUES(GEN_ID(AURORA_SYNC_EVENT_GEN,1),@consumer,'order',@id,'U')
            """,new{consumer=consumerId.ToString(),id=orderId.ToString(CultureInfo.InvariantCulture)},tx,cancellationToken:ct));
    }
}
