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
        if(lines.Count is <1 or >200)throw new FormatException("Lykes requires 1–200 explicit native lines.");
        foreach(var line in lines)
            if(!options.KeywordKinds.ContainsKey(line.Keyword))throw new FormatException($"Unmapped Nova line keyword: {line.Keyword}.");
        var freight=lines.Where(l=>options.KeywordKinds[l.Keyword]=="freight").OrderBy(l=>l.Sku,StringComparer.Ordinal).ToArray();
        if(freight.Length==0||freight.Any(l=>string.IsNullOrWhiteSpace(l.Sku))||freight.Select(l=>l.Sku).Distinct(StringComparer.Ordinal).Count()!=freight.Length)
            throw new FormatException("MVP import requires nonempty, unique freight SKUs; reconcile ambiguous freight in Nova.");
        // Sequence, stop row ID, and financial amounts are deliberately absent.
        var signature=JsonSerializer.Serialize(freight.Select(l=>new {l.StopKind,l.Keyword,l.Sku,l.Description,l.Uom,l.Cube,l.Pieces,l.Weight,l.Kilos,l.Pallets,l.PalletPositions}));
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        var prior=source["Lykes"] as JsonObject;
        if(prior is not null && (prior["IdentityMode"]?.GetValue<string>()!="aurora-snapshot" || prior["FreightFingerprint"]?.GetValue<string>()!=fingerprint))
            throw new FormatException("Nova freight changed or uses earlier native line identities. Reconcile the operational pieces in Aurora before importing; Nova and rating were not changed.");
        var seed=source["NativeOrderId"]?.ToJsonString()??source["Id"]?.ToJsonString()??throw new FormatException("Native order identity is required.");
        var copies=freight.Select(line=>
        {
            var copy=JsonSerializer.Deserialize<NovaSyncLine>(JsonSerializer.Serialize(line))!;
            copy.LineId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"aurora-snapshot:{seed}:{fingerprint}:{line.Sku}"))[..16]).ToString();
            // Financial and native row metadata must not leak into operational identity.
            copy.Sequence=0;copy.StopId=0;copy.Amount=0;
            return copy;
        }).ToArray();
        var projectionOptions=JsonSerializer.Deserialize<LykesNovaOptions>(JsonSerializer.Serialize(options))!;
        projectionOptions.NativeReadOnly=false;
        var projectionSource=(JsonObject)source.DeepClone();
        (projectionSource["Lykes"] as JsonObject)?.Remove("IdentityMode");
        var result=LykesNovaMapping.Project(projectionSource,copies,projectionOptions);
        result["Lykes"]!["IdentityMode"]="aurora-snapshot";
        result["Lykes"]!["FreightFingerprint"]=fingerprint;
        return result;
    }
}
