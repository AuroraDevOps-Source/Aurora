using Aurora.Contracts;

var start = DateTimeOffset.Parse("2026-10-26T23:00:00-04:00");
RouteStopDto Stop(int? arrival, int? departure, bool depot = false) => new("location", 0, 0, arrival is {} a ? start.AddMinutes(a) : null, departure is {} d ? start.AddMinutes(d) : null, depot, depot ? [] : ["ORDER"]);
RouteSummaryDto Route(params RouteStopDto[] stops) => new("TRK", 1, 1, 10, 3600, 100, .5, .5, "NY", 7200, 7200, 1, 1, 1, 1, [], stops, start, start.AddHours(2));
var count = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
var intervals = RouteTimelineSchedule.Read(Route(Stop(null, 0, true), Stop(30, 60), Stop(120, null, true)));
Check(intervals.Count == 3, "Outbound, customer, and return intervals appear once");
Check(intervals[1].Start == start.AddMinutes(30) && intervals[1].End.Date > start.Date, "Overnight dwell preserves the date boundary");
Check(intervals.Sum(i => (i.End - i.Start).TotalSeconds) == 7200, "Complete route covers the elapsed schedule exactly");
Check(intervals.All(i => i.Start.Offset == start.Offset), "Source offsets are retained");
var incomplete = RouteTimelineSchedule.Read(Route(Stop(30, null), Stop(75, 90)));
Check(!incomplete.Any(i => i.Kind == "travel" && i.End == start.AddMinutes(75)), "Missing departure does not fabricate travel across a stop");
Check(RouteTimelineSchedule.Read(Route(Stop(null, null)) with {Departure = null, Return = null}).Count == 0, "Missing timestamps do not generate estimated intervals");
Check(RouteTimelineSchedule.Read(Route(Stop(60, 30))).All(i => i.End >= i.Start), "Reversed timestamps do not create negative widths");
Check(RouteTimelineSchedule.Read(Route(Stop(30, 30))).Any(i => i.Kind == "stop" && i.Start == i.End), "Zero-duration visits remain inspectable");
var offsetStop = Stop(30, 60) with { Arrival = start.AddMinutes(30).ToOffset(TimeSpan.Zero), Departure = start.AddMinutes(60).ToOffset(TimeSpan.Zero) };
Check(RouteTimelineSchedule.Read(Route(offsetStop)).First().End - start == TimeSpan.FromMinutes(30), "Mixed offsets compare actual instants");
Console.WriteLine($"{count} route timeline checks passed.");
