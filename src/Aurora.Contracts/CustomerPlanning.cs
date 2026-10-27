using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public sealed class CustomerPlanning
{
    public int Revision { get; set; }
    public bool Consolidation { get; set; }
    public string EquipmentType { get; set; } = "";
    public decimal? MaximumWeight { get; set; }
    public decimal? MaximumCubes { get; set; }
    public bool KeepDeliveriesTogether { get; set; }
    public decimal? EstimatedCubesPerPiece { get; set; }
    public double? SetupMinutes { get; set; }
    public double? MinutesPerPiece { get; set; }
    public double? MinutesPerCube { get; set; }
    public void Validate()
    {
        if (new[]{MaximumWeight,MaximumCubes,EstimatedCubesPerPiece}.Any(v=>v is <=0))
            throw new FormatException("Capacities and cube estimates must be greater than zero when provided.");
        if (new[]{SetupMinutes,MinutesPerPiece,MinutesPerCube}.Any(v=>v is {} n && (!double.IsFinite(n)||n<0||n>1440)))
            throw new FormatException("Service times must be between zero and 1,440 minutes.");
        if(Consolidation && (string.IsNullOrWhiteSpace(EquipmentType)||MaximumWeight is null||MaximumCubes is null))
            throw new FormatException("Choose consolidation equipment with weight and cubic capacities.");
    }
    public decimal? PlanningCubes(decimal? actual,decimal? pieces) => actual is >0 ? actual : EstimatedCubesPerPiece is {} estimate && pieces is >0 ? estimate*pieces : null;
}

public static class CustomerPlanningRequest
{
    // Run after appointment preparation: group only locations whose complete visit
    // restrictions match, preserving separate tasks and original order identities.
    public static string GroupLocations(string json)
    {
        var root=RoutingInput.Parse(json);
        var locations=RoutingInput.Items(root["locations"]).ToDictionary(l=>RoutingInput.Text(l["id"])!);
        var groups=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
        foreach(var order in RoutingInput.Orders(root))
        {
            var id=RoutingInput.Text(order["id"])!;
            var report=RoutingInput.ReportOrder(root,id);
            if(RoutingInput.Text(report?["destinationKey"]) is not {} key)continue;
            var task=RoutingInput.CustomerTask(order)!;
            if(!locations.TryGetValue(RoutingInput.Text(task["locationId"])!,out var location))continue;
            var signature=(JsonObject)location.DeepClone();signature.Remove("id");
            // Task-specific slot references are meaningful only within their location.
            var groupKey=key+":"+signature.ToJsonString()+":"+task["timeSlotIds"]?.ToJsonString();
            if(!groups.TryGetValue(groupKey,out var shared)){groups[groupKey]=location;shared=location;}
            task["locationId"]=RoutingInput.Text(shared["id"]);
            if(report?["keepTogether"]?.GetValue<bool>()==true)
            {
                shared["stopProperties"]??=new JsonObject();
                shared["stopProperties"]!["taskAggregation"]="REQUIRED";
            }
        }
        PlannerEdits.RemoveUnusedLocations(root);
        return root.ToJsonString();
    }
}
