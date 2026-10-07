using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aurora.Contracts;

// Vocabulary from the Nova 1.5 planning workflow (sections 1.0-2.0).
public static class NodeVocabulary
{
    public const string Terminal = "Terminal";
    public static readonly string[] Types = [Terminal, "Agent Facility", "Interline Partner", "Rail Yard", "Airport", "Seaport", "Container Yard", "Drop Yard", "Other"];
    public static readonly string[] Functions = ["Cross-Dock", "Transload Facility", "Warehouse / Distribution Center"];
    public static readonly string[] Services = ["Container Freight Station", "Bonded Warehouse", "Blanket Wrap Services"];
    public const string Hub = "Hub", Spoke = "Spoke", AdHoc = "Ad Hoc";
    public static readonly string[] Roles = [Hub, Spoke, AdHoc];
    // Nodes where freight is planned from; the Route Optimization "active node".
    public static bool Plannable(TerminalDto node) => node.NodeType is Terminal or "Agent Facility";
}

public static class OrderServiceTypes
{
    public const string Consolidation = "Consolidation";
    public static readonly string[] All = [Consolidation, "Dock or Doorstep Delivery", "White Glove Inside Delivery", "Threshold Inside Delivery", "Hazmat", "Temperature Controlled"];
}

// A node's direct service area: postal codes, prefixes ("917"), or numeric ranges ("91700-91799"),
// separated by commas, semicolons, or new lines. Structured like FreightOps lanes.
public sealed partial class ServiceArea
{
    private readonly string[] _prefixes;
    private readonly (string From, string To)[] _ranges;
    private ServiceArea(string[] prefixes, (string, string)[] ranges) { _prefixes = prefixes; _ranges = ranges; }

    public bool IsEmpty => _prefixes.Length == 0 && _ranges.Length == 0;

    public static ServiceArea Parse(string? text)
    {
        var prefixes = new List<string>();
        var ranges = new List<(string, string)>();
        foreach (var raw in (text ?? "").Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var entry = Normalize(raw);
            var parts = entry.Split('-');
            if (parts.Length == 2 && Digits().IsMatch(parts[0]) && parts[0].Length == parts[1].Length && Digits().IsMatch(parts[1]) && string.CompareOrdinal(parts[0], parts[1]) <= 0)
                ranges.Add((parts[0], parts[1]));
            else if (parts.Length == 1 && Postal().IsMatch(entry))
                prefixes.Add(entry);
            else
                throw new FormatException($"Service area entry \"{raw}\" is not a postal code, prefix, or range such as 91700-91799.");
        }
        return new(prefixes.Distinct().ToArray(), ranges.ToArray());
    }

    public bool Contains(string? postalCode)
    {
        var code = Normalize(postalCode ?? "");
        if (code.Length == 0) return false;
        return _prefixes.Any(p => code.StartsWith(p, StringComparison.Ordinal))
            || _ranges.Any(r => code.Length >= r.From.Length && Digits().IsMatch(code[..r.From.Length])
                && string.CompareOrdinal(code[..r.From.Length], r.From) >= 0 && string.CompareOrdinal(code[..r.From.Length], r.To) <= 0);
    }

    private static string Normalize(string value) => value.Replace(" ", "").ToUpperInvariant();
    [GeneratedRegex("^[0-9]+$")] private static partial Regex Digits();
    [GeneratedRegex("^[0-9A-Z]{1,10}$")] private static partial Regex Postal();
}

public static class PlanningBatch
{
    public const string General = "General", LineHaul = "LineHaul", Consolidation = "Consolidation";
    // A plan started with the batch filter on "All batches" can contain several batches.
    public const string Mixed = "Mixed";
    public static readonly string[] All = [General, LineHaul, Consolidation];
    public static string Label(string batch) => batch switch { LineHaul => "Line haul", Consolidation => "Consolidation", Mixed => "All batches", _ => "General routing" };
    // The batch a plan is named for: the requested one, else the single batch its orders share, else Mixed.
    public static string For(string? requested, IEnumerable<BatchAssignment> assignments)
    {
        if (requested is not null) return requested;
        var batches = assignments.Select(a => a.Batch).Distinct().ToArray();
        return batches.Length == 1 ? batches[0] : Mixed;
    }
}

// DestinationNodeId is set when the optimizer should route to a node instead of the consignee address.
public sealed record BatchAssignment(string OrderId, string Batch, Guid? DestinationNodeId, string Reason);

