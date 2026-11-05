using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Api.Features.Routing;

public sealed class OrderImportHistoryStore(NpgsqlConnectionFactory factory, ITenantContext tenant, ILogger<OrderImportHistoryStore> logger)
{
    public async Task<IEnumerable<OrderImportHistory>> List(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QueryAsync<OrderImportHistory>(new CommandDefinition("""
            SELECT id, file_name AS FileName, created_at AS CreatedAt,
              CASE WHEN status='Converting' AND created_at < now()-interval '7 minutes' THEN 'Interrupted' ELSE status END AS Status,
              extracted_count AS ExtractedCount, cardinality(created_pros) AS CreatedCount, created_pros AS CreatedPros
            FROM order_import_history WHERE tenant_id=@TenantId ORDER BY created_at DESC,id DESC LIMIT 20
            """,new{tenant.TenantId},cancellationToken:ct));
    }

    public async Task<Guid> Start(string fileName,CancellationToken ct)
    {
        var name=Path.GetFileName(fileName.Replace('\\','/'));
        name=new string(name.Where(c=>!char.IsControl(c)).Take(255).ToArray());
        if(string.IsNullOrWhiteSpace(name))name="Document";
        var id=Guid.NewGuid();
        await using var db=await factory.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO order_import_history(id,tenant_id,user_id,file_name,status) VALUES(@id,@TenantId,@UserId,@name,'Converting')",new{id,tenant.TenantId,tenant.UserId,name},cancellationToken:ct));
        return id;
    }

    public async Task Converted(Guid id,int count,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("UPDATE order_import_history SET status='Converted',extracted_count=@count WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId",new{id,count,tenant.TenantId,tenant.UserId},cancellationToken:ct));
    }

    public async Task Validate(Guid? id,CancellationToken ct)
    {
        if(id is null)return; // Older/manual clients have no conversion batch.
        await using var db=await factory.OpenConnectionAsync(ct);
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM order_import_history WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND status IN ('Converted','Orders created'))",new{id,tenant.TenantId,tenant.UserId},cancellationToken:ct)))
            throw new FormatException("This import is unavailable. Convert the document again.");
    }

    public async Task RequireCreatedOrder(Guid id,string pro,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM order_import_history WHERE id=@id AND tenant_id=@TenantId AND @pro=ANY(created_pros))",new{id,pro,tenant.TenantId},cancellationToken:ct)))
            throw new KeyNotFoundException("This order is not in your company's import history.");
    }

    // History is secondary to the successful business write. Never report a saved order
    // as failed (and invite a duplicate retry) because the history update failed.
    public async Task RecordCreated(Guid? id,string pro)
    {
        if(id is null)return;
        await TryUpdate(id.Value,"UPDATE order_import_history SET status='Orders created',created_pros=array_append(created_pros,@pro) WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND NOT (@pro=ANY(created_pros))",pro);
    }
    public Task Failed(Guid id)=>TryUpdate(id,"UPDATE order_import_history SET status='Failed' WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND status='Converting'",null);
    async Task TryUpdate(Guid id,string sql,string? pro)
    {
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var db=await factory.OpenConnectionAsync(timeout.Token);
            await db.ExecuteAsync(new CommandDefinition(sql,new{id,pro,tenant.TenantId,tenant.UserId},cancellationToken:timeout.Token));
        }
        catch(Exception ex){logger.LogError(ex,"Could not update import history {ImportId}",id);}
    }
}
