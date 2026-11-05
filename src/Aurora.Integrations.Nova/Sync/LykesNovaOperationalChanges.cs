using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;

namespace Aurora.Integrations.Nova.Sync;

public static class LykesNovaOperationalChanges
{
    public static readonly string[] EditableOrderFields = [
        "Customer","Address","Address2","City","State","PostalCode","Country","Latitude","Longitude","CustomerCode",
        "Shipper","OriginAddress","OriginAddress2","OriginCity","OriginState","OriginPostalCode","OriginCountry","OriginLatitude","OriginLongitude",
        "AppointmentFrom","AppointmentTo","ScheduledAt","Notes",
        // These describe FreightOps execution; they are retained, not translated into guessed Nova status events.
        "FreightOpsState","FreightOpsParent"
    ];

    public static void ValidateOrder(JsonObject next,JsonObject prior)
    {
        next.Deserialize<TmsOrder>()!.Validate();
        foreach(var key in next.Select(p=>p.Key).Concat(prior.Select(p=>p.Key)).Distinct().Except(EditableOrderFields))
            if(!Equivalent(next[key],prior[key],key))
                throw new FormatException($"{key} changed. Operational sync cannot change freight, billing, identities, or order status; review this change before retrying.");
        if(!SameNativeTime(next["ScheduledAt"],prior["ScheduledAt"]) &&
            !SameNativeTime(next["ScheduledAt"],next["AppointmentFrom"]))
            throw new FormatException("Set the schedule to the appointment start so Nova can preserve it.");
    }

    // Firebird timestamps retain 100 microseconds. A roundtrip must not turn the
    // discarded .NET ticks into a dispatcher schedule conflict.
    public static bool SameNativeTime(JsonNode? a,JsonNode? b)
    {
        if(DateTimeOffset.TryParse(a?.ToString(),out var left)&&DateTimeOffset.TryParse(b?.ToString(),out var right))
            return left.UtcTicks/1000==right.UtcTicks/1000;
        return SyncJson.Equal(a,b,"ScheduledAt");
    }

    // FreightOps supplies empty optional metadata on its first read. That is not a freight edit.
    private static bool Equivalent(JsonNode? a,JsonNode? b,string? property=null)
    {
        static bool Empty(JsonNode? n)=>n is null || n is JsonValue v&&v.TryGetValue<string>(out var s)&&s.Length==0;
        if(Empty(a)&&Empty(b))return true;
        if(a is JsonObject x&&b is JsonObject y)
            return x.Select(p=>p.Key).Concat(y.Select(p=>p.Key)).Distinct().All(k=>Equivalent(x[k],y[k],k));
        if(a is JsonArray left&&b is JsonArray right)
            return left.Count==right.Count&&left.Zip(right).All(p=>Equivalent(p.First,p.Second));
        return SyncJson.Equal(a,b,property);
    }
}
