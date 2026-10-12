using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public static class LineHaulPlanning
{
    public static string Prepare(string json, TerminalDto origin, TerminalDto destination)
    {
        if(origin.Id==destination.Id || origin.Code.Equals(destination.Code,StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Choose different origin and receiving terminals.");
        if(destination.NodeType!=NodeVocabulary.Terminal)throw new FormatException("Choose a receiving terminal.");
        foreach(var node in new[]{origin,destination})
            if(node.Latitude is not (>= -90 and <= 90) || node.Longitude is not (>= -180 and <= 180) || (node.Latitude==0 && node.Longitude==0))
                throw new FormatException($"Set valid coordinates for terminal {node.Code} before planning.");
        var root=RoutingInput.Parse(json);
        var ids=RoutingInput.Items(root["orders"]?["deliveries"]).Select(o=>RoutingInput.Text(o["id"])!).ToArray();
        root=RoutingInput.Parse(PlanningBatches.ApplyDestinations(json,ids.ToDictionary(id=>id,_=>destination)));
        foreach(var order in RoutingInput.Items(root["orders"]?["deliveries"]))
        {
            order["delivery"]!.AsObject().Remove("timeSlotIds");
            var report=RoutingInput.ReportOrder(root,RoutingInput.Text(order["id"])!);
            // Consignee appointments belong to the final delivery, not this transfer leg.
            if(report is not null)
                foreach(var key in report.Select(p=>p.Key).Where(k=>k.Equals("appointment",StringComparison.OrdinalIgnoreCase) || k.Equals("appointmentDetail",StringComparison.OrdinalIgnoreCase)).ToArray())report.Remove(key);
        }
        root["reporting"]!["lineHaulTo"]=destination.Code;
        root["reporting"]!["lineHaulEndLocation"]="NODE-"+destination.Code.ToUpperInvariant();
        return root.ToJsonString();
    }

    public static string SetFleetDestination(string json)
    {
        var root=RoutingInput.Parse(json);
        var end=RoutingInput.Text(root["reporting"]?["lineHaulEndLocation"]);
        if(end is null)return json;
        foreach(var truck in RoutingInput.Items(root["vehicles"]))truck["end"]!["locationId"]=end;
        return root.ToJsonString();
    }

    // The terminal transfer is fixed by the saved draft, not by browser edits to the plan.
    public static string RestoreDestination(string savedJson, string request)
    {
        var saved = RoutingInput.Parse(savedJson);
        if (RoutingInput.Text(saved["reporting"]?["lineHaulTo"]) is null) return request;
        var root = RoutingInput.Parse(request);
        root["reporting"] ??= new JsonObject();
        foreach (var key in new[] { "terminal", "lineHaulTo", "lineHaulEndLocation" })
            root["reporting"]![key] = saved["reporting"]![key]!.DeepClone();
        var end = RoutingInput.Text(saved["reporting"]!["lineHaulEndLocation"])!;
        var savedLocations = RoutingInput.Items(saved["locations"]).ToDictionary(l => RoutingInput.Text(l["id"])!);
        var depot = RoutingInput.Text(RoutingInput.Items(saved["depots"]).FirstOrDefault()?["locationId"]) ?? "DEPOT";
        var locations = root["locations"]?.AsArray() ?? throw new FormatException("The plan has no locations.");
        foreach (var id in new[] { depot, end }.Distinct())
        {
            if (!savedLocations.TryGetValue(id, out var location)) throw new FormatException("The saved transfer has no terminal location. Start a new plan.");
            foreach (var existing in locations.OfType<JsonObject>().Where(l => RoutingInput.Text(l["id"]) == id).ToArray()) locations.Remove(existing);
            locations.Add(location.DeepClone());
        }
        foreach (var delivery in RoutingInput.Items(root["orders"]?["deliveries"]))
        {
            delivery["delivery"]!["locationId"] = end;
            delivery["delivery"]!.AsObject().Remove("timeSlotIds");
        }
        foreach (var truck in RoutingInput.Items(root["vehicles"])) truck["start"]!["locationId"] = depot;
        return SetFleetDestination(root.ToJsonString());
    }

    public static void ValidateResult(string json, IEnumerable<RouteSummaryDto> routes)
    {
        var root = RoutingInput.Parse(json);
        if (RoutingInput.Text(root["reporting"]?["lineHaulTo"]) is null) return;
        var end = RoutingInput.Text(root["reporting"]?["lineHaulEndLocation"]);
        if (end is null || routes.Any(r => r.RouteStops.LastOrDefault()?.LocationId != end ||
            r.RouteStops.Where(s => s.OrderIds.Count > 0).Any(s => s.LocationId != end)))
            throw new FormatException("The result does not route all freight to the receiving terminal. Optimize this line haul plan again before creating manifests.");
    }
}
