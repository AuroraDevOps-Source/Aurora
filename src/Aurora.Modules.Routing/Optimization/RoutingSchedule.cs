using Aurora.Contracts;
using System.Text.Json.Nodes;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>Display instants in the vehicle's explicit source offset, never the server's timezone.</summary>
public static class RoutingSchedule
{
    public static TimeSpan? VehicleOffset(JsonObject? vehicle)
    {
        try { return RoutingInput.Time(vehicle?["start"]?["earliestStartTime"])?.Offset; }
        catch (FormatException) { return null; }
    }

    public static DateTimeOffset? InOffset(DateTimeOffset? instant, TimeSpan? offset) =>
        instant is not null && offset is not null ? instant.Value.ToOffset(offset.Value) : instant;
}
