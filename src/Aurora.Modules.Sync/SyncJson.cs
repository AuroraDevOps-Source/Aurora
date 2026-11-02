using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Modules.Sync;

public static class SyncJson
{
    // PostgreSQL stores timestamptz in UTC. The same appointment expressed with
    // its source offset is not a local edit and must never produce an echo write.
    public static bool Equal(JsonNode? left,JsonNode? right,string? property=null)
    {
        if(left is JsonObject a && right is JsonObject b)
            return a.Count==b.Count && a.All(p=>b.ContainsKey(p.Key)&&Equal(p.Value,b[p.Key],p.Key));
        if(left is JsonArray x && right is JsonArray y)
            return x.Count==y.Count && x.Zip(y).All(pair=>Equal(pair.First,pair.Second));
        if(property is "ScheduledAt" or "AppointmentFrom" or "AppointmentTo"
            && left?.GetValueKind()==JsonValueKind.String && right?.GetValueKind()==JsonValueKind.String
            && DateTimeOffset.TryParse(JsonSerializer.Deserialize<string>(left.ToJsonString()),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var first)
            && DateTimeOffset.TryParse(JsonSerializer.Deserialize<string>(right.ToJsonString()),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var second))
        {
            // Npgsql stores timestamptz as whole microseconds since 2000. Losing
            // the final 100ns digit on storage is not a competing business edit.
            var epoch=new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc).Ticks;
            return (first.UtcTicks-epoch)/TimeSpan.TicksPerMicrosecond==(second.UtcTicks-epoch)/TimeSpan.TicksPerMicrosecond;
        }
        return JsonNode.DeepEquals(left,right);
    }
}