// Pre-optimization categories (workflow section 3.2, "Assign Pre-Optimization Shipment Categories").
public static class PlanningBatches
{
    public static BatchAssignment Classify(TmsOrder order, TerminalDto active, IReadOnlyList<TerminalDto> nodes)
    {
        var activeArea = ServiceArea.Parse(active.ServiceArea);
        // An explicit deliver-to node (agent, airport, rail yard...) replaces the consignee as the destination.
        if (order.DeliverToNodeId is { } nodeId && nodeId != active.Id && nodes.FirstOrDefault(n => n.Id == nodeId) is { } deliverTo)
        {
            if (activeArea.IsEmpty || activeArea.Contains(deliverTo.PostalCode))
                return new(order.Id, PlanningBatch.General, deliverTo.Id, $"Deliver to {deliverTo.Code}");
            var target = SameRegion(deliverTo, active) ? deliverTo : RouteVia(deliverTo, nodes);
            return new(order.Id, PlanningBatch.LineHaul, target.Id, target.Id == deliverTo.Id ? $"Deliver to {deliverTo.Code}" : $"Deliver to {deliverTo.Code} via {target.Code}");
        }
        if (activeArea.IsEmpty || activeArea.Contains(order.PostalCode))
            return order.ServiceType == OrderServiceTypes.Consolidation
                ? new(order.Id, PlanningBatch.Consolidation, null, "Consolidation job")
                : new(order.Id, PlanningBatch.General, null, activeArea.IsEmpty ? $"{active.Code} has no service area" : $"In {active.Code} service area");
        var serving = nodes.Where(n => n.Id != active.Id && ServiceArea.Parse(n.ServiceArea).Contains(order.PostalCode))
            .OrderBy(n => n.RoutingRole == NodeVocabulary.Spoke ? 0 : n.RoutingRole == NodeVocabulary.Hub ? 1 : 2).ThenBy(n => n.Code, StringComparer.Ordinal).FirstOrDefault();
        // Freight outside every service area stays in general routing (workflow default: include it).
        if (serving is null) return new(order.Id, PlanningBatch.General, null, "Outside every service area");
        var destination = SameRegion(serving, active) ? serving : RouteVia(serving, nodes);
        return new(order.Id, PlanningBatch.LineHaul, destination.Id, destination.Id == serving.Id ? $"Served by {serving.Code}" : $"Served by {serving.Code} via {destination.Code}");
    }

    // Unset regions are treated as one network so small companies can skip region setup.
    private static bool SameRegion(TerminalDto a, TerminalDto b) =>
        string.IsNullOrWhiteSpace(a.Region) || string.IsNullOrWhiteSpace(b.Region) || string.Equals(a.Region.Trim(), b.Region.Trim(), StringComparison.OrdinalIgnoreCase);

    // Out-of-region freight goes to the node's Route Via node, else its region's Hub, else the node itself.
    private static TerminalDto RouteVia(TerminalDto node, IReadOnlyList<TerminalDto> nodes) =>
        (node.RouteViaNodeId is { } via ? nodes.FirstOrDefault(n => n.Id == via) : null)
        ?? nodes.FirstOrDefault(n => n.RoutingRole == NodeVocabulary.Hub && !string.IsNullOrWhiteSpace(node.Region) && string.Equals(n.Region?.Trim(), node.Region.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? node;

    // Points each redirected order's delivery at its destination node, so the optimizer routes to the node.
    public static string ApplyDestinations(string json, IReadOnlyDictionary<string, TerminalDto> destinations)
    {
        if (destinations.Count == 0) return json;
        var root = RoutingInput.Parse(json);
        if (root["locations"] is not JsonArray locations) throw new FormatException("This planning source has no locations to route to.");
        var deliveries = RoutingInput.Items(root["orders"]?["deliveries"]).ToDictionary(d => RoutingInput.Text(d["id"]) ?? "", StringComparer.Ordinal);
        foreach (var (orderId, node) in destinations)
        {
            if (node.Latitude is not { } latitude || node.Longitude is not { } longitude)
                throw new FormatException($"Node {node.Code} needs latitude and longitude before orders can be routed to it.");
            if (!deliveries.TryGetValue(orderId, out var delivery) || delivery["delivery"] is not JsonObject stop)
                throw new FormatException($"Order {orderId} is not a delivery in this plan.");
            var locationId = "NODE-" + node.Code.ToUpperInvariant();
            if (!RoutingInput.Items(locations).Any(l => RoutingInput.Text(l["id"]) == locationId))
                locations.Add(new JsonObject { ["id"] = locationId, ["latitude"] = latitude, ["longitude"] = longitude });
            stop["locationId"] = locationId;
            if (root["reporting"]?["orders"]?[orderId] is JsonObject report)
            {
                report["consigneeName"] ??= report["shipToName"]?.DeepClone();
                report["shipToName"] = $"{node.Name} ({node.Code})";
                report["address1"] = node.Address; report["city"] = node.City; report["state"] = node.State; report["postalCode"] = node.PostalCode;
                report["deliverToNode"] = node.Code;
            }
        }
        PlannerEdits.RemoveUnusedLocations(root);
        return root.ToJsonString();
    }
}
