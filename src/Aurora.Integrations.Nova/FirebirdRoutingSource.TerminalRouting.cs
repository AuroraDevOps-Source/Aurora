using Aurora.Contracts;
using Dapper;
using System.Text.Json;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    private sealed record TerminalRoutingRow(string Code, string Data, int Revision);
    private async Task ApplyTerminalRouting(TerminalDto[] nodes, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        var rows = await db.QueryAsync<TerminalRoutingRow>(new CommandDefinition(
            "SELECT terminal_code AS Code,data::text AS Data,revision FROM aurora_terminal_routing WHERE tenant_id=@TenantId AND source_key=@SourceKey",
            new { tenant.TenantId, Options.SourceKey }, cancellationToken:ct));
        foreach (var row in rows)
        {
            var node = nodes.SingleOrDefault(n => n.Code == row.Code);
            if (node is null) continue;
            var settings = JsonSerializer.Deserialize<TerminalDto>(row.Data)!;
            node.IsConsolidation=settings.IsConsolidation; node.ServiceArea=settings.ServiceArea;
            node.RoutingRole=settings.RoutingRole; node.RouteViaNodeId=settings.RouteViaNodeId; node.OutboundRoutes=settings.OutboundRoutes ?? []; node.Revision=row.Revision;
        }
    }

    public async Task<TerminalDto> SaveTerminalRouting(string code, TerminalDto input, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await Lock(db,tx,ct);
        var nodes = await ReadTerminals(ct);
        var node = nodes.SingleOrDefault(n => n.Code == code) ?? throw new KeyNotFoundException("Terminal no longer exists.");
        node.IsConsolidation=input.IsConsolidation; node.ServiceArea=input.ServiceArea;
        node.RoutingRole=input.RoutingRole; node.RouteViaNodeId=null; node.OutboundRoutes=input.OutboundRoutes ?? [];
        node.Validate();
        TerminalRoutingRules.Validate(node,nodes);
        var data = JsonSerializer.Serialize(new { node.IsConsolidation,node.ServiceArea,node.RoutingRole,node.OutboundRoutes });
        var changed=await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_terminal_routing(tenant_id,source_key,terminal_code,data,revision)
            SELECT @TenantId,@SourceKey,@code,CAST(@data AS jsonb),1 WHERE @Revision=0
            ON CONFLICT(tenant_id,source_key,terminal_code) DO NOTHING
            """,new {tenant.TenantId,Options.SourceKey,code,data,input.Revision},tx,cancellationToken:ct));
        if (input.Revision>0)
            changed=await db.ExecuteAsync(new CommandDefinition("""
                UPDATE aurora_terminal_routing SET data=CAST(@data AS jsonb),revision=revision+1
                WHERE tenant_id=@TenantId AND source_key=@SourceKey AND terminal_code=@code AND revision=@Revision
                """,new {tenant.TenantId,Options.SourceKey,code,data,input.Revision},tx,cancellationToken:ct));
        if(changed!=1)throw new FormatException("Terminal routing changed in another window. Close and reopen it before saving.");
        await tx.CommitAsync(ct);
        node.Revision=input.Revision+1;
        return node;
    }
}
