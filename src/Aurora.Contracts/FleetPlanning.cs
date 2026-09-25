using System.Globalization;
using System.Text.Json.Nodes;
namespace Aurora.Contracts;

public static class FleetPlanning
{
    public static EquipmentUnitDto[] Eligible(EquipmentCatalogDto catalog,string terminal) => catalog.Units
        .Where(u=>u.UseForRouting && u.Available && u.Terminal.Equals(terminal,StringComparison.OrdinalIgnoreCase)
            && catalog.Types.Any(t=>t.Code==u.TypeCode && !t.PowerOnly)).OrderBy(u=>u.Id,StringComparer.Ordinal).ToArray();
    public static string Build(string json,EquipmentCatalogDto catalog)
    {
        var root=RoutingInput.Parse(json);
        var terminal=RoutingInput.Text(root["reporting"]?["terminal"]) ?? throw new FormatException("Orders need a terminal to select the company's fleet.");
        var depot=RoutingInput.Text(RoutingInput.Items(root["depots"]).FirstOrDefault()?["locationId"])
            ?? RoutingInput.Text(RoutingInput.Items(root["vehicles"]).FirstOrDefault()?["start"]?["locationId"])
            ?? throw new FormatException("Orders need a depot location.");
        var dateText=RoutingInput.Text(RoutingInput.Items(root["vehicles"]).FirstOrDefault()?["start"]?["earliestStartTime"]);
        var date=dateText is not null ? DateOnly.ParseExact(dateText[..10],"yyyy-MM-dd",CultureInfo.InvariantCulture)
            : DateOnly.FromDateTime((RoutingInput.Items(root["orders"]?["deliveries"]).Select(o=>RoutingInput.Describe(root,RoutingInput.Text(o["id"])!))
                .SelectMany(a=>a?.Windows ?? []).Select(w=>w.EarliestStart).FirstOrDefault(d=>d is not null)
                ?? throw new FormatException("Choose a planning date for these orders.")).DateTime);
        var eligible=Eligible(catalog,terminal);
        if(eligible.Length==0)throw new FormatException($"No available trucks are saved for {terminal}. Add or enable trucks in Fleet Management first.");
        var vehicles=new JsonArray();
        foreach(var unit in eligible)
        {
            unit.Validate();
            var end=string.CompareOrdinal(unit.ShiftEnd,unit.ShiftStart)<0 ? date.AddDays(1):date;
            vehicles.Add(EquipmentPlanning.Vehicle(catalog.Types.Single(t=>t.Code==unit.TypeCode),unit.Id,depot,
                $"{date:yyyy-MM-dd}T{unit.ShiftStart}:00{unit.UtcOffset}",$"{end:yyyy-MM-dd}T{unit.ShiftEnd}:00{unit.UtcOffset}"));
        }
        root["vehicles"]=vehicles;
        root.Remove("routes");
        root["reporting"] ??= new JsonObject();
        root["reporting"]!["vehicles"]=new JsonObject();
        root["reporting"]!.AsObject().Remove("unavailableVehicles");
        foreach(var unit in eligible)root["reporting"]!["vehicles"]![unit.Id]=new JsonObject { ["equipmentType"]=unit.TypeCode };
        return root.ToJsonString();
    }
    // The browser may choose availability and daily shifts, but cannot invent vehicles or change saved capacities.
    public static string ApplySavedFleet(string json, EquipmentCatalogDto catalog, string terminal)
    {
        var root=RoutingInput.Parse(json);
        var eligible=Eligible(catalog,terminal).ToDictionary(u=>u.Id,StringComparer.Ordinal);
        var trucks=RoutingInput.Items(root["vehicles"]).ToArray();
        if(trucks.Length==0 || trucks.Select(t=>RoutingInput.Text(t["id"])).Distinct().Count()!=trucks.Length)
            throw new FormatException("Select at least one distinct saved truck.");
        var locations=RoutingInput.Items(root["locations"]).Select(l=>RoutingInput.Text(l["id"])).ToHashSet();
        var rebuilt=new JsonArray();
        foreach(var truck in trucks)
        {
            var id=RoutingInput.Text(truck["id"]) ?? "";
            if(!eligible.TryGetValue(id,out var unit))throw new FormatException($"Truck {id} is no longer available in your terminal's fleet. Start a new selection from Orders.");
            var depot=RoutingInput.Text(truck["start"]?["locationId"]) ?? "";
            var destination=RoutingInput.Text(truck["end"]?["locationId"]) ?? depot;
            if(!locations.Contains(depot) || !locations.Contains(destination))throw new FormatException("Choose valid start and return locations.");
            var replacement=EquipmentPlanning.Vehicle(catalog.Types.Single(t=>t.Code==unit.TypeCode),id,depot,
                RoutingInput.Text(truck["start"]?["earliestStartTime"]) ?? "",RoutingInput.Text(truck["end"]?["latestEndTime"]) ?? "");
            replacement["end"]!["locationId"]=destination;
            rebuilt.Add(replacement);
        }
        root["vehicles"]=rebuilt;
        return root.ToJsonString();
    }
}
