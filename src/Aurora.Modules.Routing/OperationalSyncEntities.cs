using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

public sealed class OperationalSyncEntities(ITenantContext tenant,DriverStore drivers):ISyncEntityStore
{
    public async Task<SyncEntity?> Read(string entityType,string localId,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        await OperationalOutbox.Lock(db,tx,tenant.TenantId,ct);
        if(entityType=="driver")return await drivers.Read(localId,db,tx,ct);
        if(entityType=="equipment")return await EquipmentSyncEntities.Read(tenant.TenantId,localId,db,tx,ct);
        if(entityType=="manifest")return await ManifestSyncEntities.Read(tenant.TenantId,localId,db,tx,ct);
        if(entityType=="order")return await OrderSyncEntities.Read(tenant.TenantId,localId,db,tx,ct);
        var sql=entityType switch{
            "terminal"=>"SELECT revision::bigint AS Revision,deleted_at IS NOT NULL AS Deleted,((details || jsonb_build_object('Code',code,'Name',name,'Address',address,'City',city,'State',state,'PostalCode',postal_code,'Country',country)) - 'Id' - 'Revision')::text AS Json FROM aurora_terminal WHERE tenant_id=@TenantId AND id=@id FOR UPDATE",
            "customer"=>"SELECT revision::bigint AS Revision,deleted_at IS NOT NULL AS Deleted,((data || jsonb_build_object('Code',code,'Name',name)) - 'Id' - 'Revision')::text AS Json FROM aurora_customer WHERE tenant_id=@TenantId AND id=@id FOR UPDATE",
            _=>throw new FormatException($"No import mapping exists for {entityType}.")};
        if(!Guid.TryParse(localId,out var id))throw new FormatException("Invalid local record ID.");
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql,new{tenant.TenantId,id},tx,cancellationToken:ct));
        return row is null?null:new(localId,row.Revision,JsonNode.Parse(row.Json)!.AsObject(),row.Deleted);
    }
    public async Task<SyncEntity> Write(string entityType,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        await OperationalOutbox.Lock(db,tx,tenant.TenantId,ct);
        if(entityType=="driver")return await drivers.Write(localId,data,deleted,db,tx,ct);
        if(entityType=="equipment")return await EquipmentSyncEntities.Write(tenant.TenantId,localId,data,deleted,db,tx,ct);
        if(entityType=="manifest")return await ManifestSyncEntities.Write(tenant.TenantId,localId,data,deleted,db,tx,ct);
        if(entityType=="order")return await OrderSyncEntities.Write(tenant.TenantId,localId,data,deleted,db,tx,ct);
        var id=localId is null?Guid.NewGuid():Guid.Parse(localId);
        var normalized=(JsonObject)data.DeepClone();normalized.Remove("Id");normalized.Remove("Revision");
        int revision;
        if(entityType=="terminal")
        {
            var item=normalized.Deserialize<TerminalDto>()??throw new FormatException("Invalid terminal.");item.Id=id;item.Validate();
            if(!deleted)
            {
                var rows=await db.QueryAsync<string>(new CommandDefinition("SELECT (details || jsonb_build_object('Id',id,'Code',code,'Name',name))::text FROM aurora_terminal WHERE tenant_id=@TenantId AND deleted_at IS NULL",new{tenant.TenantId},tx,cancellationToken:ct));
                var nodes=rows.Select(row=>JsonSerializer.Deserialize<TerminalDto>(row)!).ToList();
                TerminalRoutingRules.Validate(item,nodes);
                if(item.RoutingRole==NodeVocabulary.Hub&&item.Region.Trim().Length>0&&nodes.Any(n=>n.Id!=id&&n.RoutingRole==NodeVocabulary.Hub&&n.Region.Trim().Equals(item.Region.Trim(),StringComparison.OrdinalIgnoreCase)))throw new FormatException("A region can have only one hub.");
            }
            var sql=localId is null?"INSERT INTO aurora_terminal(tenant_id,id,code,name,address,city,state,postal_code,country,details,deleted_at) VALUES(@TenantId,@id,@Code,@Name,@Address,@City,@State,@PostalCode,@Country,CAST(@json AS jsonb),CASE WHEN @deleted THEN now() END) RETURNING revision"
                :"UPDATE aurora_terminal SET name=@Name,address=@Address,city=@City,state=@State,postal_code=@PostalCode,country=@Country,details=CAST(@json AS jsonb),deleted_at=CASE WHEN @deleted THEN now() END,revision=revision+1 WHERE tenant_id=@TenantId AND id=@id AND code=@Code RETURNING revision";
            revision=await db.QuerySingleAsync<int>(new CommandDefinition(sql,new{tenant.TenantId,id,item.Code,item.Name,item.Address,item.City,item.State,item.PostalCode,item.Country,json=normalized.ToJsonString(),deleted},tx,cancellationToken:ct));
        }
        else if(entityType=="customer")
        {
            var item=normalized.Deserialize<CustomerDto>()??throw new FormatException("Invalid customer.");item.Validate();
            var sql=localId is null?"INSERT INTO aurora_customer(tenant_id,id,code,name,data,deleted_at) VALUES(@TenantId,@id,@Code,@Name,CAST(@json AS jsonb),CASE WHEN @deleted THEN now() END) RETURNING revision"
                :"UPDATE aurora_customer SET name=@Name,data=CAST(@json AS jsonb),deleted_at=CASE WHEN @deleted THEN now() END,revision=revision+1 WHERE tenant_id=@TenantId AND id=@id AND code=@Code RETURNING revision";
            revision=await db.QuerySingleAsync<int>(new CommandDefinition(sql,new{tenant.TenantId,id,item.Code,item.Name,json=normalized.ToJsonString(),deleted},tx,cancellationToken:ct));
        }
        else throw new FormatException($"No import mapping exists for {entityType}.");
        return new(id.ToString(),revision,normalized,deleted);
    }
    sealed record Row(long Revision,bool Deleted,string Json);
}
