using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Npgsql;
namespace Aurora.Modules.Routing;

public sealed class TmsStore(NpgsqlConnectionFactory factory, ITenantContext tenant)
{
    const string OrderSql = """
      SELECT (jsonb_build_object('Address',COALESCE(s.request->'reporting'->'orders'->o.id->>'address1',''),
      'State',COALESCE(s.request->'reporting'->'orders'->o.id->>'state',''),
      'PostalCode',COALESCE(s.request->'reporting'->'orders'->o.id->>'postalCode',''),
      'Weight',COALESCE((SELECT l->'value' FROM jsonb_array_elements(s.request#>'{orders,deliveries}') d CROSS JOIN LATERAL jsonb_array_elements(d#>'{properties,loads}') l WHERE d->>'id'=o.id AND l->>'dimension'='weight' LIMIT 1),'0'::jsonb),
      'Cube',COALESCE((SELECT l->'value' FROM jsonb_array_elements(s.request#>'{orders,deliveries}') d CROSS JOIN LATERAL jsonb_array_elements(d#>'{properties,loads}') l WHERE d->>'id'=o.id AND l->>'dimension'='volume' LIMIT 1),'0'::jsonb),
      'Pieces',COALESCE(s.request->'reporting'->'orders'->o.id->'units','0'::jsonb),
      'Notes',COALESCE(s.request->'reporting'->'orders'->o.id->>'specialInstructions','')) || o.details || jsonb_build_object('SourceId',o.source_id,'Id',o.id,'ScheduledAt',o.scheduled_at,
      'Customer',o.customer,'City',o.city,'Status',o.status,'Revision',o.revision,'TerminalId',o.terminal_id,
      'TerminalCode',(SELECT code FROM aurora_terminal t WHERE (t.tenant_id,t.id)=(o.tenant_id,o.terminal_id)),
      'CustomerId',o.customer_id,'CustomerCode',(SELECT code FROM aurora_customer c WHERE (c.tenant_id,c.id)=(o.tenant_id,o.customer_id)),
      'ManifestId',m.id,'ManifestNumber',m.number,'StopNumber',a.stop_number))::text
      FROM aurora_order o JOIN aurora_order_source s ON (s.tenant_id,s.id)=(o.tenant_id,o.source_id) LEFT JOIN aurora_manifest_order a ON (a.tenant_id,a.source_id,a.order_id)=(o.tenant_id,o.source_id,o.id)
      LEFT JOIN aurora_manifest m ON (m.tenant_id,m.id)=(a.tenant_id,a.manifest_id)
      WHERE o.tenant_id=@TenantId AND o.deleted_at IS NULL
      """;
    const string ManifestSql = """
      SELECT jsonb_build_object('Id',m.id,'Number',m.number,'ManifestDate',m.manifest_date,'Status',m.status,
      'Vehicle',m.vehicle,'Details',m.details,'Revision',m.revision,
      'OrderCount',(SELECT count(*) FROM aurora_manifest_order a WHERE a.tenant_id=m.tenant_id AND a.manifest_id=m.id))::text
      FROM aurora_manifest m WHERE m.tenant_id=@TenantId AND m.deleted_at IS NULL
      """;
    public async Task<List<TmsOrder>> Orders(DateTimeOffset? from,DateTimeOffset? to,string? status,string? search,Guid? manifest,CancellationToken ct,Guid? terminal=null)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<string>(new CommandDefinition(OrderSql+" AND (@from IS NULL OR o.scheduled_at>=@from) AND (@to IS NULL OR o.scheduled_at<=@to) AND (@status IS NULL OR o.status=@status) AND (@terminal IS NULL OR o.terminal_id=@terminal) AND (@manifest IS NULL OR a.manifest_id=@manifest) AND (@search IS NULL OR concat_ws(' ',o.id,o.customer,o.city,o.details->>'Reference') ILIKE '%'||@search||'%') ORDER BY a.stop_number NULLS LAST,o.scheduled_at,o.id",new {tenant.TenantId,from=from?.ToUniversalTime(),to=to?.ToUniversalTime(),status=string.IsNullOrEmpty(status)?null:status,search=string.IsNullOrWhiteSpace(search)?null:search,manifest,terminal},cancellationToken:ct));
        return rows.Select(x=>JsonSerializer.Deserialize<TmsOrder>(x)!).ToList();
    }
    public async Task<List<TmsManifest>> Manifests(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return (await db.QueryAsync<string>(new CommandDefinition(ManifestSql+" ORDER BY m.manifest_date DESC,m.number",new{tenant.TenantId},cancellationToken:ct))).Select(x=>JsonSerializer.Deserialize<TmsManifest>(x)!).ToList();
    }
    static void Check(int count) { if(count!=1) throw new FormatException("This record changed or no longer exists. Refresh and try again."); }
    public async Task SaveOrder(TmsOrder item,bool create,CancellationToken ct)
    {
        item.Validate(); item.ScheduledAt=item.ScheduledAt.ToUniversalTime();
        await using var db=await factory.OpenConnectionAsync(ct); await using var tx=await db.BeginTransactionAsync(ct);
        if (item.TerminalId is {} terminalId && await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_terminal WHERE tenant_id=@TenantId AND id=@terminalId AND deleted_at IS NULL FOR SHARE", new {tenant.TenantId,terminalId},tx,cancellationToken:ct)) is null)
            throw new FormatException("Choose a saved terminal from your company.");
        if (item.CustomerId is {} customerId && await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM aurora_customer WHERE tenant_id=@TenantId AND id=@customerId AND deleted_at IS NULL FOR SHARE", new {tenant.TenantId,customerId},tx,cancellationToken:ct)) is null)
            throw new FormatException("Choose a saved customer from your company.");
        if(create) {
            if(item.Status=="Routed" || item.Status=="Dispatched" || item.Status=="InTransit" || item.Status=="OutForDelivery") throw new FormatException("Assign the order to a manifest to set that status.");
            item.SourceId=await db.ExecuteScalarAsync<Guid>(new CommandDefinition("INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES(@TenantId,gen_random_uuid(),'Manual orders','{}') ON CONFLICT(tenant_id,name) DO UPDATE SET name=excluded.name RETURNING id",new{tenant.TenantId},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,status,terminal_id,customer_id,details) VALUES(@TenantId,@SourceId,@Id,@ScheduledAt,@Customer,@City,@Status,@TerminalId,@CustomerId,CAST(@json AS jsonb))",new{tenant.TenantId,item.SourceId,item.Id,item.ScheduledAt,item.Customer,item.City,item.Status,item.TerminalId,item.CustomerId,json=JsonSerializer.Serialize(item)},tx,cancellationToken:ct));
        } else {
            // Lock the owning manifest before its order, matching membership operations.
            var mid=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT manifest_id FROM aurora_manifest_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND order_id=@Id",new{tenant.TenantId,item.SourceId,item.Id},tx,cancellationToken:ct));
            if(mid is {} id) { var m=await Lock(db,tx,id,ct); if(!m.Editable) throw new FormatException("This manifest has been dispatched; its orders cannot be edited."); }
            var current=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT status FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@Id AND deleted_at IS NULL FOR UPDATE",new{tenant.TenantId,item.SourceId,item.Id},tx,cancellationToken:ct));
            if(current is null) throw new KeyNotFoundException("Order not found.");
            var assigned=await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM aurora_manifest_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND order_id=@Id)",new{tenant.TenantId,item.SourceId,item.Id},tx,cancellationToken:ct));
            if(assigned && (mid is null || item.Status!=current)) throw new FormatException("Manifest assignment controls this order's status. Refresh and try again.");
            if(!assigned && new[]{"Routed","Dispatched","InTransit","OutForDelivery"}.Contains(item.Status)) throw new FormatException("Assign the order to a manifest to set that status.");
            Check(await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_order SET scheduled_at=@ScheduledAt,customer=@Customer,city=@City,status=@Status,terminal_id=@TerminalId,customer_id=@CustomerId,details=CAST(@json AS jsonb),revision=revision+1 WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@Id AND revision=@Revision AND deleted_at IS NULL",new{tenant.TenantId,item.SourceId,item.Id,item.ScheduledAt,item.Customer,item.City,item.Status,item.TerminalId,item.CustomerId,item.Revision,json=JsonSerializer.Serialize(item)},tx,cancellationToken:ct)));
        }
        await tx.CommitAsync(ct);
    }
    public async Task DeleteOrder(Guid source,string id,int revision,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        Check(await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_order o SET deleted_at=now(),revision=revision+1 WHERE tenant_id=@TenantId AND source_id=@source AND id=@id AND revision=@revision AND deleted_at IS NULL AND NOT EXISTS(SELECT 1 FROM aurora_manifest_order a WHERE (a.tenant_id,a.source_id,a.order_id)=(o.tenant_id,o.source_id,o.id))",new{tenant.TenantId,source,id,revision},cancellationToken:ct)));
    }
    async Task<TmsManifest> Lock(NpgsqlConnection db,NpgsqlTransaction tx,Guid id,CancellationToken ct) => JsonSerializer.Deserialize<TmsManifest>(await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition(ManifestSql+" AND m.id=@id FOR UPDATE",new{tenant.TenantId,id},tx,cancellationToken:ct)) ?? throw new KeyNotFoundException("Manifest not found."))!;
    public async Task SaveManifest(TmsManifest item,bool create,CancellationToken ct)
    {
        item.Validate();
        if(item.Vehicle.Length>0 && !(await new EquipmentStore(factory,tenant).List(ct)).Units.Any(u=>u.Id==item.Vehicle)) throw new FormatException("Choose a truck from your company’s Fleet Management.");
        await using var db=await factory.OpenConnectionAsync(ct); await using var tx=await db.BeginTransactionAsync(ct);
        if(create) {
            if(item.Status!="Planning") throw new FormatException("New manifests start in Planning.");
            item.Id=Guid.NewGuid();
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest(tenant_id,id,vehicle,data,number,manifest_date,status,details) VALUES(@TenantId,@Id,@Vehicle,'{}',@Number,@date,@Status,CAST(@json AS jsonb))",new{tenant.TenantId,item.Id,item.Vehicle,item.Number,date=item.ManifestDate.ToDateTime(TimeOnly.MinValue),item.Status,json=JsonSerializer.Serialize(item.Details)},tx,cancellationToken:ct));
        } else {
            var current=await Lock(db,tx,item.Id,ct);
            if(current.Revision!=item.Revision) throw new FormatException("Manifest changed. Refresh and try again.");
            if(!current.Editable && Array.IndexOf(TmsManifest.Statuses,item.Status)<Array.IndexOf(TmsManifest.Statuses,current.Status)) throw new FormatException("A dispatched manifest cannot move backwards.");
            await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_manifest SET vehicle=@Vehicle,number=@Number,manifest_date=@date,status=@Status,details=CAST(@json AS jsonb),revision=revision+1 WHERE tenant_id=@TenantId AND id=@Id",new{tenant.TenantId,item.Id,item.Vehicle,item.Number,date=item.ManifestDate.ToDateTime(TimeOnly.MinValue),item.Status,json=JsonSerializer.Serialize(item.Details)},tx,cancellationToken:ct));
            var status=item.Status switch {"Complete"=>"Delivered","Dispatched"=>"Dispatched","En Route" or "Arrived"=>"OutForDelivery",_=>"Routed"};
            await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_order o SET status=@status,revision=revision+1 FROM aurora_manifest_order a WHERE a.tenant_id=@TenantId AND a.manifest_id=@Id AND (o.tenant_id,o.source_id,o.id)=(a.tenant_id,a.source_id,a.order_id)",new{tenant.TenantId,item.Id,status},tx,cancellationToken:ct));
        }
        await tx.CommitAsync(ct);
    }
    public async Task ChangeOrders(Guid id,TmsAssignmentChange input,bool remove,bool delete,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct); await using var tx=await db.BeginTransactionAsync(ct);
        var m=await Lock(db,tx,id,ct);
        if(!m.Editable) throw new FormatException("Orders cannot be changed after dispatch.");
        if(m.Revision!=input.Revision) throw new FormatException("Manifest changed. Refresh and try again.");
        var keys=delete ? (await db.QueryAsync<TmsOrderKey>(new CommandDefinition("SELECT source_id AS SourceId,order_id AS Id FROM aurora_manifest_order WHERE tenant_id=@TenantId AND manifest_id=@manifestId",new{tenant.TenantId,manifestId=id},tx,cancellationToken:ct))).ToArray() : input.Orders;
        if(keys is null || keys.Length>5000 || keys.Distinct().Count()!=keys.Length) throw new FormatException("Select distinct orders.");
        foreach(var key in keys.OrderBy(k=>k.SourceId).ThenBy(k=>k.Id,StringComparer.Ordinal)) {
            var status=await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT status FROM aurora_order WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@Id AND deleted_at IS NULL FOR UPDATE",new{tenant.TenantId,key.SourceId,key.Id},tx,cancellationToken:ct));
            if(status is null) throw new FormatException("Order no longer exists.");
            if(remove || delete) {
                Check(await db.ExecuteAsync(new CommandDefinition("DELETE FROM aurora_manifest_order WHERE tenant_id=@TenantId AND manifest_id=@manifestId AND source_id=@SourceId AND order_id=@Id",new{tenant.TenantId,manifestId=id,key.SourceId,key.Id},tx,cancellationToken:ct)));
            } else {
                if(status!=WorkspaceOrderStatus.Ready) throw new FormatException("Only Ready to Ship orders can be added.");
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) SELECT @TenantId,@manifestId,@SourceId,@Id,COALESCE(max(stop_number),0)+1 FROM aurora_manifest_order WHERE tenant_id=@TenantId AND manifest_id=@manifestId",new{tenant.TenantId,manifestId=id,key.SourceId,key.Id},tx,cancellationToken:ct));
            }
            await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_order SET status=@status,revision=revision+1 WHERE tenant_id=@TenantId AND source_id=@SourceId AND id=@Id",new{tenant.TenantId,key.SourceId,key.Id,status=remove||delete?WorkspaceOrderStatus.Ready:"Routed"},tx,cancellationToken:ct));
        }
        await db.ExecuteAsync(new CommandDefinition("UPDATE aurora_manifest SET revision=revision+1,deleted_at=CASE WHEN @delete THEN now() ELSE deleted_at END WHERE tenant_id=@TenantId AND id=@manifestId",new{tenant.TenantId,manifestId=id,delete},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
}
