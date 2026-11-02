using System.Globalization;
using System.Text.Json.Nodes;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Aurora.Contracts.Integration;

namespace Aurora.Integrations.Nova.Sync;

public sealed class LykesNovaOptions
{
    public bool Enabled { get; set; }
    // Empty preserves the normal source-wide import; pilot IDs are native PB_IDs.
    public long[] PilotOrderIds { get; set; } = [];
    public bool IncludesEntity(string kind,string id) => !Enabled || PilotOrderIds.Length==0 ||
        (kind is "terminal" or "customer") ||
        (kind is "order" or "order-or-manifest" && long.TryParse(id,out var native) && PilotOrderIds.Contains(native));
    public bool NativeReadOnly { get; set; } // MVP: operational IDs live in Aurora; no native order/manifest writes.
    public bool WritesEnabled { get; set; } // Separate opt-in; never enabled by import alone.
    public bool OrderLifecycleEnabled { get; set; } // Creation/cancellation are a separate rollout capability.
    public int? NewOrderReadyStatus { get; set; }
    public string OperatorInitials { get; set; } = "";
    public string? NewFreightKeyword { get; set; } // Explicit native keyword for added freight; null holds additions.
    // Native Nova code -> shared Lykes code. No implicit terminal fallback.
    public Dictionary<string,string> TerminalMappings { get; set; } = new();
    public Dictionary<string,string> KeywordKinds { get; set; } = new();
    public Dictionary<string,string> PackTypes { get; set; } = new();
    public string CubeBasis { get; set; } = ""; // per-piece or line-total
    public string ScalarCubeUnit { get; set; } = ""; // ft3 or m3
    public string DimensionUnit { get; set; } = ""; // in, cm, ft, m
    public string FreightStopKind { get; set; } = ""; // P or D; don't double-count mirrors
}

