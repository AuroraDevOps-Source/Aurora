using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Api.Features.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.DataProtection;

namespace Aurora.Api.Features.Auth;

public sealed class TenantSettingsService(NpgsqlConnectionFactory factory,ITenantContext tenant,NovaUserConnections nova,IDataProtectionProvider protection)
{
    sealed record Row(Guid NovaId,string? Nova,string? FreightOps,string? DispatchTrack,string? Zenventory,int Revision,decimal DriverCostPerHour,string Navigation,string DocumentExtractionInstructions,string FormRules);
    sealed record FreightOpsSecret(string ApiUrl,string UserName,string? ProtectedPassword);
    const string ReadSql="SELECT nova_id AS NovaId,nova::text AS Nova,freightops::text AS FreightOps,dispatchtrack::text AS DispatchTrack,zenventory::text AS Zenventory,revision,driver_cost_per_hour AS DriverCostPerHour,navigation::text AS Navigation,document_extraction_instructions AS DocumentExtractionInstructions,document_form_rules::text AS FormRules FROM tenant_settings WHERE tenant_id=@TenantId";
    public async Task<TenantSettingsDto> Read(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(ReadSql,new{tenant.TenantId},cancellationToken:ct));
        var n=nova.Read(row?.Nova);
        var f=row?.FreightOps is {} json?JsonSerializer.Deserialize<FreightOpsSecret>(json):null;
        return new(){Navigation=JsonSerializer.Deserialize<TenantNavigationSettings>(row?.Navigation??"{}"),Revision=row?.Revision??0,DriverCostPerHour=row?.DriverCostPerHour??0,CompanyName=await db.QuerySingleAsync<string>(new CommandDefinition("SELECT name FROM tenant WHERE id=@TenantId",new{tenant.TenantId},cancellationToken:ct)),
            GlobalDocumentSettings=await db.QuerySingleAsync<GlobalDocumentSettings>(new CommandDefinition(
                "SELECT instructions, revision, public.can_edit_global_document_settings() AS CanEdit FROM global_document_settings", cancellationToken:ct)),
            DocumentFormRules=JsonSerializer.Deserialize<List<DocumentFormRule>>(row?.FormRules??"[]"),
            DocumentExtractionInstructions=row?.DocumentExtractionInstructions??"",
            Nova=n is null?new():new(){Host=n.Host,Port=n.Port,Database=n.Database,UserName=n.UserName,HasPassword=true},
            FreightOps=f is null?new():new(){ApiUrl=f.ApiUrl,UserName=f.UserName,HasPassword=!string.IsNullOrEmpty(f.ProtectedPassword)},DispatchTrack=ConnectionSettingsProtection.PublicDispatchTrack(row?.DispatchTrack),Zenventory=ConnectionSettingsProtection.PublicZenventory(row?.Zenventory)};
    }
    public async Task TestNovaConnection(NovaConnectionSettings input, CancellationToken ct)
    {
        StoredNovaConnection? saved = null;
        if (string.IsNullOrEmpty(input.Password))
        {
            await using var db = await factory.OpenConnectionAsync(ct);
            var json = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT nova::text FROM tenant_settings WHERE tenant_id=@TenantId", new { tenant.TenantId }, cancellationToken: ct));
            saved = nova.Read(json);
        }
        var candidate = nova.Build(input, saved);
        var options = nova.Resolve(candidate, tenant.TenantId);
        var connection = new FbConnectionStringBuilder(options.ConnectionString) { Pooling = false };
        await using var firebird = new FbConnection(connection.ConnectionString);
        await firebird.OpenAsync(ct);
        await firebird.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1 FROM RDB$DATABASE", cancellationToken: ct));
    }

    public async Task Save(TenantSettingsDto input,CancellationToken ct)
    {
        OptimizationLabor.Validate(input.DriverCostPerHour);
        var instructions = input.DocumentExtractionInstructions;
        if (instructions is not null) instructions = DocumentExtractionPrompt.ValidateInstructions(instructions);
        if (input.DocumentFormRules is {} rules) DocumentFormRule.Validate(rules);
        var companyName=input.CompanyName?.Trim();
        if(string.IsNullOrWhiteSpace(companyName) || companyName.Length>200)throw new FormatException("Enter a company name of 1 to 200 characters.");
        if(input.Nova is null || input.FreightOps is null || input.DispatchTrack is null || input.Zenventory is null)throw new FormatException("Connection sections are required.");
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"sync-connection:{tenant.TenantId}"},tx,cancellationToken:ct));
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(ReadSql+" FOR UPDATE",new{tenant.TenantId},tx,cancellationToken:ct));
        if(input.Revision!=(row?.Revision??0))throw new FormatException("Settings changed. Close and reopen Settings before saving.");
        if (input.GlobalInstructionsEdit is {} edit)
        {
            var value = DocumentExtractionPrompt.ValidateInstructions(edit, TenantSettingsDto.MaxGlobalInstructionsLength);
            var updated = await db.ExecuteAsync(new CommandDefinition(
                "UPDATE global_document_settings SET instructions=@value,revision=revision+1 WHERE revision=@revision",
                new {value, revision=input.GlobalDocumentSettings?.Revision}, tx, cancellationToken:ct));
            if (updated != 1) throw new FormatException("Global instructions require Aurora staff administrator access and the current revision. Reopen Settings before saving.");
        }
        var navigation=JsonSerializer.Serialize(input.Navigation??JsonSerializer.Deserialize<TenantNavigationSettings>(row?.Navigation??"{}"));
        var previous=nova.Read(row?.Nova);
        StoredNovaConnection? saved=null;
        if(previous is not null || new[]{input.Nova.Host,input.Nova.Database,input.Nova.UserName,input.Nova.Password}.Any(v=>!string.IsNullOrWhiteSpace(v)))
            saved=nova.Build(input.Nova,previous);
        var source=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT source_identity FROM aurora_sync_connection WHERE tenant_id=@TenantId AND provider='nova' FOR UPDATE",new{tenant.TenantId},tx,cancellationToken:ct));
        // Settings select the database for direct access. Existing sync history stays bound
        // to its source; aurora_sync_credentials only serves credentials for a matching source.
        var oldFreight=row?.FreightOps is {} oldJson?JsonSerializer.Deserialize<FreightOpsSecret>(oldJson):null;
        FreightOpsSecret? freight=null;
        var f=input.FreightOps;
        if(oldFreight is not null || new[]{f.ApiUrl,f.UserName,f.Password}.Any(v=>!string.IsNullOrWhiteSpace(v)))
        {
            if(!Uri.TryCreate(f.ApiUrl?.Trim(),UriKind.Absolute,out var url) || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
                throw new FormatException("Enter a FreightOps API URL using http or https, without embedded credentials, query parameters or a fragment.");
            var user=f.UserName?.Trim() ?? "";
            if(user.Length>128 || url.AbsoluteUri.Length>2000)throw new FormatException("FreightOps allows 128 characters for the user ID and 2000 for the URL.");
            if(user.Length==0 && !string.IsNullOrEmpty(f.Password))throw new FormatException("Enter a FreightOps user ID for this password.");
            var password=string.IsNullOrEmpty(f.Password)
                ? (oldFreight?.ApiUrl==url.AbsoluteUri && oldFreight.UserName==user?oldFreight.ProtectedPassword:null)
                : protection.CreateProtector("Aurora.TenantFreightOpsPassword.v1").Protect(f.Password);
            freight=new(url.AbsoluteUri,user,password);
        }
        var secrets=new ConnectionSettingsProtection(protection,tenant.TenantId);
        var dispatch=secrets.DispatchTrack(input.DispatchTrack,row?.DispatchTrack);
        var zenventory=secrets.Zenventory(input.Zenventory,row?.Zenventory);
        var novaId=row?.NovaId??Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO tenant_settings(tenant_id,nova_id,nova,freightops,dispatchtrack,zenventory,driver_cost_per_hour,navigation,document_extraction_instructions,document_form_rules,revision) VALUES(@TenantId,@novaId,CAST(@n AS jsonb),CAST(@f AS jsonb),CAST(@dispatch AS jsonb),CAST(@zenventory AS jsonb),@DriverCostPerHour,CAST(@navigation AS jsonb),@instructions,CAST(@formRules AS jsonb),1)
            ON CONFLICT(tenant_id) DO UPDATE SET nova=excluded.nova,freightops=excluded.freightops,dispatchtrack=excluded.dispatchtrack,zenventory=excluded.zenventory,driver_cost_per_hour=excluded.driver_cost_per_hour,navigation=excluded.navigation,document_extraction_instructions=excluded.document_extraction_instructions,document_form_rules=excluded.document_form_rules,revision=tenant_settings.revision+1
            """,new{tenant.TenantId,novaId,formRules=input.DocumentFormRules is null ? row?.FormRules??"[]" : JsonSerializer.Serialize(input.DocumentFormRules),dispatch,zenventory,navigation,instructions=instructions??row?.DocumentExtractionInstructions??"",input.DriverCostPerHour,n=saved is null?null:JsonSerializer.Serialize(saved),f=freight is null?null:JsonSerializer.Serialize(freight)},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("UPDATE tenant SET name=@companyName WHERE id=@TenantId",new{tenant.TenantId,companyName},tx,cancellationToken:ct));
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
        group.MapPost("/document-preview", async (IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string? instructions, HttpRequest request, OrderDocumentConversion conversion, CancellationToken ct) =>
        {
            List<DocumentFormRule>? rules=null;
            if(request.Form.TryGetValue("formRules",out var json))
            {
                try { rules=DocumentFormRule.Validate(JsonSerializer.Deserialize<List<DocumentFormRule>>(json.ToString()) ?? []); }
                catch(Exception ex) when(ex is JsonException or FormatException) { return Results.BadRequest(new ApiErrorDto("Invalid PDF form rules. "+ex.Message)); }
            }
            return await conversion.Convert(file, instructions ?? "", ct, request.Form.TryGetValue("globalInstructions", out var draft) ? draft.ToString() : null, previewRules:rules);
        }).WithDocumentUploadLimits();
        group.MapGet("",(TenantSettingsService service,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>Results.Ok(await service.Read(ct))));
        group.MapPost("/nova/test", (NovaConnectionSettings input, TenantSettingsService service, CancellationToken ct) =>
            AdminUserEndpoints.Guard(async () => { await service.TestNovaConnection(input, ct); return Results.NoContent(); }));
        group.MapPut("",(TenantSettingsDto input,TenantSettingsService service,CancellationToken ct)=>AdminUserEndpoints.Guard(async()=>{await service.Save(input,ct);return Results.NoContent();}));
    }
}
