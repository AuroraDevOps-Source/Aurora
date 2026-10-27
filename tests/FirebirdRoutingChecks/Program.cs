using Aurora.Integrations.Nova;
using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using Npgsql;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var draftId = Guid.NewGuid();
Check(FirebirdRoutingSource.ExportKey(tenantId,draftId,"TRUCK") == FirebirdRoutingSource.ExportKey(tenantId,draftId,"TRUCK"), "stable retry identity");
Check(FirebirdRoutingSource.ExportKey(tenantId,draftId,"A") != FirebirdRoutingSource.ExportKey(tenantId,draftId,"B"), "distinct routes have distinct export identities");
Check(FirebirdRoutingSource.ExportKey(Guid.NewGuid(),draftId,"A") != FirebirdRoutingSource.ExportKey(tenantId,draftId,"A"), "export identities are tenant scoped");
Check(!FirebirdRoutingSource.HasCoordinates(0,0) && !FirebirdRoutingSource.HasCoordinates(null,null) && !FirebirdRoutingSource.HasCoordinates(91,10), "missing and invalid coordinates rejected");
Check(FirebirdRoutingSource.HasCoordinates(42,-71), "valid coordinates accepted");
Check(DataRequirements.Depot("MHT",0,0).Single().Code=="depot-coordinates", "unlocated depots have an actionable grid issue");
Check(DataRequirements.Depot("PWM",43,-70).Length==0, "corrected depots clear their issue");
Check(DataRequirements.HasCoordinates(0,15) && DataRequirements.HasCoordinates(15,0), "equator and prime meridian coordinates remain valid");
Check(!DataRequirements.HasCoordinates(double.NaN,10) && !DataRequirements.HasCoordinates(42,null), "nonfinite and incomplete coordinates are excluded");
Check(DataRequirements.Fleet("MHT",true,null,null,null,20000,1700).Single().ActionUrl=="aurora/terminals", "fleet depot issue links to Terminals");
Check(DataRequirements.Fleet("",false,null,null,null,0,null).Select(i=>i.Code).SequenceEqual(new[]{"fleet-terminal","fleet-weight","fleet-cubes"}), "fleet shows each missing planning requirement without duplicate depot errors");
Check(DataRequirements.Fleet("UNKNOWN",false,null,null,null,20000,1700).Single().Code=="unknown-terminal", "unknown fleet terminal is distinguished from missing coordinates");
Check(DataRequirements.Fleet("MHT",true,"14 LIBERTY DRIVE","LONDONDERRY","03053",20000,1700).Length==0, "fleet with terminal address and capacities needs no stored coordinates for its issue indicator");
Check(DataRequirements.Order(1,1,"MHT",true).Length==0, "missing delivery coordinates are resolved during planning instead of flagged as an order issue");
Check(DataRequirements.Order(2,0,"MHT",true).Single().Code=="delivery-count", "unsupported multiple deliveries are identified");
Check(DataRequirements.Order(1,0,"MHT",true).Length==0, "ready orders have no issues");

var truckPlan=FirebirdRoutingSource.PlannedEquipment("329", [new(){NativeId=192,Unit="329",Kind="TRK",Description="Truck"}]);
Check(truckPlan.Trailer==192 && truckPlan.Tractor is null,"straight truck uses internal ID in planned trailer");
var trailerPlan=FirebirdRoutingSource.PlannedEquipment("4086923", [new(){NativeId=294,Unit="4086923",Kind="TRL",Description="Trailer"}]);
Check(trailerPlan.Trailer==294 && trailerPlan.Tractor is null,"trailer uses internal ID in planned trailer");
var tractorPlan=FirebirdRoutingSource.PlannedEquipment("31471", [new(){NativeId=265,Unit="31471",Kind="TRK",Description="Tractor"}]);
Check(tractorPlan.Tractor==265 && tractorPlan.Trailer is null,"tractor never populates planned trailer");
foreach(var invalid in new FirebirdRoutingSource.NovaFleetRow[][] {
    [], [new(){NativeId=1,Unit="329",Kind="DRV"}],
    [new(){NativeId=1,Unit="329"},new(){NativeId=2,Unit="329"}], [new(){NativeId=192,Unit="other"}] })
{
    try { FirebirdRoutingSource.PlannedEquipment("329",invalid); throw new Exception("Invalid equipment accepted"); }
    catch(FormatException) { Check(true,"missing, driver, duplicate or mismatched equipment rejected"); }
}

