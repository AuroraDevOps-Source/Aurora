using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

// Driver identity and labor rates belong to Aurora. Truck assignments and working
// hours belong to each plan/manifest, never to the saved driver profile.
public sealed class DriverStore(NpgsqlConnectionFactory factory,ITenantContext tenant)
{
    public async Task<NovaDriver[]> List(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<string>(new CommandDefinition("SELECT (data || jsonb_build_object('Id',id,'Number',number,'Terminal',terminal,'Revision',revision::text))::text FROM aurora_driver WHERE tenant_id=@TenantId AND deleted_at IS NULL ORDER BY number",new{tenant.TenantId},cancellationToken:ct));
        return rows.Select(x=>JsonSerializer.Deserialize<NovaDriver>(x)!).ToArray();
    }

    public async Task<NovaDriver> Save(long id,NovaDriver input,CancellationToken ct)
    {
        if(id<0)throw new FormatException("Invalid driver ID.");
        input.Validate();
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await Lock(db,tx,ct);
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_terminal WHERE tenant_id=@TenantId AND code=@Terminal AND deleted_at IS NULL)",new{tenant.TenantId,input.Terminal},tx,cancellationToken:ct)))
            throw new FormatException("Choose a saved terminal.");
        if(id>0)
        {
            var current=await Read(id.ToString(CultureInfo.InvariantCulture),db,tx,ct)??throw new KeyNotFoundException("Driver no longer exists.");
            if(current.Deleted||current.Revision.ToString(CultureInfo.InvariantCulture)!=input.Revision)throw new FormatException("This driver changed. Refresh before saving.");
            if(current.Data["Number"]?.GetValue<string>()!=input.Number.Trim())throw new FormatException("Driver number cannot be changed.");
        }
        var saved=await Write(id==0?null:id.ToString(CultureInfo.InvariantCulture),Data(input),false,db,tx,ct);
        await SyncStore.Enqueue(db,tx,tenant.TenantId,"driver",saved.Id,saved.Revision,"upsert",saved.Data.ToJsonString(),ct);
        await tx.CommitAsync(ct);
        return Model(saved);
    }

    public async Task Delete(long id,string revision,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await Lock(db,tx,ct);
        var current=await Read(id.ToString(CultureInfo.InvariantCulture),db,tx,ct)??throw new KeyNotFoundException("Driver no longer exists.");
        if(current.Deleted||current.Revision.ToString(CultureInfo.InvariantCulture)!=revision)throw new FormatException("This driver changed. Refresh before deleting.");
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_manifest WHERE tenant_id=@TenantId AND deleted_at IS NULL AND status<>'Complete'
              AND (details->>'DriverId'=@key OR EXISTS(SELECT 1 FROM jsonb_each(COALESCE(data#>'{reporting,drivers}','{}')) d WHERE d.value->>'id'=@key)))
            """,new{tenant.TenantId,key=current.Id},tx,cancellationToken:ct)))throw new FormatException("This driver is assigned to an open manifest. Reassign it before deleting.");
        var saved=await Write(current.Id,current.Data,true,db,tx,ct);
        await SyncStore.Enqueue(db,tx,tenant.TenantId,"driver",saved.Id,saved.Revision,"delete",saved.Data.ToJsonString(),ct);
        await tx.CommitAsync(ct);
    }

    internal Task Lock(NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)=>db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"planning:{tenant.TenantId}"},tx,cancellationToken:ct));

    internal async Task<SyncEntity?> Read(string id,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        if(!long.TryParse(id,NumberStyles.None,CultureInfo.InvariantCulture,out var key))throw new FormatException("Invalid driver ID.");
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT id,revision,data::text AS Data,deleted_at IS NOT NULL AS Deleted FROM aurora_driver WHERE tenant_id=@TenantId AND id=@key FOR UPDATE",new{tenant.TenantId,key},tx,cancellationToken:ct));
        return row is null?null:new(id,row.Revision,JsonNode.Parse(row.Data)!.AsObject(),row.Deleted);
    }

    internal async Task<SyncEntity> Write(string? id,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var model=data.Deserialize<NovaDriver>()??throw new FormatException("Invalid driver.");
        model.Validate();
        var normalized=Data(model);
        var sql=id is null?"INSERT INTO aurora_driver(tenant_id,number,terminal,data,deleted_at) VALUES(@TenantId,@number,@terminal,CAST(@json AS jsonb),CASE WHEN @deleted THEN now() END) RETURNING id,revision":"UPDATE aurora_driver SET number=@number,terminal=@terminal,data=CAST(@json AS jsonb),deleted_at=CASE WHEN @deleted THEN now() END,revision=revision+1 WHERE tenant_id=@TenantId AND id=@key RETURNING id,revision";
        var saved=await db.QuerySingleAsync<Saved>(new CommandDefinition(sql,new{tenant.TenantId,key=id is null?(long?)null:long.Parse(id,CultureInfo.InvariantCulture),number=model.Number.Trim(),terminal=model.Terminal.Trim(),json=normalized.ToJsonString(),deleted},tx,cancellationToken:ct));
        return new(saved.Id.ToString(CultureInfo.InvariantCulture),saved.Revision,normalized,deleted);
    }

    public static JsonObject Data(NovaDriver input)=>new(){["Number"]=input.Number.Trim(),["FirstName"]=input.FirstName.Trim(),["LastName"]=input.LastName.Trim(),["Terminal"]=input.Terminal.Trim(),["Phone"]=input.Phone.Trim(),["Email"]=input.Email.Trim(),["HireDate"]=input.HireDate,["TerminationDate"]=input.TerminationDate,["LicenseExpiry"]=input.LicenseExpiry,["PerHour"]=input.PerHour};
    static NovaDriver Model(SyncEntity entity){var model=entity.Data.Deserialize<NovaDriver>()!;model.Id=long.Parse(entity.Id,CultureInfo.InvariantCulture);model.Revision=entity.Revision.ToString(CultureInfo.InvariantCulture);return model;}
    sealed record Row(long Id,long Revision,string Data,bool Deleted);
    sealed record Saved(long Id,long Revision);
}
