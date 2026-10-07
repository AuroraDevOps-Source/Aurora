namespace Aurora.Modules.Routing;

// "Optimization in Process": an order is locked while an open plan contains it. Plans that are never
// finished or cancelled stop locking after 12 hours so abandoned browser sessions cannot strand freight.
internal static class PlanningLock
{
    public const string Open = "d.finished_session IS NULL AND d.cancelled_at IS NULL AND d.created_at > now() - interval '12 hours'";
    // Correlated to an aurora_order row aliased "o".
    public const string DraftForOrder = "(SELECT d.id FROM aurora_planning_draft d WHERE d.tenant_id=o.tenant_id AND d.source_id=o.source_id AND o.id=ANY(d.order_ids) AND " + Open + " ORDER BY d.created_at DESC LIMIT 1)";
}
