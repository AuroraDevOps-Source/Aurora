namespace Aurora.Contracts;

public sealed record RouteTimelineInterval(DateTimeOffset Start, DateTimeOffset End, string Kind, string Label, RouteStopDto? Stop);

/// <summary>Only place intervals supported by timestamps; missing times are never inferred from totals.</summary>
public static class RouteTimelineSchedule
{
    public static IReadOnlyList<RouteTimelineInterval> Read(RouteSummaryDto route)
    {
        var intervals = new List<RouteTimelineInterval>();
        var cursor = route.Departure;
        var number = 0;
        foreach (var stop in route.RouteStops)
        {
            if (!stop.IsDepot) number++;
            if (cursor is { } previous && stop.Arrival is { } arrival && arrival > previous)
                intervals.Add(new(previous, arrival, "travel", "Between stops", null));
            if (stop.Arrival is { } start && stop.Departure is { } end && end >= start)
                intervals.Add(new(start, end, stop.IsDepot ? "depot" : "stop", stop.IsDepot ? "Terminal" : $"Stop {number}", stop));
            // An unknown departure breaks continuity. Do not bridge missing visits with a travel bar.
            cursor = stop.Departure ?? (stop.IsDepot ? stop.Arrival : null);
        }
        if (cursor is { } last && route.Return is { } returned && returned > last)
            intervals.Add(new(last, returned, "travel", "Return to terminal", null));
        return intervals;
    }
}
