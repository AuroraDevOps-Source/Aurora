namespace Aurora.Contracts;

// Stored on the origin terminal. A null intermediate terminal means a direct transfer.
public sealed class TerminalRoutingRule
{
    public Guid DestinationTerminalId { get; set; }
    public Guid? ViaTerminalId { get; set; }
}

public static class TerminalRoutingRules
{
    public static TerminalDto Next(TerminalDto origin, TerminalDto destination, IReadOnlyList<TerminalDto> nodes)
    {
        var rules = origin.OutboundRoutes.Where(r => r.DestinationTerminalId == destination.Id).ToArray();
        if (rules.Length > 1) throw new FormatException($"{origin.Code} has duplicate routing rules for {destination.Code}.");
        var id = rules.SingleOrDefault()?.ViaTerminalId ?? destination.Id;
        return nodes.SingleOrDefault(n => n.Id == id)
            ?? throw new FormatException($"A terminal in the routing rule from {origin.Code} to {destination.Code} is no longer available.");
    }

    public static void Validate(TerminalDto edited, IReadOnlyList<TerminalDto> nodes)
    {
        var network = nodes.Where(n => n.Id != edited.Id).Append(edited).ToArray();
        foreach (var origin in network)
        {
            if (origin.OutboundRoutes.Count > 1000) throw new FormatException("A terminal allows up to 1,000 routing rules.");
            if (origin.OutboundRoutes.Select(r => r.DestinationTerminalId).Distinct().Count() != origin.OutboundRoutes.Count)
                throw new FormatException($"{origin.Code} has more than one rule for the same destination.");
            foreach (var rule in origin.OutboundRoutes)
            {
                if (!network.Any(n => n.Id == rule.DestinationTerminalId && n.Id != origin.Id && n.NodeType == NodeVocabulary.Terminal))
                    throw new FormatException($"Choose another saved destination terminal for {origin.Code}.");
                if (rule.ViaTerminalId is { } via && !network.Any(n => n.Id == via && n.Id != origin.Id && n.Id != rule.DestinationTerminalId && n.NodeType == NodeVocabulary.Terminal))
                    throw new FormatException($"Choose an intermediate terminal different from the origin and destination for {origin.Code}, or choose Direct.");
                var destination = network.Single(n => n.Id == rule.DestinationTerminalId);
                var seen = new HashSet<Guid>();
                var current = origin;
                while (current.Id != destination.Id)
                {
                    if (!seen.Add(current.Id)) throw new FormatException($"Routing rules create a loop on the way to {destination.Code}.");
                    current = Next(current, destination, network);
                }
            }
        }
    }
}
