using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Contracts;

public sealed record ManifestPlanningLockEdit(string Revision, bool Locked);
public sealed record ReplanManifest(long Id, string Revision, string[] OrderIds, DateOnly Date)
{
    public Guid? LocalId {get;init;}
}

public static class ManifestReplanning
{
    public static ReplanManifest[] Included(string json) => RoutingInput.Parse(json)["reporting"]?["replanManifests"]?.Deserialize<ReplanManifest[]>() ?? [];
    public static void ValidateResult(string saved, IEnumerable<RouteSummaryDto> routes)
    {
        var actual=routes.ToArray();
        foreach(var manifest in Included(saved))
        foreach(var id in manifest.OrderIds)
        {
            var assigned=actual.Where(r=>r.Pros.Any(p=>p.ProNumber==id)).ToArray();
            if(assigned.Length!=1)throw new FormatException($"Existing manifest order {id} must remain scheduled. Adjust the plan before saving; existing manifests have not changed.");

        }
    }
    public static string Protect(string saved,string request)
    {
        var included=Included(saved);
        if(included.Length==0)return request;
        var root=RoutingInput.Parse(request);
        root["reporting"]??=new JsonObject();
        root["reporting"]!["replanManifests"]=JsonSerializer.SerializeToNode(included);
        var ids=RoutingInput.Orders(root).Select(o=>RoutingInput.Text(o["id"])).ToHashSet();
        foreach(var manifest in included)
        foreach(var id in manifest.OrderIds)
        {
            if(!ids.Contains(id) || PlanOrderDecisions.Get(root,id) is PlanOrderDecisions.Excluded or PlanOrderDecisions.LeftOff)
                throw new FormatException($"Order {id} belongs to an existing manifest and cannot be excluded from this plan.");
        }
        foreach(var vehicle in RoutingInput.Items(root["vehicles"]))
            if(RoutingInput.Text(vehicle["start"]?["earliestStartTime"]) is not {} start || !DateOnly.TryParse(start[..10],out var day) || included.Any(m=>m.Date!=day))
                throw new FormatException("Keep the planning date for included manifests. Start a new plan to use another date.");
        return root.ToJsonString();
    }
}
