using System.Globalization;
using System.Text.Json.Nodes;
namespace Aurora.Contracts;

public static class DriverPlanning
{
    public static string? Unavailable(NovaDriver d,DateTimeOffset start,DateTimeOffset end)
    {
        if(d.HireDate?.Date>start.Date)return "Hire date is after departure.";
        if(d.TerminationDate?.Date<=end.Date)return "Employment ends before this trip is complete.";
        if(d.LicenseExpiry?.Date<end.Date)return "License expires before this trip is complete.";
        return null;
    }
    // Called after saved equipment costs have been restored, so repeated optimization never compounds labor.
    public static string Apply(string json,NovaDriver[] drivers,HashSet<long> booked,string basis,bool initialize)
    {
        basis=DriverCostBasis.Separate; // Equipment and driver labor are always separate inputs.
        var root=RoutingInput.Parse(json);var report=root["reporting"]!.AsObject();
        var terminal=RoutingInput.Text(report["terminal"]);
        var bindings=report["drivers"]?.DeepClone()?.AsObject()??new JsonObject();
        var selected=new JsonArray();var used=new HashSet<long>();
        foreach(var truck in RoutingInput.Items(root["vehicles"]).ToArray())
        {
            var unit=RoutingInput.Text(truck["id"])!;
            var start=RoutingInput.Time(truck["start"]?["earliestStartTime"])??throw new FormatException("Set the route departure time.");
            var end=RoutingInput.Time(truck["end"]?["latestEndTime"])??throw new FormatException("Set the route return time.");
            var binding=bindings[unit];
            var driver=drivers.SingleOrDefault(d=>d.Id.ToString(CultureInfo.InvariantCulture)==RoutingInput.Text(binding?["id"])&&d.Terminal==terminal);
            if(driver is null)throw new FormatException($"Select a driver for truck {unit} in route setup, or mark the truck unavailable.");
            if(booked.Contains(driver.Id))throw new FormatException($"Driver {driver.Number} is reserved by another manifest. Choose another driver.");
            if(Unavailable(driver,start,end) is {} problem)throw new FormatException($"Driver {driver.Number}: {problem}");
            if(driver.PerHour is null or <0)throw new FormatException($"Enter driver {driver.Number}'s hourly labor cost in Drivers.");
            if(!used.Add(driver!.Id))throw new FormatException($"Driver {driver.Number} cannot operate two trucks in the same plan.");
            var shiftStart=PlanTime(binding,"shiftStart",unit);
            var shiftEnd=PlanTime(binding,"shiftEnd",unit);
            if(shiftStart==shiftEnd)throw new FormatException($"Truck {unit}: driver shift start and end must differ.");
            var duration=shiftEnd-shiftStart;if(duration<=TimeSpan.Zero)duration+=TimeSpan.FromDays(1);
            var begins=new DateTimeOffset(DateOnly.FromDateTime(start.DateTime).ToDateTime(shiftStart),start.Offset);
            // Overnight shifts that started on the prior date can include an early-morning departure.
            if(shiftEnd<shiftStart&&TimeOnly.FromDateTime(start.DateTime)<shiftEnd)begins=begins.AddDays(-1);
            var finishes=begins+duration;
            if(end-start<=TimeSpan.FromHours(24))end=end<finishes?end:finishes;
            start=start>begins?start:begins;
            if(start>=end)throw new FormatException($"Driver {driver.Number}'s shift does not overlap the route schedule.");
            truck["start"]!["earliestStartTime"]=start.ToString("O");truck["end"]!["latestEndTime"]=end.ToString("O");
            if(end-start>TimeSpan.FromHours(24))
            {
                var working=RoutingInput.Items(truck["breaks"]?["working"]).FirstOrDefault();
                if(working is null)throw new FormatException("Multi-day driver plans need working and rest limits.");
                working["maximumWorkingDuration"]=Math.Min(working["maximumWorkingDuration"]!.GetValue<int>(),(int)duration.TotalSeconds);
                var driving=RoutingInput.Items(truck["breaks"]?["driving"]).FirstOrDefault();
                if(driving is not null)driving["maximumDrivingDuration"]=Math.Min(driving["maximumDrivingDuration"]!.GetValue<int>(),(int)duration.TotalSeconds);
            }
            var equipment=truck["costs"]!["perHour"]!.GetValue<double>();
            var labor=(double)driver.PerHour!.Value;
            truck["costs"]!["perHour"]=equipment+labor;
            bindings[unit]=new JsonObject{["id"]=driver.Id.ToString(CultureInfo.InvariantCulture),["number"]=driver.Number,["name"]=driver.Name,["revision"]=driver.Revision,["shiftStart"]=shiftStart.ToString("HH:mm",CultureInfo.InvariantCulture),["shiftEnd"]=shiftEnd.ToString("HH:mm",CultureInfo.InvariantCulture),["maximumWorkingSeconds"]=(int)duration.TotalSeconds,["equipmentPerHour"]=equipment,["addedLaborPerHour"]=labor};
            selected.Add(truck.DeepClone());
        }
        if(selected.Count==0)throw new FormatException("Select at least one truck and driver for optimization.");
        root["vehicles"]=selected;report["drivers"]=bindings;report["driverCostBasis"]=basis;
        report.Remove("driverExclusions");
        return root.ToJsonString();
    }
    private static TimeOnly PlanTime(JsonNode? binding,string key,string unit)
    {
        if(!TimeOnly.TryParseExact(RoutingInput.Text(binding?[key]),"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))
            throw new FormatException($"Set driver shift start and end for truck {unit} in route setup.");
        return time;
    }

}
