using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Microsoft.AspNetCore.DataProtection;

namespace Aurora.Api.Features.Auth;

public sealed class TenantSettingsService(NpgsqlConnectionFactory factory,ITenantContext tenant,NovaUserConnections nova,IDataProtectionProvider protection)
{
    sealed record Row(Guid NovaId,string? Nova,string? FreightOps,string? DispatchTrack,string? Zenventory,int Revision);
    sealed record FreightOpsSecret(string ApiUrl,string UserName,string ProtectedPassword);
    const string ReadSql="SELECT nova_id AS NovaId,nova::text AS Nova,freightops::text AS FreightOps,dispatchtrack::text AS DispatchTrack,zenventory::text AS Zenventory,revision FROM tenant_settings WHERE tenant_id=@TenantId";
    public async Task<TenantSettingsDto> Read(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(ReadSql,new{tenant.TenantId},cancellationToken:ct));
        var n=nova.Read(row?.Nova);
        var f=row?.FreightOps is {} json?JsonSerializer.Deserialize<FreightOpsSecret>(json):null;
        return new(){Revision=row?.Revision??0,CompanyName=await db.QuerySingleAsync<string>(new CommandDefinition("SELECT name FROM tenant WHERE id=@TenantId",new{tenant.TenantId},cancellationToken:ct)),
            Nova=n is null?new():new(){Host=n.Host,Port=n.Port,Database=n.Database,UserName=n.UserName,HasPassword=true},
            FreightOps=f is null?new():new(){ApiUrl=f.ApiUrl,UserName=f.UserName,HasPassword=true},DispatchTrack=ConnectionSettingsProtection.PublicDispatchTrack(row?.DispatchTrack),Zenventory=ConnectionSettingsProtection.PublicZenventory(row?.Zenventory)};
    }
    public async Task Save(TenantSettingsDto input,CancellationToken ct)
    {
        if(input.Nova is null || input.FreightOps is null || input.DispatchTrack is null || input.Zenventory is null)throw new FormatException("Connection sections are required.");
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"sync-connection:{tenant.TenantId}"},tx,cancellationToken:ct));
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(ReadSql+" FOR UPDATE",new{tenant.TenantId},tx,cancellationToken:ct));
        if(input.Revision!=(row?.Revision??0))throw new FormatException("Settings changed. Close and reopen Settings before saving.");
        var previous=nova.Read(row?.Nova);
        StoredNovaConnection? saved=null;
        if(previous is not null || new[]{input.Nova.Host,input.Nova.Database,input.Nova.UserName,input.Nova.Password}.Any(v=>!string.IsNullOrWhiteSpace(v)))
            saved=nova.Build(input.Nova,previous);
        var source=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT source_identity FROM aurora_sync_connection WHERE tenant_id=@TenantId AND provider='nova' FOR UPDATE",new{tenant.TenantId},tx,cancellationToken:ct));
        if(source is not null && saved?.SourceKey!=source)throw new FormatException("This tenant already has synchronized records. Changing its Nova database requires a data migration; credentials for the same database can be updated here.");
        var oldFreight=row?.FreightOps is {} oldJson?JsonSerializer.Deserialize<FreightOpsSecret>(oldJson):null;
        FreightOpsSecret? freight=null;
        var f=input.FreightOps;
        if(oldFreight is not null || new[]{f.ApiUrl,f.UserName,f.Password}.Any(v=>!string.IsNullOrWhiteSpace(v)))
        {
            if(!Uri.TryCreate(f.ApiUrl.Trim(),UriKind.Absolute,out var url) || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
                throw new FormatException("Enter a FreightOps API URL using http or https, without embedded credentials, query parameters or a fragment.");
            var user=f.UserName.Trim();
            if(user.Length is 0 or >128 || url.AbsoluteUri.Length>2000)throw new FormatException("Enter the FreightOps API URL and user ID.");
            var password=string.IsNullOrEmpty(f.Password)
                ? (oldFreight?.ApiUrl==url.AbsoluteUri && oldFreight.UserName==user?oldFreight.ProtectedPassword:null)
                : protection.CreateProtector("Aurora.TenantFreightOpsPassword.v1").Protect(f.Password);
            if(password is null)throw new FormatException("Enter the FreightOps password for this connection.");
            freight=new(url.AbsoluteUri,user,password);
        }
        var secrets=new ConnectionSettingsProtection(protection,tenant.TenantId);
        var dispatch=secrets.DispatchTrack(input.DispatchTrack,row?.DispatchTrack);
        var zenventory=secrets.Zenventory(input.Zenventory,row?.Zenventory);
        var novaId=row?.NovaId??Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO tenant_settings(tenant_id,nova_id,nova,freightops,dispatchtrack,zenventory,revision) VALUES(@TenantId,@novaId,CAST(@n AS jsonb),CAST(@f AS jsonb),CAST(@dispatch AS jsonb),CAST(@zenventory AS jsonb),1)
            ON CONFLICT(tenant_id) DO UPDATE SET nova=excluded.nova,freightops=excluded.freightops,dispatchtrack=excluded.dispatchtrack,zenventory=excluded.zenventory,revision=tenant_settings.revision+1
            """,new{tenant.TenantId,novaId,dispatch,zenventory,n=saved is null?null:JsonSerializer.Serialize(saved),f=freight is null?null:JsonSerializer.Serialize(freight)},tx,cancellationToken:ct));
        // Preserve existing synchronization state. New connections are saved disabled until explicitly enabled.
        // Keep the non-secret launcher address aligned with Connections, without changing
        // product entitlements or exposing the administrator-only connection credentials.
        await db.ExecuteAsync(new CommandDefinition("UPDATE tenant_product SET instance_url=@url WHERE tenant_id=@TenantId AND product_code='freightops'",new{tenant.TenantId,url=freight?.ApiUrl},tx,cancellationToken:ct));
        if(saved is not null && source is null)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_sync_connection(tenant_id,provider,credential_reference,source_identity,enabled,revision) VALUES(@TenantId,'nova',@novaId,@SourceKey,false,1)",new{tenant.TenantId,novaId,saved.SourceKey},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
}
public static class TenantSettingsEndpoints
{
    public static void MapTenantSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin/settings").RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
        group.MapGet("",(TenantSettingsService service,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>Results.Ok(await service.Read(ct))));
        group.MapPut("",(TenantSettingsDto input,TenantSettingsService service,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>{await service.Save(input,ct);return Results.NoContent();}));
    }
}
