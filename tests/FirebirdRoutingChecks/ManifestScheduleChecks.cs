using Aurora.Contracts;
using Aurora.Integrations.Nova;

static class ManifestScheduleChecks
{
    public static void Run()
    {
        const string request = """
            {"reporting":{"orders":{"FB-10":{"nativeOrderId":10,"nativeStopId":100,"nativeStopKind":"D"},"FB-P-10":{"nativeOrderId":10,"nativeStopId":101,"nativeStopKind":"P"}}}}
            """;
        var arrival = new DateTimeOffset(2026, 11, 20, 23, 45, 0, TimeSpan.Zero);
        var pros = new[] {
            new ManifestProDto("FB-10",1,"Customer","","","","",null,null,null,"",900,32,-96,null,arrival,arrival.AddMinutes(30)),
            new ManifestProDto("FB-P-10",2,"Pickup","","","","",null,null,null,"",900,33,-96,null,arrival.AddHours(1),arrival.AddHours(2)) { WorkType="Pickup" }
        };
        var route = new RouteSummaryDto("TRUCK",2,2,0,0,0,0,0,"DAL",null,0,2,null,null,null,pros,[],arrival.AddHours(-1));
        NovaManifestOrder[] Orders() => [new() {Id=10,NativeStopId=100,Sequence=1},new() {Id=10,NativeStopId=101,Sequence=2}];
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test Central",TimeSpan.FromHours(-6),"Test Central","Test Central");
        var orders = Orders();
        FirebirdRoutingSource.ApplyManifestSchedule(orders,route,request,zone);
        Check(orders[0].PlannedArrival == arrival && orders[0].PlannedArrival?.Hour == 17,"saved arrival uses terminal local time");
        Check(orders[0].PlannedDeparture == arrival.AddMinutes(30),"departure retains overnight instant");
        Check(orders[1].PlannedArrival == arrival.AddHours(1),"pickup and delivery for the same order retain distinct times");
        Check(orders[0].PlannedRouteDeparture == route.Departure,"depot departure comes from saved route");
        orders=Orders(); orders[1].NativeStopId=999;
        FirebirdRoutingSource.ApplyManifestSchedule(orders,route,request,zone);
        Check(orders.All(o=>o.PlannedArrival is null),"changed membership suppresses entire stale schedule");
        orders=Orders(); orders[1].Sequence=3;
        FirebirdRoutingSource.ApplyManifestSchedule(orders,route,request,zone);
        Check(orders.All(o=>o.PlannedArrival is null),"changed sequence suppresses stale schedule");
        orders=[Orders()[0]];
        FirebirdRoutingSource.ApplyManifestSchedule(orders,route,request,zone);
        Check(orders[0].PlannedArrival is null,"removed orders suppress stale schedule");
        orders=Orders();
        FirebirdRoutingSource.ApplyManifestSchedule(orders,route with { Pros=pros.Select(p=>p with { Arrival=null,Departure=null }).ToArray() },request,zone);
        Check(orders.All(o=>o.PlannedArrival is null && o.PlannedDeparture is null),"missing times are never invented");
        Console.WriteLine("8 manifest schedule checks passed.");
    }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
}
