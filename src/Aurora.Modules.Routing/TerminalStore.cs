using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class TerminalStore(NpgsqlConnectionFactory factory, ITenantContext tenant)
{
    public async Task<List<TerminalDto>> List(CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        // Node attributes live in details; the columns win so codes and addresses stay authoritative.
        var rows = await db.QueryAsync<string>(new CommandDefinition("SELECT (details || jsonb_build_object('Id',id,'Code',code,'Name',name,'Address',address,'City',city,'State',state,'PostalCode',postal_code,'Country',country,'Revision',revision))::text FROM aurora_terminal WHERE tenant_id=@TenantId AND deleted_at IS NULL ORDER BY code", new { tenant.TenantId }, cancellationToken: ct));
        return rows.Select(r => JsonSerializer.Deserialize<TerminalDto>(r)!).ToList();
    }
    public async Task Save(TerminalDto item, bool create, CancellationToken ct)
    {
        if (create) item.Id = Guid.NewGuid();
        item.Validate();
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new {key=$"terminal-rules:{tenant.TenantId}"},tx,cancellationToken:ct));
        if (item.RoutingRole == NodeVocabulary.Hub && item.Region.Trim().Length > 0 && await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT code FROM aurora_terminal WHERE tenant_id=@TenantId AND id<>@Id AND deleted_at IS NULL AND details->>'RoutingRole'='Hub' AND lower(trim(details->>'Region'))=lower(trim(@Region)) LIMIT 1", new { tenant.TenantId, item.Id, item.Region }, tx, cancellationToken: ct)) is { } hub)
            throw new FormatException($"Region {item.Region.Trim()} already has {hub} as its hub. A region has one hub.");
        TerminalRoutingRules.Validate(item,await List(ct));
        item.RouteViaNodeId=null;
        var details = JsonSerializer.Serialize(item);
        if (create)
        {
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_terminal(tenant_id,id,code,name,address,city,state,postal_code,country,details) VALUES(@TenantId,@Id,@Code,@Name,@Address,@City,@State,@PostalCode,@Country,CAST(@details AS jsonb))", new { tenant.TenantId, item.Id, item.Code, item.Name, item.Address, item.City, item.State, item.PostalCode, item.Country, details }, tx, cancellationToken: ct));
        }
        else
        {
            var count = await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_terminal SET name=@Name,address=@Address,city=@City,state=@State,postal_code=@PostalCode,country=@Country,details=CAST(@details AS jsonb),revision=revision+1 WHERE tenant_id=@TenantId AND id=@Id AND code=@Code AND revision=@Revision AND deleted_at IS NULL", new { tenant.TenantId, item.Id, item.Code, item.Name, item.Address, item.City, item.State, item.PostalCode, item.Country, details, item.Revision }, tx, cancellationToken: ct));
            if (count != 1) throw new FormatException("Node changed or no longer exists. Refresh and try again. Node codes cannot be changed.");
        }
        await tx.CommitAsync(ct);
    }
    public async Task Delete(Guid id, int revision, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new {key=$"terminal-rules:{tenant.TenantId}"},tx,cancellationToken:ct));
        var code = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT code FROM aurora_terminal WHERE tenant_id=@TenantId AND id=@id AND revision=@revision AND deleted_at IS NULL FOR UPDATE", new { tenant.TenantId, id, revision }, tx, cancellationToken: ct));
        if (code is null) throw new FormatException("Terminal changed or no longer exists. Refresh and try again.");
        var used = await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_order WHERE tenant_id=@TenantId AND terminal_id=@id AND deleted_at IS NULL)
            OR EXISTS(SELECT 1 FROM routing_equipment_unit WHERE tenant_id=@TenantId AND lower(trim(data->>'Terminal'))=lower(@code))
            OR EXISTS(SELECT 1 FROM aurora_order WHERE tenant_id=@TenantId AND deleted_at IS NULL AND (details->>'DeliverToNodeId'=@id::text OR details->>'PickupNodeId'=@id::text))
            OR EXISTS(SELECT 1 FROM aurora_terminal WHERE tenant_id=@TenantId AND deleted_at IS NULL AND EXISTS(SELECT 1 FROM jsonb_array_elements(COALESCE(details->'OutboundRoutes','[]'::jsonb)) rule WHERE rule->>'DestinationTerminalId'=@id::text OR rule->>'ViaTerminalId'=@id::text))
            """, new { tenant.TenantId, id, code }, tx, cancellationToken: ct));
        if (used) throw new FormatException("This node is used by orders, fleet equipment, or terminal routing rules. Reassign them before deleting it.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_terminal SET deleted_at=now(),revision=revision+1 WHERE tenant_id=@TenantId AND id=@id", new { tenant.TenantId, id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
}
