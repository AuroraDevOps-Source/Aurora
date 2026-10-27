using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public sealed record TripRestRules(double DrivingHours=10,double WorkingHours=12,double RestHours=12)
{
    public void Validate()
    {
        if(new[]{DrivingHours,WorkingHours,RestHours}.Any(n=>!double.IsFinite(n)||n<1||n>24)||WorkingHours<DrivingHours)
            throw new FormatException("Enter rest limits between 1 and 24 hours, with working hours at least as long as driving hours.");
    }
}
public static class MultiDayPlanning
{
    public static string Apply(string json,DateOnly departure,DateOnly returning,string returnTime,TripRestRules? rules)
    {
        if(returning<departure||returning.DayNumber-departure.DayNumber>13)throw new FormatException("Choose a return date within 14 days of departure.");
        if(!TimeOnly.TryParseExact(returnTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var clock))throw new FormatException("Enter a valid return time.");
        if(returning==departure)return json;
        rules??=new();rules.Validate();var root=RoutingInput.Parse(json);
        foreach(var truck in RoutingInput.Items(root["vehicles"]).Concat(RoutingInput.Items(root["reporting"]?["unavailableVehicles"])))
        {
            var begin=RoutingInput.Time(truck["start"]?["earliestStartTime"])??throw new FormatException("Choose a departure time.");
            truck["end"]!["latestEndTime"]=new DateTimeOffset(returning.ToDateTime(clock),begin.Offset).ToString("O");
        }
        root["reporting"]??=new JsonObject();root["reporting"]!["tripRestRules"]=JsonSerializer.SerializeToNode(rules);
        return ApplyRest(root.ToJsonString());
    }
    public static string ApplyRest(string json)
    {
        var root=RoutingInput.Parse(json);var rules=root["reporting"]?["tripRestRules"]?.Deserialize<TripRestRules>();
        foreach(var truck in RoutingInput.Items(root["vehicles"]))
        {
            var start=RoutingInput.Time(truck["start"]?["earliestStartTime"]);var end=RoutingInput.Time(truck["end"]?["latestEndTime"]);
            if(start is null||end is null)continue;
            if(end-start>TimeSpan.FromHours(24)&&rules is null && root["reporting"]?[PlannerOvertime.ShiftKey]?[RoutingInput.Text(truck["id"])!] is null)throw new FormatException("Multi-day routes need driving, working and rest limits. Start a new plan with a return date.");
            if(rules is null)continue;rules.Validate();
            truck["routing"]??=new JsonObject();truck["routing"]!["trafficMode"]="CONSTANT";
            var driverLimit=root["reporting"]?["drivers"]?[RoutingInput.Text(truck["id"])!]?["maximumWorkingSeconds"]?.GetValue<int>()??86400;
            truck["breaks"]=new JsonObject{
                ["driving"]=new JsonArray(new JsonObject{["maximumDrivingDuration"]=Math.Min((int)(rules.DrivingHours*3600),driverLimit),["minimumBreakDurations"]=new JsonArray((int)(rules.RestHours*3600))}),
                ["working"]=new JsonArray(new JsonObject{["maximumWorkingDuration"]=Math.Min((int)(rules.WorkingHours*3600),driverLimit),["minimumBreakDurations"]=new JsonArray((int)(rules.RestHours*3600))})};
        }
        return root.ToJsonString();
    }
}
