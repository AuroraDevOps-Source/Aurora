using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Api.Features.Auth;

public sealed class AdminDatabases(NpgsqlConnectionFactory factory,ITenantContext tenant,NovaUserConnections nova)
{
    public async Task<AdminDatabaseDto[]> List(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<Row>(new CommandDefinition("""
            SELECT d.id,d.name,d.revision,d.connection::text AS Json,
              (SELECT count(*)::int FROM user_tenant m WHERE m.tenant_id=d.tenant_id AND m.nova_database_id=d.id) AS UserCount
            FROM nova_database d WHERE d.tenant_id=@TenantId ORDER BY lower(d.name)
            """,new{tenant.TenantId},cancellationToken:ct));
        return rows.Select(r=>{var c=nova.Read(r.Json)!;return new AdminDatabaseDto(r.Id,r.Name,r.Revision,c.Host,c.Port,c.Database,c.UserName,true,r.UserCount);}).ToArray();
    }
    public async Task<Guid> Save(Guid? id,AdminDatabaseEdit input,CancellationToken ct)
    {
        var name=input.Name.Trim();
        if(name.Length is 0 or >200)throw new FormatException("Enter a database name (up to 200 characters).");
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        StoredNovaConnection? existing=null;
        if(id is not null)
        {
            var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT id,name,revision,connection::text AS Json,0 AS UserCount FROM nova_database WHERE tenant_id=@TenantId AND id=@id FOR UPDATE",new{tenant.TenantId,id},tx,cancellationToken:ct))??throw new KeyNotFoundException("Database not found.");
            if(row.Revision!=input.Revision)throw new FormatException("This database changed. Reload before saving.");
            existing=nova.Read(row.Json);
        }
        var saved=nova.Build(input,existing);
        var result=id??Guid.NewGuid();
        var sql=id is null
            ? "INSERT INTO nova_database(tenant_id,id,name,connection) VALUES(@TenantId,@result,@name,CAST(@json AS jsonb))"
            : "UPDATE nova_database SET name=@name,connection=CAST(@json AS jsonb),revision=revision+1 WHERE tenant_id=@TenantId AND id=@result";
        await db.ExecuteAsync(new CommandDefinition(sql,new{tenant.TenantId,result,name,json=JsonSerializer.Serialize(saved)},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return result;
    }
    public async Task Delete(Guid id,int revision,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var count=await db.ExecuteAsync(new CommandDefinition("DELETE FROM nova_database WHERE tenant_id=@TenantId AND id=@id AND revision=@revision",new{tenant.TenantId,id,revision},cancellationToken:ct));
        if(count!=1)throw new FormatException("This database changed or was removed. Reload before deleting.");
    }
    public async Task Test(Guid? id,AdminDatabaseEdit input,CancellationToken ct)
    {
        StoredNovaConnection? saved=null;
        if(id is not null)
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            var json=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT connection::text FROM nova_database WHERE tenant_id=@TenantId AND id=@id",new{tenant.TenantId,id},cancellationToken:ct))??throw new KeyNotFoundException("Database not found.");
            saved=nova.Read(json);
        }
        var options=nova.Resolve(nova.Build(input,saved),tenant.TenantId);
        await using var fb=new FbConnection(options.ConnectionString);
        await fb.OpenAsync(ct);
        await fb.ExecuteScalarAsync<int>(new CommandDefinition("SELECT FIRST 1 PB_ID FROM OPS_HEADER",cancellationToken:ct));
    }
    private sealed record Row(Guid Id,string Name,int Revision,string Json,int UserCount);
}

public static class AdminDatabaseEndpoints
{
    public static void MapAdminDatabaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin/databases").RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
        group.MapGet("",(AdminDatabases s,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>Results.Ok(await s.List(ct))));
        group.MapPost("",(AdminDatabaseEdit input,AdminDatabases s,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>Results.Ok(await s.Save(null,input,ct))));
        group.MapPut("/{id:guid}",(Guid id,AdminDatabaseEdit input,AdminDatabases s,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>Results.Ok(await s.Save(id,input,ct))));
        group.MapDelete("/{id:guid}",(Guid id,int revision,AdminDatabases s,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>{await s.Delete(id,revision,ct);return Results.Ok();}));
        group.MapPost("/test-connection",(Guid? id,AdminDatabaseEdit input,AdminDatabases s,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>{await s.Test(id,input,ct);return Results.Ok();}));
    }
}