// Explicit opt-in: refresh writes only the restored local Aurora database. Firebird's
// business writes are always rolled back; its nontransactional generators can advance.
if (Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG") is { Length: > 0 } configPath)
{
    var config = JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(configPath))!.ToDictionary(p=>p.Key,p=>p.Value.ToString());
    var pgSettings = new NpgsqlConnectionStringBuilder(config["ConnectionStrings:Aurora"]);
    if (pgSettings.Host != "127.0.0.1" || pgSettings.Port != 55432 || pgSettings.Database != "aurora_test")
        throw new Exception("Live checks require the local restored Aurora test database.");
    var options = new FirebirdRoutingOptions
    {
        Enabled=true, TenantId=Guid.Parse(config["FirebirdRouting:TenantId"]),
        SourceKey=config["FirebirdRouting:SourceKey"], ConnectionString=config["FirebirdRouting:ConnectionString"],
        ReadyStatus=int.Parse(config["FirebirdRouting:ReadyStatus"]), TimeZoneId=config["FirebirdRouting:TimeZoneId"]
    };
    var fbSettings = new FbConnectionStringBuilder(options.ConnectionString);
    if (fbSettings.DataSource != "172.23.0.5" || !fbSettings.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase))
        throw new Exception("Live checks require the explicitly requested wayl_test source.");
    await using var dataSource=NpgsqlDataSource.Create(pgSettings.ConnectionString);
    var tenant=new TestTenant(options.TenantId);
    var factory=new NpgsqlConnectionFactory(dataSource,tenant);
    var source=new FirebirdRoutingSource(factory,tenant,Options.Create(options));
    var settingsKey="consolidation-check-"+Guid.NewGuid().ToString("N");
    var settingsSource=new FirebirdRoutingSource(factory,tenant,Options.Create(new FirebirdRoutingOptions {Enabled=true,TenantId=options.TenantId,SourceKey=settingsKey,ConnectionString=options.ConnectionString}));
    try
    {
        var nodes=await settingsSource.ReadTerminals(default);
        var marked=nodes[0]; marked.IsConsolidation=true; marked.ServiceArea="00001";
        var saved=await settingsSource.SaveTerminalRouting(marked.Code,marked,default);
        var reread=(await settingsSource.ReadTerminals(default)).Single(n=>n.Code==marked.Code);
        Check(reread.IsConsolidation && reread.ServiceArea=="00001" && reread.Revision==1,"native terminal routing settings persist in source-scoped Aurora storage");
        try {await settingsSource.SaveTerminalRouting(marked.Code,marked,default);throw new Exception("Stale routing settings accepted");}
        catch(FormatException){Check(true,"stale terminal routing save rejected");}
        var secondNode=nodes[1];
        saved.OutboundRoutes=[new(){DestinationTerminalId=secondNode.Id}];
        saved=await settingsSource.SaveTerminalRouting(saved.Code,saved,default);
        var lanes=(await settingsSource.ReadTerminals(default)).Single(n=>n.Id==saved.Id).OutboundRoutes;
        Check(lanes.Count==1 && lanes[0].DestinationTerminalId==secondNode.Id && lanes[0].ViaTerminalId is null,"origin destination routing rule persists");
        saved.OutboundRoutes.Add(new(){DestinationTerminalId=secondNode.Id});
        try {await settingsSource.SaveTerminalRouting(saved.Code,saved,default);throw new Exception("Duplicate lane accepted");}
        catch(FormatException){Check(true,"duplicate terminal lane rejected");}

    }
    finally
    {
        await using var cleanup=await factory.OpenConnectionAsync();
        await cleanup.ExecuteAsync("DELETE FROM aurora_terminal_routing WHERE tenant_id=@TenantId AND source_key=@settingsKey",new{TenantId=options.TenantId,settingsKey});
    }
    var disabled=new FirebirdRoutingSource(factory,new TestTenant(Guid.NewGuid()),Options.Create(options));
    Check(!disabled.Enabled,"another tenant cannot access the Firebird connection");
    try { await disabled.Synchronize(default); throw new Exception("Cross-tenant refresh was allowed"); }
    catch(FormatException) { Check(true,"cross-tenant refresh rejected before connecting"); }
    try { await disabled.ReadManifests(null,null,1,false,default); throw new Exception("Cross-tenant manifest read was allowed"); }
    catch(FormatException) { Check(true,"cross-tenant manifest read rejected before connecting"); }
    var nativeManifests=await source.ReadManifests(null,null,1,false,default);
    Check(nativeManifests.Manifests.All(m=>m.StatusId==1 && m.CreatedAt is {} created && DateOnly.FromDateTime(created)==nativeManifests.Today),"Nova defaults filter creation day and Unassigned status");
    foreach(var manifest in nativeManifests.Manifests.Take(2))
    {
        var members=await source.ReadManifestOrders(manifest.Id,default);
        Check(members.Select(m=>m.Id).Distinct().Count()==manifest.OrderCount,"Nova detail membership matches native manifest count");
        if(members.FirstOrDefault() is {} member)
        {
            var detail=await source.ReadOrderDetails(manifest.Id,member.Id,default);
            Check(detail.Stops.Length>0,"Nova order detail includes native pickup/delivery parties");
            try { await source.ReadOrderDetails(-1,member.Id,default); throw new Exception("Unrelated manifest accepted"); }
            catch(KeyNotFoundException) { Check(true,"order detail requires native manifest membership"); }
        }
    }
    try { await source.ReadManifests(nativeManifests.Today,nativeManifests.Today.AddDays(-1),1,false,default); throw new Exception("Reversed date range accepted"); }
    catch(FormatException) { Check(true,"reversed manifest creation range rejected"); }
    var first=await source.Synchronize(default);
    var second=await source.Synchronize(default);
    Check(first.Orders>0 && first.Orders==second.Orders && first.SourceIds.SequenceEqual(second.SourceIds),"repeat refresh is stable");
    Console.WriteLine($"Refreshed {second.Orders} Nova orders across {second.Terminals.Length} terminals.");
    foreach(var warning in second.Warnings) Console.WriteLine(warning);
    await using var pg=await factory.OpenConnectionAsync();
    var count=await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_order WHERE tenant_id=@TenantId AND source_id=ANY(@ids) AND status='ReadyToRoute' AND deleted_at IS NULL",new{TenantId=options.TenantId,ids=second.SourceIds});
    Check(count==second.Orders,"refresh does not duplicate orders");
    var json=await pg.QuerySingleAsync<string>("SELECT request::text FROM aurora_order_source WHERE tenant_id=@TenantId AND id=@id",new{TenantId=options.TenantId,id=second.SourceIds[0]});
    var root=RoutingInput.Parse(json);
    var ids=OrderWorkspace.OrderIds(json).Take(2).ToArray();
    var pros=ids.Select((id,i)=>new ManifestProDto(id,i+1,"Rollback check","","","","",0,0,0,"",0,42,-71)).ToArray();
    var route=new RouteSummaryDto("ROLLBACK_CHECK_ONLY",ids.Length,ids.Length,0,0,0,0,0,second.Terminals[0],null,0,ids.Length,0,0,0,pros,[],DateTimeOffset.Now);
    Check(FirebirdRoutingSource.ExportFingerprint(draftId,[route]) != FirebirdRoutingSource.ExportFingerprint(Guid.NewGuid(),[route]),"a different completed result cannot reuse the export receipt");
    var manifests=await source.Export(draftId,Guid.NewGuid(),json,[route],default,rollbackOnly:true);
    Check(manifests.Count==1 && manifests[route.Vehicle]>0,"real Nova header, routing stop and membership SQL succeeds in rollback transaction");
    await using var fb=new FbConnection(options.ConnectionString); await fb.OpenAsync();
    Check(await fb.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_EXTERNAL_REF STARTING WITH @prefix",new{prefix=FirebirdRoutingSource.ExportPrefix(options.TenantId,draftId)})==0,"rollback leaves no test manifest in Nova");
    var transferRoot=RoutingInput.Parse(json);
    var origin=RoutingInput.Text(transferRoot["reporting"]?["terminal"])!;
    var receiving=second.Terminals.First(t=>t!=origin);
    transferRoot["reporting"]!["lineHaulTo"]=receiving;
    transferRoot["reporting"]!["lineHaulEndLocation"]="NODE-"+receiving;
    var transferRoute=route with { RouteStops=new[]{new RouteStopDto("DEPOT",42,-71,null,null,true,Array.Empty<string>()),new RouteStopDto("NODE-"+receiving,43,-70,null,null,false,ids)} };
    var transferDraft=Guid.NewGuid();
    var transferManifests=await source.Export(transferDraft,Guid.NewGuid(),transferRoot.ToJsonString(),[transferRoute],default,rollbackOnly:true);
    Check(transferManifests.Count==1,"Nova line haul header, two terminal routing stops and receiving-terminal membership verified before rollback");
    Check(await fb.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_EXTERNAL_REF STARTING WITH @prefix",new{prefix=FirebirdRoutingSource.ExportPrefix(options.TenantId,transferDraft)})==0,"line haul rollback leaves no demo manifest");
    transferRoot["reporting"]!["transferKind"]=PlanningBatch.Consolidation;
    var consolidationDraft=Guid.NewGuid();
    var consolidationManifests=await source.Export(consolidationDraft,Guid.NewGuid(),transferRoot.ToJsonString(),[transferRoute],default,rollbackOnly:true);
    Check(consolidationManifests.Count==1,"native consolidation manifest and terminal membership export succeeds before rollback");
    Check(await fb.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_EXTERNAL_REF STARTING WITH @prefix",new{prefix=FirebirdRoutingSource.ExportPrefix(options.TenantId,consolidationDraft)})==0,"consolidation rollback leaves no manifest");
    root["reporting"]!["orders"]![ids[0]]!["nativeStopId"] = long.MaxValue;
    try { await source.Export(Guid.NewGuid(),Guid.NewGuid(),root.ToJsonString(),[route],default,rollbackOnly:true); throw new Exception("Stale native identity was accepted"); }
    catch(FormatException) { Check(true,"stale or foreign delivery stop fails before manifest creation"); }
}
Console.WriteLine($"PASS {checks} Firebird routing checks.");
sealed class TestTenant(Guid id):ITenantContext
{
    public Guid? TenantIdOrNull => id;
    public Guid? UserIdOrNull => null;
}
