namespace Aurora.Contracts;
public static class PickupPlanning
{
    public static void ValidateReturn(string json,IEnumerable<RouteSummaryDto> routes)
    {
        var root=RoutingInput.Parse(json);
        var ids=RoutingInput.Items(root["orders"]?["pickups"]).Select(o=>RoutingInput.Text(o["id"])!).ToHashSet();
        if(ids.Count==0)return;
        var depot=RoutingInput.Items(root["depots"]).SingleOrDefault();
        var location=RoutingInput.Text(depot?["locationId"]);
        if(location is null || routes.Where(r=>r.Pros.Any(p=>ids.Contains(p.ProNumber))).Any(r=>r.RouteStops.LastOrDefault()?.LocationId!=location))
            throw new FormatException("Pickup routes must return to the selected terminal. Re-optimize before creating manifests.");
    }
}
