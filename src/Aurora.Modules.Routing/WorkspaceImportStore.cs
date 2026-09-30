using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Modules.Routing;

public sealed class WorkspaceImportStore(NpgsqlConnectionFactory factory, ITenantContext tenant)
{
    public async Task<ImportSummary> Execute(WorkspaceImport data, bool replace, CancellationToken ct)
    {
        data.Validate();
        var sources=new List<(Guid Id,string Name,string Json)>();
        var orderSources=new Dictionary<string,Guid>();
        var orders=data.Orders.ToDictionary(o=>o.Id);
        foreach(var source in data.PlanningSources)
        {
            if(string.IsNullOrWhiteSpace(source.Name) || source.Name.Length>150 || source.Request is null) throw new FormatException("Planning sources need a name (up to 150 characters) and request object.");
            var json=source.Request.ToJsonString();
            var root=RoutingInput.Parse(RoutingInput.Prepare(json));
            var sourceTerminal=RoutingInput.Text(root["reporting"]?["terminal"]);
            var sourceId=Guid.NewGuid();
            foreach(var id in OrderWorkspace.OrderIds(json))
            {
                if(!orders.TryGetValue(id,out var order) || !orderSources.TryAdd(id,sourceId)) throw new FormatException($"Planning sources contain an unknown or repeated order: {id}.");
                if(!string.Equals(order.TerminalCode,sourceTerminal,StringComparison.OrdinalIgnoreCase)) throw new FormatException($"Planning source terminal does not match order {id}.");
            }
            sources.Add((sourceId,source.Name,json));
        }
        if(sources.Select(s=>s.Name).Distinct().Count()!=sources.Count || sources.Any(s=>s.Name=="Imported orders")) throw new FormatException("Use distinct planning source names; Imported orders is reserved.");
        var fallback=Guid.NewGuid();
        foreach(var order in data.Orders) if(!orderSources.ContainsKey(order.Id)) orderSources.Add(order.Id,fallback);
        if(orderSources.ContainsValue(fallback)) sources.Add((fallback,"Imported orders","{}"));

        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        if(replace)
        {
            // Pause writers while replacing the related rows, including writers on existing CRUD endpoints.
            await db.ExecuteAsync(new CommandDefinition("LOCK TABLE aurora_terminal,aurora_customer,aurora_order_source,aurora_order,aurora_planning_draft,aurora_manifest,aurora_manifest_order,routing_equipment_type,routing_equipment_unit,route_plan IN SHARE ROW EXCLUSIVE MODE",transaction:tx,cancellationToken:ct));
        }
        var types=(await db.QueryAsync<string>(new CommandDefinition("SELECT data::text FROM routing_equipment_type WHERE tenant_id=@TenantId",new {tenant.TenantId},tx,cancellationToken:ct)))
            .Select(s=>JsonSerializer.Deserialize<EquipmentTypeDto>(s)!).ToDictionary(t=>t.Code);
        foreach(var type in data.EquipmentTypes) types[type.Code]=type;
        foreach(var truck in data.Trucks)
        {
            if(!types.TryGetValue(truck.TypeCode,out var type)) throw new FormatException($"Truck {truck.Id} needs equipment type {truck.TypeCode}. Include it in equipmentTypes or save it first.");
            if(truck.UseForRouting && type.PowerOnly) throw new FormatException($"Truck {truck.Id} cannot route with a power-only equipment type.");
        }
        if(!replace) { await tx.RollbackAsync(ct); return data.Summary; }

        await db.ExecuteAsync(new CommandDefinition("""
            DELETE FROM aurora_manifest_order WHERE tenant_id=@TenantId;
            DELETE FROM aurora_manifest WHERE tenant_id=@TenantId;
            DELETE FROM aurora_planning_draft WHERE tenant_id=@TenantId;
            DELETE FROM aurora_order WHERE tenant_id=@TenantId;
            DELETE FROM aurora_order_source WHERE tenant_id=@TenantId;
            DELETE FROM aurora_customer WHERE tenant_id=@TenantId;
            DELETE FROM routing_equipment_unit WHERE tenant_id=@TenantId;
            DELETE FROM aurora_terminal WHERE tenant_id=@TenantId;
            DELETE FROM route_plan WHERE tenant_id=@TenantId;
            """,new {tenant.TenantId},tx,cancellationToken:ct));
        var terminals=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);
        var customers=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);
        foreach(var c in data.Customers)
        {
            var id=Guid.NewGuid();customers.Add(c.Code,id);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_customer(tenant_id,id,code,name,data) VALUES(@TenantId,@id,@Code,@Name,CAST(@json AS jsonb))",new {tenant.TenantId,id,c.Code,c.Name,json=JsonSerializer.Serialize(c)},tx,cancellationToken:ct));
        }
        foreach(var t in data.Terminals)
        {
            var id=Guid.NewGuid();terminals.Add(t.Code,id);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_terminal(tenant_id,id,code,name,address,city,state,postal_code,country) VALUES(@TenantId,@id,@Code,@Name,@Address,@City,@State,@PostalCode,@Country)",new {tenant.TenantId,id,t.Code,t.Name,t.Address,t.City,t.State,t.PostalCode,t.Country},tx,cancellationToken:ct));
        }
        foreach(var type in data.EquipmentTypes)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES(@TenantId,@Code,CAST(@json AS jsonb)) ON CONFLICT(tenant_id,code) DO UPDATE SET data=excluded.data,updated_utc=now()",new {tenant.TenantId,type.Code,json=JsonSerializer.Serialize(type)},tx,cancellationToken:ct));
        foreach(var truck in data.Trucks)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES(@TenantId,@Id,@TypeCode,CAST(@json AS jsonb))",new {tenant.TenantId,truck.Id,truck.TypeCode,json=JsonSerializer.Serialize(truck)},tx,cancellationToken:ct));
        foreach(var source in sources)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES(@TenantId,@Id,@Name,CAST(@Json AS jsonb))",new {tenant.TenantId,source.Id,source.Name,source.Json},tx,cancellationToken:ct));
        foreach(var order in data.Orders)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,status,terminal_id,customer_id,details) VALUES(@TenantId,@source,@Id,@time,@Customer,@City,@Status,@terminalId,@customerId,CAST(@json AS jsonb))",new {tenant.TenantId,source=orderSources[order.Id],order.Id,time=order.ScheduledAt.ToUniversalTime(),order.Customer,order.City,order.Status,terminalId=terminals[order.TerminalCode!],customerId=customers[order.CustomerCode!],json=JsonSerializer.Serialize(order)},tx,cancellationToken:ct));
        foreach(var manifest in data.Manifests)
        {
            var id=Guid.NewGuid();
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest(tenant_id,id,vehicle,data,number,manifest_date,status,details) VALUES(@TenantId,@id,@TruckId,'{}',@Number,@date,@Status,CAST(@json AS jsonb))",new {tenant.TenantId,id,manifest.TruckId,manifest.Number,date=manifest.ManifestDate.ToDateTime(TimeOnly.MinValue),manifest.Status,json=JsonSerializer.Serialize(manifest.Details)},tx,cancellationToken:ct));
            for(var i=0;i<manifest.OrderIds.Length;i++)
            {
                var order=manifest.OrderIds[i];
                await db.ExecuteAsync(new CommandDefinition("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) VALUES(@TenantId,@id,@source,@order,@stop)",new {tenant.TenantId,id,source=orderSources[order],order,stop=i+1},tx,cancellationToken:ct));
            }
        }
        await tx.CommitAsync(ct);
        return data.Summary;
    }
}
