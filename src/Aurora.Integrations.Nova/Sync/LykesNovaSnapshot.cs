using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Integrations.Nova.Sync;

// An operational snapshot, not a claim about the identity of native database rows.
// Native rerating/resequencing may replace rows. Never infer continuity after a
// freight change, and never use these identifiers to write back to Nova.
internal static class LykesNovaSnapshot
{
    internal static JsonObject Project(JsonObject source,IReadOnlyList<NovaSyncLine> lines,LykesNovaOptions options)
    {
        var headerOnly=lines.Count==0&&options.AllowHeaderTotals;
        if(headerOnly)
        {
            var pieces=source["Pieces"]?.Deserialize<decimal>()??0;
            var weight=source["Weight"]?.Deserialize<decimal>()??0;
            var cube=source["Cube"]?.Deserialize<decimal>()??0;
            if(pieces is <1 or >500||pieces!=decimal.Truncate(pieces)||weight<0||cube<0)
                throw new FormatException("Header-only freight needs 1–500 whole pieces and nonnegative weight/cube before enrollment.");
            var keywords=options.KeywordKinds.Where(p=>p.Value=="freight").Select(p=>p.Key).ToArray();
            var units=options.PackTypes.Where(p=>p.Value.Equals("Piece",StringComparison.OrdinalIgnoreCase)).Select(p=>p.Key).ToArray();
            if(keywords.Length!=1||units.Length!=1||options.ScalarCubeUnit!="ft3")throw new FormatException("Header totals require an explicit freight keyword, Piece unit and cubic feet configuration.");
            lines=[new NovaSyncLine{StopKind=options.FreightStopKind,Keyword=keywords[0],Sku="ORDER-"+source["Id"]!.ToString(),
                Description="Shipment totals; detailed descriptions remain in order notes",Uom=units[0],Pieces=pieces,Weight=weight,
                Cube=(options.CubeBasis=="per-piece"?cube/pieces:cube).ToString(System.Globalization.CultureInfo.InvariantCulture)}];
        }
        if(lines.Count is <1 or >200)throw new FormatException("Lykes requires 1–200 explicit native lines.");
        foreach(var line in lines)
            if(!options.KeywordKinds.ContainsKey(line.Keyword))throw new FormatException($"Unmapped Nova line keyword: {line.Keyword}.");
        var freight=lines.Where(l=>options.KeywordKinds[l.Keyword]=="freight")
            .OrderBy(l=>l.Sku,StringComparer.Ordinal).ThenBy(LineSignature,StringComparer.Ordinal).ToArray();
        if(freight.Length==0)throw new FormatException("At least one freight line is required.");
        // Sequence, stop row ID, and financial amounts are deliberately absent.
        var signature=JsonSerializer.Serialize(freight.Select(l=>new {l.StopKind,l.Keyword,l.Sku,l.Description,l.Uom,l.Cube,l.Pieces,l.Weight,l.Kilos,l.Pallets,l.PalletPositions}));
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        var prior=source["Lykes"] as JsonObject;
        var priorRows=(prior?["Lines"] as JsonArray)?.OfType<JsonObject>().ToArray()??[];
        var nativeSeed=prior is not null&&prior["IdentityMode"] is null&&options.OperationalOrderCreationEnabled&&priorRows.Length==freight.Length;
        if(nativeSeed)
        {
            var strict=JsonSerializer.Deserialize<LykesNovaOptions>(JsonSerializer.Serialize(options))!;strict.NativeReadOnly=false;
            var seededRows=LykesNovaMapping.Project(source,freight,strict)["Lykes"]!["Lines"]!.AsArray().OfType<JsonObject>().ToArray();
            foreach(var row in seededRows)
            {
                var old=priorRows.SingleOrDefault(p=>p["LineId"]?.ToString()==row["LineId"]?.ToString());
                if(old is null||new[]{"Sku","Description","Pieces","TotalWeight","CubePerPiece","PackType"}.Any(k=>
                    k=="Description"?(old[k]?.ToString()??"")!=(row[k]?.ToString()??""):!Aurora.Modules.Sync.SyncJson.Equal(old[k],row[k])))
                    throw new FormatException("New native freight does not exactly match its original FreightOps identities and measurements.");
            }
        }
        if(prior is not null && !nativeSeed && (prior["IdentityMode"]?.GetValue<string>()!="aurora-snapshot" || prior["FreightFingerprint"]?.GetValue<string>()!=fingerprint))
            throw new FormatException("Nova freight changed or uses earlier native line identities. Reconcile the operational pieces in Aurora before importing; Nova and rating were not changed.");
        var seed=source["NativeOrderId"]?.ToJsonString()??source["Id"]?.ToJsonString()??throw new FormatException("Native order identity is required.");
        var skuCounts=freight.GroupBy(l=>l.Sku,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
        var usedSkus=freight.Where(l=>!string.IsNullOrWhiteSpace(l.Sku)&&skuCounts[l.Sku]==1).Select(l=>l.Sku).ToHashSet(StringComparer.Ordinal);
        var occurrences=new Dictionary<string,int>(StringComparer.Ordinal);
        var copies=freight.Select(line=>
        {
            var copy=JsonSerializer.Deserialize<NovaSyncLine>(JsonSerializer.Serialize(line))!;
            // Internal codes distinguish source lines; they never modify Nova's SKU.
            // Identical rows remain separate occurrences in the unchanged snapshot.
            if(string.IsNullOrWhiteSpace(line.Sku)||skuCounts[line.Sku]>1)
            {
                var itemSignature=LineSignature(line);
                occurrences.TryGetValue(itemSignature,out var occurrence);occurrences[itemSignature]=++occurrence;
                for(var salt=0;;salt++)
                {
                    copy.Sku="AUR-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{itemSignature}:{occurrence}:{salt}")))[..24];
                    if(usedSkus.Add(copy.Sku))break;
                }
            }
            copy.LineId=priorRows.SingleOrDefault(r=>r["Sku"]?.ToString()==copy.Sku)?["LineId"]?.ToString()
                ??new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"aurora-snapshot:{seed}:{fingerprint}:{copy.Sku}"))[..16]).ToString();
            // Financial and native row metadata must not leak into operational identity.
            copy.Sequence=0;copy.StopId=0;copy.Amount=0;
            return copy;
        }).ToArray();
        var projectionOptions=JsonSerializer.Deserialize<LykesNovaOptions>(JsonSerializer.Serialize(options))!;
        projectionOptions.NativeReadOnly=false;
        var projectionSource=(JsonObject)source.DeepClone();
        (projectionSource["Lykes"] as JsonObject)?.Remove("IdentityMode");
        var result=LykesNovaMapping.Project(projectionSource,copies,projectionOptions);
        var projected=result["Lykes"]!["Lines"]!.AsArray();
        for(var i=0;i<copies.Length;i++)
        {
            if(copies[i].Sku==freight[i].Sku)continue;
            var row=projected.OfType<JsonObject>().Single(r=>r["LineId"]!.GetValue<string>()==copies[i].LineId);
            row["SkuGenerated"]=true;
            row["Nova"]!["Sku"]=freight[i].Sku; // Keep the original blank/repeated native value.
        }
        result["Lykes"]!["IdentityMode"]="aurora-snapshot";
        result["Lykes"]!["FreightFingerprint"]=fingerprint;
        if(headerOnly)result["Lykes"]!["FreightSource"]="header-totals";
        return result;
    }
    static string LineSignature(NovaSyncLine l)=>JsonSerializer.Serialize(new {l.StopKind,l.Keyword,l.Sku,l.Description,l.Uom,l.Cube,l.Pieces,l.Weight,l.Kilos,l.Pallets,l.PalletPositions});
}
