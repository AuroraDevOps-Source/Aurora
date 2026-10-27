using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

public static class WorkspaceOrderSource
{
    public static async Task<Guid> Get(NpgsqlConnection db,Guid tenantId,string? terminal,NpgsqlTransaction? tx,CancellationToken ct)
    {
        // Keep newly synchronized orders in the same planning source as adopted
        // orders at that terminal, so a planner can select both in one route.
        var existing=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT s.id FROM aurora_order_source s JOIN aurora_sync_connection c ON c.tenant_id=s.tenant_id
            WHERE s.tenant_id=@tenantId AND s.request#>>'{reporting,firebirdSource}'=c.source_identity
              AND EXISTS(SELECT 1 FROM aurora_order o WHERE o.tenant_id=s.tenant_id AND o.source_id=s.id
                AND o.details->>'TerminalCode'=@terminal)
            ORDER BY s.id LIMIT 1
            """,new{tenantId,terminal},tx,cancellationToken:ct));
        return existing??await db.QuerySingleAsync<Guid>(new CommandDefinition("INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES(@tenantId,gen_random_uuid(),'Synchronized orders','{}') ON CONFLICT(tenant_id,name) DO UPDATE SET name=excluded.name RETURNING id",new{tenantId},tx,cancellationToken:ct));
    }
}
