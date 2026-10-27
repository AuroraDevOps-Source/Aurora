using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

internal static class OperationalOutbox
{
    public static Task Lock(NpgsqlConnection db,NpgsqlTransaction tx,Guid tenantId,CancellationToken ct)=>db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"planning:{tenantId}"},tx,cancellationToken:ct));
    public static async Task Order(NpgsqlConnection db,NpgsqlTransaction tx,Guid tenantId,Guid sourceId,string id,CancellationToken ct)
    {
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT aurora_source_in_workspace(@tenantId,@sourceId)",new{tenantId,sourceId},tx,cancellationToken:ct)))
            throw new FormatException("This order belongs to a different source database and is read-only.");
        var row=await db.QuerySingleAsync<Payload>(new CommandDefinition(OrderSyncEntities.Select,new{tenantId,sourceId,id},tx,cancellationToken:ct));
        await SyncStore.Enqueue(db,tx,tenantId,"order",$"{sourceId:D}/{id}",row.Revision,row.Deleted?"delete":"upsert",row.Json,ct);
    }
    public static async Task Manifest(NpgsqlConnection db,NpgsqlTransaction tx,Guid tenantId,Guid id,CancellationToken ct)
    {
        if(await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest m JOIN aurora_planning_draft d ON d.tenant_id=m.tenant_id AND d.id=m.draft_id WHERE m.tenant_id=@tenantId AND m.id=@id AND NOT aurora_source_in_workspace(d.tenant_id,d.source_id)) OR EXISTS(SELECT 1 FROM aurora_manifest_order a WHERE a.tenant_id=@tenantId AND a.manifest_id=@id AND NOT aurora_source_in_workspace(a.tenant_id,a.source_id))",new{tenantId,id},tx,cancellationToken:ct)))
            throw new FormatException("This manifest belongs to a different source database and is read-only.");
        var row=await db.QuerySingleAsync<Payload>(new CommandDefinition(ManifestSyncEntities.Select,new{tenantId,id},tx,cancellationToken:ct));
        await SyncStore.Enqueue(db,tx,tenantId,"manifest",id.ToString(),row.Revision,row.Deleted?"delete":"upsert",row.Json,ct);
    }
    sealed record Payload(long Revision,bool Deleted,string Json);
    public static async Task Master(NpgsqlConnection db,NpgsqlTransaction tx,Guid tenantId,string kind,Guid id,CancellationToken ct)
    {
        var select=kind switch{
            "terminal"=>"SELECT revision::bigint AS Revision,deleted_at IS NOT NULL AS Deleted,((details || jsonb_build_object('Code',code,'Name',name,'Address',address,'City',city,'State',state,'PostalCode',postal_code,'Country',country)) - 'Id' - 'Revision')::text AS Json FROM aurora_terminal WHERE tenant_id=@tenantId AND id=@id",
            "customer"=>"SELECT revision::bigint AS Revision,deleted_at IS NOT NULL AS Deleted,((data || jsonb_build_object('Code',code,'Name',name)) - 'Id' - 'Revision')::text AS Json FROM aurora_customer WHERE tenant_id=@tenantId AND id=@id",
            _=>throw new FormatException("Unknown master type.")};
        var row=await db.QuerySingleAsync<Payload>(new CommandDefinition(select,new{tenantId,id},tx,cancellationToken:ct));
        await SyncStore.Enqueue(db,tx,tenantId,kind,id.ToString(),row.Revision,row.Deleted?"delete":"upsert",row.Json,ct);
    }
}
