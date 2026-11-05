namespace Aurora.Integrations.Nova;

internal static class NativeSnapshotSql
{
    internal const string Upsert = """
                    INSERT INTO aurora_order AS o(tenant_id,source_id,id,scheduled_at,customer,city,status,terminal_id,details)
                    VALUES(@TenantId,@source,@Id,@time,@Customer,@City,'ReadyToRoute',NULL,CAST(@json AS jsonb))
                    ON CONFLICT(tenant_id,source_id,id) DO UPDATE SET scheduled_at=excluded.scheduled_at,
                        customer=excluded.customer,city=excluded.city,terminal_id=NULL,details=excluded.details,deleted_at=NULL,
                        revision=o.revision+1,status='ReadyToRoute'
                    WHERE NOT EXISTS(SELECT 1 FROM aurora_planning_draft d
                        WHERE d.tenant_id=o.tenant_id AND d.source_id=o.source_id AND o.id=ANY(d.order_ids)
                        AND d.finished_session IS NULL AND d.cancelled_at IS NULL)
                    """;

    internal const string ReleaseAssignment = """
                    DELETE FROM aurora_manifest_order a USING aurora_order o
                    WHERE (a.tenant_id,a.source_id,a.order_id)=(o.tenant_id,o.source_id,o.id)
                        AND o.tenant_id=@TenantId AND o.source_id=@source AND o.id=@id AND o.status='ReadyToRoute'
                        AND NOT EXISTS(SELECT 1 FROM aurora_planning_draft d WHERE d.tenant_id=o.tenant_id
                            AND d.source_id=o.source_id AND o.id=ANY(d.order_ids)
                            AND d.finished_session IS NULL AND d.cancelled_at IS NULL)
                    """;

    internal const string Withdraw = """
            UPDATE aurora_order o SET deleted_at=now(),revision=o.revision+1 FROM aurora_order_source s
            WHERE (s.tenant_id,s.id)=(o.tenant_id,o.source_id) AND o.tenant_id=@TenantId
                AND s.request#>>'{reporting,firebirdSource}'=@SourceKey AND o.status='ReadyToRoute'
                AND o.deleted_at IS NULL AND NOT((o.id || '@' || o.source_id::text)=ANY(@keys))
                AND NOT EXISTS(SELECT 1 FROM aurora_planning_draft d WHERE d.tenant_id=o.tenant_id
                    AND d.source_id=o.source_id AND o.id=ANY(d.order_ids) AND d.finished_session IS NULL AND d.cancelled_at IS NULL)
            """;
}
