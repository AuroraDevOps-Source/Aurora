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
        return (await db.QueryAsync<TerminalDto>(new CommandDefinition("SELECT id,code,name,address,city,state,postal_code AS PostalCode,country,revision FROM aurora_terminal WHERE tenant_id=@TenantId AND deleted_at IS NULL ORDER BY code", new { tenant.TenantId }, cancellationToken: ct))).ToList();
    }
    public async Task Save(TerminalDto item, bool create, CancellationToken ct)
    {
        item.Validate();
        await using var db = await factory.OpenConnectionAsync(ct);
        if (create)
        {
            item.Id = Guid.NewGuid();
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_terminal(tenant_id,id,code,name,address,city,state,postal_code,country) VALUES(@TenantId,@Id,@Code,@Name,@Address,@City,@State,@PostalCode,@Country)", new { tenant.TenantId, item.Id, item.Code, item.Name, item.Address, item.City, item.State, item.PostalCode, item.Country }, cancellationToken: ct));
        }
        else
        {
            var count = await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_terminal SET name=@Name,address=@Address,city=@City,state=@State,postal_code=@PostalCode,country=@Country,revision=revision+1 WHERE tenant_id=@TenantId AND id=@Id AND code=@Code AND revision=@Revision AND deleted_at IS NULL", new { tenant.TenantId, item.Id, item.Code, item.Name, item.Address, item.City, item.State, item.PostalCode, item.Country, item.Revision }, cancellationToken: ct));
            if (count != 1) throw new FormatException("Terminal changed or no longer exists. Refresh and try again. Terminal codes cannot be changed.");
        }
    }
    public async Task Delete(Guid id, int revision, CancellationToken ct)
    {
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var code = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT code FROM aurora_terminal WHERE tenant_id=@TenantId AND id=@id AND revision=@revision AND deleted_at IS NULL FOR UPDATE", new { tenant.TenantId, id, revision }, tx, cancellationToken: ct));
        if (code is null) throw new FormatException("Terminal changed or no longer exists. Refresh and try again.");
        var used = await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_order WHERE tenant_id=@TenantId AND terminal_id=@id AND deleted_at IS NULL)
            OR EXISTS(SELECT 1 FROM routing_equipment_unit WHERE tenant_id=@TenantId AND lower(trim(data->>'Terminal'))=lower(@code))
            """, new { tenant.TenantId, id, code }, tx, cancellationToken: ct));
        if (used) throw new FormatException("This terminal is used by orders or fleet equipment. Reassign them before deleting it.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_terminal SET deleted_at=now(),revision=revision+1 WHERE tenant_id=@TenantId AND id=@id", new { tenant.TenantId, id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
}
