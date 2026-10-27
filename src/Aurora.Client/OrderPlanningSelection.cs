using Aurora.Contracts;

namespace Aurora.Client;

// Carries the exact selection across client-side navigation, using source + order IDs.
public sealed class OrderPlanningSelection
{
    public sealed record Selection(Guid TerminalId, TmsOrderKey[] Orders);
    private Selection? _pending;

    public void Set(IEnumerable<TmsOrder> orders)
    {
        var selected = orders.ToArray();
        if (selected.Length == 0) throw new FormatException("Select orders to optimize.");
        if (selected.Any(o => o.Status != WorkspaceOrderStatus.Ready || o.ManifestId is not null || o.OptimizationDraftId is not null))
            throw new FormatException("Select ready orders that are not already assigned to a manifest or an open plan.");
        var terminals = selected.Select(o => o.TerminalId).Distinct().ToArray();
        if (terminals.Length != 1 || terminals[0] is null)
            throw new FormatException("Select orders assigned to one terminal to optimize routes.");
        if (selected.Select(o => o.SourceId).Distinct().Count() != 1)
            throw new FormatException("Select orders from one planning source.");
        _pending = new(terminals[0]!.Value, selected.Select(o => new TmsOrderKey(o.SourceId, o.Id)).Distinct().ToArray());
    }

    public Selection? Take()
    {
        var selection = _pending;
        _pending = null;
        return selection;
    }
}
