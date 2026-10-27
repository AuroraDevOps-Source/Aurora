using System.Text.Json;
using Aurora.Contracts;
using Dapper;
using Npgsql;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    private async Task<Dictionary<string,CustomerPlanning>> CustomerPolicies(NpgsqlConnection db,NpgsqlTransaction? tx,CancellationToken ct)
    {
        var rows=await db.QueryAsync<CustomerPolicyRow>(new CommandDefinition("SELECT customer_code AS Code,settings::text AS Settings,revision FROM aurora_customer_planning WHERE tenant_id=@TenantId AND source_key=@SourceKey",new{tenant.TenantId,Options.SourceKey},tx,cancellationToken:ct));
        return rows.ToDictionary(r=>r.Code,r=>{var p=JsonSerializer.Deserialize<CustomerPlanning>(r.Settings)!;p.Revision=r.Revision;return p;},StringComparer.OrdinalIgnoreCase);
    }
    private sealed record CustomerPolicyRow(string Code,string Settings,int Revision);
    public async Task RejectConsolidationOrders(string json,CancellationToken ct)
    {
        var root=RoutingInput.Parse(json);
        var ids=RoutingInput.Items(root["orders"]?["deliveries"]).Select(o=>RoutingInput.Text(o["id"])!).Select(id=>root["reporting"]?["orders"]?[id]?["nativeOrderId"]?.GetValue<long>()).Where(id=>id is not null).Select(id=>id!.Value).ToArray();
        if(ids.Length==0)return;
        await using var pg=await factory.OpenConnectionAsync(ct);
        var policies=await CustomerPolicies(pg,null,ct);
        if(!policies.Values.Any(p=>p.Consolidation))return;
        await using var db=await Open(ct);
        var customers=await db.QueryAsync<string>(new CommandDefinition("SELECT DISTINCT TRIM(OPS_ACCTID) FROM OPS_STOP_REC WHERE OPS_TYPE='D' AND OPS_ORDER_ID IN @ids",new{ids},cancellationToken:ct));
        if(customers.Any(c=>policies.GetValueOrDefault(c??"")?.Consolidation==true))
            throw new FormatException("This plan contains a consolidation customer's orders. Cancel it and refresh Orders so those orders accumulate on their customer manifest without PTV.");
    }
    public async Task<CustomerPlanning> ReadCustomerPlanning(string code,CancellationToken ct)
    {
        await ReadMaster("customers",code,ct);
        await using var db=await factory.OpenConnectionAsync(ct);
        return (await CustomerPolicies(db,null,ct)).GetValueOrDefault(code)??new();
    }
    public async Task<CustomerPlanning> SaveCustomerPlanning(string code,CustomerPlanning input,CancellationToken ct)
    {
        if(input.Consolidation)
        {
            var type=(await ReadFleet(ct)).Types.SingleOrDefault(t=>t.Code==input.EquipmentType&&!t.PowerOnly)
                ??throw new FormatException("Choose an active truck or trailer capacity template.");
            input.MaximumWeight=(decimal?)type.Weight;input.MaximumCubes=(decimal?)type.Cubes;
        }
        input.Validate();await ReadMaster("customers",code,ct);
        await using var db=await factory.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await Lock(db,tx,ct);
        var current=(await CustomerPolicies(db,tx,ct)).GetValueOrDefault(code);
        if((current?.Revision??0)!=input.Revision)throw new FormatException("Customer planning settings changed. Reopen the customer before saving.");
        var revision=input.Revision+1;
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO aurora_customer_planning(tenant_id,source_key,customer_code,settings,revision)
            VALUES(@TenantId,@SourceKey,@code,CAST(@json AS jsonb),@revision)
            ON CONFLICT(tenant_id,source_key,customer_code) DO UPDATE SET settings=excluded.settings,revision=excluded.revision
            """,new{tenant.TenantId,Options.SourceKey,code,json=JsonSerializer.Serialize(input),revision},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);input.Revision=revision;return input;
    }
}