public sealed class NovaSyncLine
{
    public string? LineId { get; set; }
    public int Sequence { get; set; }
    public long StopId { get; set; }
    public string StopKind { get; set; } = "";
    public string Keyword { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Description { get; set; } = "";
    public string Uom { get; set; } = "";
    public string Cube { get; set; } = "";
    public decimal Pieces { get; set; }
    public decimal Weight { get; set; }
    public decimal Kilos { get; set; }
    public int Pallets { get; set; }
    public int PalletPositions { get; set; }
    public decimal Amount { get; set; }
}

public static class LykesNovaMapping
{
    public static void RequireSupportedRead(JsonObject data,LykesNovaOptions options)
    {
        if(data["Lykes"]?["IdentityMode"]?.GetValue<string>()=="aurora-snapshot"&&!options.NativeReadOnly)
            throw new FormatException("MVP operational identities cannot be converted to native writable line identities. Reconcile before changing mode.");
        if(data.ContainsKey("Lykes")&&!options.Enabled)
            throw new FormatException("This order already uses Lykes mapping; restore its configuration before importing a partial legacy snapshot.");
    }
    public static void RequireSupportedWrite(string entityType,JsonObject data,LykesNovaOptions options)
    {
        if(options.Enabled&&options.NativeReadOnly)
            throw new FormatException("Nova business writes are disabled for this connection; Nova was not changed.");
        if(entityType is "order" or "manifest" && (options.Enabled&&options.NativeReadOnly || data["Lykes"]?["IdentityMode"]?.GetValue<string>()=="aurora-snapshot"))
            throw new FormatException("Nova owns freight in the MVP. Reverse order, line and manifest changes require native review; Nova was not changed.");
        if(entityType is "order" or "manifest" && (options.Enabled||data.ContainsKey("Lykes")) &&
           !(entityType=="order"&&options.Enabled&&options.WritesEnabled&&data["Lykes"] is JsonObject))
            throw new FormatException("Lykes Nova writes remain disabled until transactional line and rating reconciliation is implemented.");
    }
    public static async Task<NovaSyncLine[]> ReadLines(FbConnection db,FbTransaction tx,long orderId,CancellationToken ct,bool nativeReadOnly=false)
    {
        if(!nativeReadOnly && await db.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM RDB$RELATION_FIELDS
            WHERE RDB$RELATION_NAME='OPS_LINE_ITEMS' AND RDB$FIELD_NAME='OPL_SYNC_ID'
            """,transaction:tx,cancellationToken:ct))!=1)
            throw new FormatException("Nova stable line identity migration is required before Lykes import.");
        var identity=nativeReadOnly ? "CAST(NULL AS VARCHAR(36))" : "l.OPL_SYNC_ID";
        return (await db.QueryAsync<NovaSyncLine>(new CommandDefinition($"""
            SELECT {identity} AS LineId,l.OPL_SEQUENCE AS Sequence,l.OPL_STOP_ID AS StopId,
              COALESCE(TRIM(s.OPS_TYPE),'') AS StopKind,COALESCE(TRIM(l.OPL_KEYWORD),'') AS Keyword,
              COALESCE(TRIM(l.OPL_SKU),'') AS Sku,COALESCE(l.OPL_DESC,'') AS Description,
              COALESCE(TRIM(l.OPL_UOM),'') AS Uom,COALESCE(l.OPL_CUBE,'') AS Cube,
              COALESCE(l.OPL_PIECES,0) AS Pieces,COALESCE(l.OPL_WEIGHT,0) AS Weight,
              COALESCE(l.OPL_KILOS,0) AS Kilos,COALESCE(l.OPL_PALLETS,0) AS Pallets,COALESCE(l.OPL_PALLET_POSITIONS,0) AS PalletPositions,
              COALESCE(l.OPL_AMOUNT,0) AS Amount
            FROM OPS_LINE_ITEMS l LEFT JOIN OPS_STOP_REC s
              ON s.OPS_STOP_ID=l.OPL_STOP_ID AND s.OPS_ORDER_ID=l.OPL_ORDER_ID
            WHERE l.OPL_ORDER_ID=@orderId ORDER BY l.OPL_SEQUENCE
            """,new{orderId},tx,cancellationToken:ct))).ToArray();
    }

    // Returns a new snapshot; validation failures never partially modify the caller.
    public static JsonObject Project(JsonObject source,IReadOnlyList<NovaSyncLine> lines,LykesNovaOptions options)
    {
        RequireSupportedRead(source,options);
        var data=(JsonObject)source.DeepClone();
        if(!options.Enabled)return data;
        ValidateOptions(options);
        if(options.NativeReadOnly)return LykesNovaSnapshot.Project(source,lines,options);
        var nativeTerminal=data["TerminalCode"]?.GetValue<string>()??"";
        if(!options.TerminalMappings.TryGetValue(nativeTerminal,out var shared))
            throw new FormatException($"Unmapped Nova terminal: {nativeTerminal}.");
        if(lines.Count is <1 or >200)throw new FormatException("Lykes requires 1–200 explicit lines.");
        if(data["Lykes"] is JsonObject previous && previous["Version"]?.GetValue<int>()!=2)
            throw new FormatException("Changing a shipment mapping version requires reconciliation.");
        var priorRows=(data["Lykes"]?["Lines"] as JsonArray)?.OfType<JsonObject>().ToArray()??[];
        var result=new JsonArray();var ids=new HashSet<Guid>();var skus=new HashSet<string>(StringComparer.Ordinal);
        var count=0;decimal weight=0,cube=0;
        foreach(var line in lines)
        {
            if(!Guid.TryParse(line.LineId?.Trim(),out var id)||id==Guid.Empty||!ids.Add(id))
                throw new FormatException("Missing, invalid or duplicate Nova line ID; adopt identities before syncing.");
            if(!options.KeywordKinds.TryGetValue(line.Keyword,out var kind))
                throw new FormatException($"Unmapped Nova line keyword: {line.Keyword}.");
            var prior=priorRows.SingleOrDefault(r=>Guid.TryParse(r["LineId"]?.GetValue<string>(),out var old)&&old==id);
            if(prior is not null&&prior["Kind"]?.GetValue<string>()!=kind)
                throw new FormatException("Changing freight into a charge or vice versa requires reconciliation.");
            var row=prior is null?new JsonObject():(JsonObject)prior.DeepClone();
            row["LineId"]=id.ToString();row["Kind"]=kind;
            row["Nova"]=new JsonObject{["Sequence"]=line.Sequence,["StopId"]=line.StopId,["StopKind"]=line.StopKind,
                ["Keyword"]=line.Keyword,["CubeText"]=line.Cube,["Pallets"]=line.Pallets,["PalletPositions"]=line.PalletPositions,["Amount"]=line.Amount,
                ["Description"]=line.Description,["Uom"]=line.Uom,["Sku"]=line.Sku,["Pieces"]=line.Pieces,
                ["Weight"]=line.Weight,["Kilos"]=line.Kilos};
            if(kind=="freight")
            {
                if(line.StopKind!=options.FreightStopKind)throw new FormatException("Freight on an unexpected stop requires review; do not double-count mirrored lines.");
                if(line.Pieces<1||line.Pieces>500||line.Pieces!=decimal.Truncate(line.Pieces))throw new FormatException("Freight needs 1–500 whole pieces per line.");
                if(string.IsNullOrWhiteSpace(line.Sku)||!skus.Add(line.Sku))throw new FormatException("Missing or repeated freight SKU requires review.");
                if(!options.PackTypes.TryGetValue(line.Uom,out var pack))throw new FormatException($"Unmapped Nova packaging unit: {line.Uom}.");
                var pounds=LykesNovaMeasurements.Pounds(line.Weight,line.Kilos);
                var volume=LykesNovaMeasurements.CubicFeet(line.Cube,options.ScalarCubeUnit,options.DimensionUnit);
                var perPiece=options.CubeBasis=="per-piece"?volume:volume/line.Pieces;
                row["Sku"]=line.Sku;row["Pieces"]=(int)line.Pieces;row["TotalWeight"]=pounds;
                row["CubePerPiece"]=perPiece;row["PackType"]=pack;
                if(row.ContainsKey("HandlingUnits"))row["HandlingUnits"]=(int)line.Pieces;
                if(row.ContainsKey("TotalCube"))row["TotalCube"]=perPiece*line.Pieces;
                count+=(int)line.Pieces;weight+=pounds;cube+=perPiece*line.Pieces;
            }
            result.Add(row);
        }
        if(count is <1 or >2000)throw new FormatException("Shipment needs 1–2,000 freight pieces.");
        var profile=data["Lykes"] as JsonObject??new JsonObject();
        profile["Version"]=2;profile["WeightUnit"]="lb";profile["VolumeUnit"]="ft3";profile["Lines"]=result;
        if(data["Lykes"] is null)data["Lykes"]=profile;
        data["TerminalCode"]=shared;data["Pieces"]=count;data["Weight"]=weight;data["Cube"]=cube;
        return data;
    }

    internal static void ValidateOptions(LykesNovaOptions options)
    {
        static bool Invalid(Dictionary<string,string> map)=>map.Count==0||map.Any(p=>p.Key!=p.Key.Trim()||string.IsNullOrWhiteSpace(p.Value)||p.Value!=p.Value.Trim());
        if(Invalid(options.TerminalMappings)||options.TerminalMappings.Keys.Any(string.IsNullOrWhiteSpace)||options.TerminalMappings.Values.Distinct(StringComparer.Ordinal).Count()!=options.TerminalMappings.Count)
            throw new FormatException("Explicit, unambiguous Nova terminal mappings are required.");
        if(Invalid(options.KeywordKinds)||options.KeywordKinds.Values.Any(v=>v is not ("freight" or "charge"))||Invalid(options.PackTypes))
            throw new FormatException("Explicit freight/charge keywords and packaging mappings are required.");
        if(options.CubeBasis is not ("per-piece" or "line-total")||options.ScalarCubeUnit is not ("ft3" or "m3")||options.DimensionUnit is not ("in" or "cm" or "ft" or "m")||options.FreightStopKind is not ("P" or "D"))
            throw new FormatException("Confirm Nova cube units, cube basis and freight stop before enabling Lykes mapping.");
    }
}
