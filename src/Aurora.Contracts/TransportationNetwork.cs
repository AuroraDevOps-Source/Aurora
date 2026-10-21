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
    // Nodes where freight is planned from; the Optimization "active node".
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
    public static string TransferTo(TerminalDto destination) => destination.IsConsolidation ? Consolidation : LineHaul;
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
        if (order.UseConsigneeAddress)
        {
            if (!string.IsNullOrWhiteSpace(order.ReceivingTerminal))
            {
                var receiving = nodes.SingleOrDefault(n => n.NodeType == NodeVocabulary.Terminal && n.Code.Equals(order.ReceivingTerminal, StringComparison.OrdinalIgnoreCase))
                    ?? throw new FormatException($"Order {order.Id}'s receiving terminal is no longer available. Edit the order's Line Haul setting.");
                if (receiving.Id == active.Id) throw new FormatException($"Order {order.Id} needs a receiving terminal different from its starting terminal.");
                return new(order.Id, PlanningBatch.TransferTo(receiving), receiving.Id, $"Line haul to selected terminal {receiving.Code}");
            }
            return new(order.Id, PlanningBatch.General, null, "Deliver to consignee address");
        }
        var activeArea = ServiceArea.Parse(active.ServiceArea);
        if (order.DeliverToNodeId is { } requested && !nodes.Any(n => n.Id == requested))
            throw new FormatException($"Order {order.Id} has a receiving location that is no longer available. Update its destination before planning.");
        if (order.DeliverToNodeId == active.Id)
            return new(order.Id, PlanningBatch.General, null, $"At final terminal {active.Code}");
        // An explicit deliver-to node (agent, airport, rail yard...) replaces the consignee as the destination.
        if (order.DeliverToNodeId is { } nodeId && nodeId != active.Id && nodes.FirstOrDefault(n => n.Id == nodeId) is { } deliverTo)
        {
            if (deliverTo.NodeType != NodeVocabulary.Terminal && (activeArea.IsEmpty || activeArea.Contains(deliverTo.PostalCode)))
                return new(order.Id, PlanningBatch.General, deliverTo.Id, $"Deliver to {deliverTo.Code}");
            var target = NextTerminal(deliverTo, active, nodes);
            return new(order.Id, PlanningBatch.TransferTo(target), target.Id, target.Id == deliverTo.Id ? $"Deliver to {deliverTo.Code}" : $"Deliver to {deliverTo.Code} via {target.Code}");
        }
        if (activeArea.IsEmpty || activeArea.Contains(order.PostalCode))
            return order.ServiceType == OrderServiceTypes.Consolidation
                ? new(order.Id, PlanningBatch.Consolidation, null, "Consolidation job")
                : new(order.Id, PlanningBatch.General, null, activeArea.IsEmpty ? $"{active.Code} has no service area" : $"In {active.Code} service area");
        var candidates = nodes.Where(n => n.Id != active.Id && n.NodeType == NodeVocabulary.Terminal && ServiceArea.Parse(n.ServiceArea).Contains(order.PostalCode)).ToArray();
        int Priority(TerminalDto n) => n.RoutingRole == NodeVocabulary.Spoke ? 0 : n.RoutingRole == NodeVocabulary.Hub ? 1 : 2;
        var preferred = candidates.Length == 0 ? [] : candidates.Where(n => Priority(n) == candidates.Min(Priority)).ToArray();
        if (preferred.Select(n => NextTerminal(n, active, nodes).Id).Distinct().Count() > 1)
            throw new FormatException($"Order {order.Id} matches more than one receiving terminal ({string.Join(", ", preferred.Select(n => n.Code))}). Set its destination or correct the overlapping service areas before planning.");
        var serving = preferred.OrderBy(n => n.Code, StringComparer.Ordinal).FirstOrDefault();
        // Freight outside every service area stays in general routing (workflow default: include it).
        if (serving is null) return new(order.Id, PlanningBatch.General, null, "Outside every service area");
        var destination = NextTerminal(serving, active, nodes);
        return new(order.Id, PlanningBatch.TransferTo(destination), destination.Id, destination.Id == serving.Id ? $"Served by {serving.Code}" : $"Served by {serving.Code} via {destination.Code}");
    }

    // A transfer plan has exactly one receiving terminal. Local jobs may share a plan.
    public static CreateOrderDraftDto[] GroupPlans(CreateOrderDraftDto input, IReadOnlyList<TmsOrder> orders,
        TerminalDto origin, IReadOnlyList<TerminalDto> nodes)
    {
        if (input.OrderIds is not { Length: > 0 } || input.OrderIds.Length > 5000 || input.OrderIds.Distinct().Count() != input.OrderIds.Length)
            throw new FormatException("Select between 1 and 5,000 distinct orders.");
        var selected = input.OrderIds.Select(id => orders.SingleOrDefault(o => o.Id == id && o.SourceId == input.SourceId)
            ?? throw new FormatException($"Order {id} is no longer available. Refresh Orders.")).ToArray();
        var assignments = selected.Select(o => Classify(o, origin, nodes)).ToArray();
        bool Transfer(BatchAssignment a) => a.DestinationNodeId is not null && a.Batch is PlanningBatch.LineHaul or PlanningBatch.Consolidation;
        foreach (var assignment in assignments.Where(Transfer))
            if (!nodes.Any(n => n.Id == assignment.DestinationNodeId && n.Id != origin.Id && n.NodeType == NodeVocabulary.Terminal))
                throw new FormatException($"Order {assignment.OrderId} needs a receiving terminal. Review its destination and origin-to-destination routing rules.");
        return assignments.GroupBy(a => Transfer(a) ? a.DestinationNodeId : null)
            .OrderBy(g => g.Key is null ? "" : nodes.Single(n => n.Id == g.Key).Code, StringComparer.Ordinal)
            .Select(g => input with { OrderIds = g.Select(a => a.OrderId).ToArray(),
                Batch = g.Key is null ? null : PlanningBatch.TransferTo(nodes.Single(n => n.Id == g.Key)), ToTerminalId = g.Key }).ToArray();
    }

    private static TerminalDto NextTerminal(TerminalDto destination, TerminalDto origin, IReadOnlyList<TerminalDto> nodes)
    {
        TerminalRoutingRules.Validate(origin, nodes);
        return TerminalRoutingRules.Next(origin, destination, nodes);
    }

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
