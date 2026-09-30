using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class CustomerStore(NpgsqlConnectionFactory factory,ITenantContext tenant)
{
    public async Task<List<CustomerDto>> List(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<string>(new CommandDefinition("SELECT (data || jsonb_build_object('Id',id,'Code',code,'Name',name,'Revision',revision))::text FROM aurora_customer WHERE tenant_id=@TenantId AND deleted_at IS NULL ORDER BY name,code",new {tenant.TenantId},cancellationToken:ct));
        return rows.Select(x=>JsonSerializer.Deserialize<CustomerDto>(x)!).ToList();
    }
    public async Task Save(CustomerDto item,bool create,CancellationToken ct)
    {
        item.Validate();await using var db=await factory.OpenConnectionAsync(ct);
        if(create) item.Id=Guid.NewGuid();
        var sql=create ? "INSERT INTO aurora_customer(tenant_id,id,code,name,data) VALUES(@TenantId,@Id,@Code,@Name,CAST(@json AS jsonb))" : "UPDATE aurora_customer SET name=@Name,data=CAST(@json AS jsonb),revision=revision+1 WHERE tenant_id=@TenantId AND id=@Id AND code=@Code AND revision=@Revision AND deleted_at IS NULL";
        if(await db.ExecuteAsync(new CommandDefinition(sql,new {tenant.TenantId,item.Id,item.Code,item.Name,item.Revision,json=JsonSerializer.Serialize(item)},cancellationToken:ct))!=1) throw new FormatException("Customer changed or no longer exists. Refresh and try again. Customer codes cannot be changed.");
    }
    public async Task Delete(Guid id,int revision,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);
        var found=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_customer WHERE tenant_id=@TenantId AND id=@id AND revision=@revision AND deleted_at IS NULL FOR UPDATE",new {tenant.TenantId,id,revision},tx,cancellationToken:ct));
        if(found is null) throw new FormatException("Customer changed or no longer exists. Refresh and try again.");
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_order WHERE tenant_id=@TenantId AND customer_id=@id AND deleted_at IS NULL)",new {tenant.TenantId,id},tx,cancellationToken:ct))) throw new FormatException("This customer is used by orders. Reassign those orders before deleting it.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_customer SET deleted_at=now(),revision=revision+1 WHERE tenant_id=@TenantId AND id=@id",new {tenant.TenantId,id},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
}
