using System.Net;
using System.Net.Http.Json;
using Aurora.Client;
using Aurora.Contracts;

var now = new DateTimeOffset(2026,11,3,15,0,0,TimeSpan.Zero);
var date = new DateTime(2026,11,3);
DispatchItem Item(DateTime? scheduled=null,string lane="Unassigned") => new("o:1","1001","Customer","KC",scheduled,null,"","Ready",lane,"/orders");
void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS "+message);}
var late=Item(date) with { Due=new DateTime(2026,11,3,9,59,0),Zone="America/New_York" };
Check(late.Late(now),"Appointment compares against source time zone");
Check(!(late with { Due=new DateTime(2026,11,3,10,0,0) }).Late(now),"Appointment boundary is not late");
Check(!(late with { Lane="Complete" }).Late(now),"Completed work is never late");
Check(!Item(date.AddDays(-1)).Late(now),"Scheduled date alone does not claim an appointment is late");
Check(DispatchBoardRules.InScope(Item(date.AddDays(-2)),date,true),"Carry-over includes earlier unfinished work");
Check(!DispatchBoardRules.InScope(Item(date.AddDays(-2)),date,false),"Date-only filter excludes carry-over");
Check(!DispatchBoardRules.InScope(Item(date.AddDays(-2),"Complete"),date,true),"Prior completed work excluded");
Check(!DispatchBoardRules.InScope(Item(date.AddDays(1)),date,true),"Future work excluded");
Check(DispatchBoardRules.InScope(Item(),date,false),"Undated work remains visible");
Check(DispatchBoardRules.NativeLane("ENR")=="Dispatched" && DispatchBoardRules.NativeLane("CPLT")=="Complete","Native status codes classify work");
Check(DispatchBoardRules.AssignmentIssues("","").Length==2,"Missing assignments become explicit blockers");
Check(DispatchBoardRules.NativeOrderClosed(new(1,"1","","","Void"){StatusCode="VOID"}),"Live Nova VOID orders excluded");
Check(DispatchBoardRules.NativeOrderClosed(new(1,"1","","","Custom complete label"){StatusCode="CPLT"}),"Native completion uses code rather than display label");
Check(DispatchBoardRules.NativeOrderIssues(new(1,"1","","","Unknown")).Length==1,"Unknown native status creates a review blocker");
Check(DispatchBoardRules.NativeOrderIssues(new(1,"1","","","Ready"){StatusCode="RTR"}).Length==0,"Known native status creates no false blocker");

using var handler=new Fixture();
using var http=new HttpClient(handler){BaseAddress=new Uri("https://fixture.invalid/")};
var snapshot=await new DispatchBoardLoader(http).Load(true,date,true,default);
Check(snapshot.Items.Count==3,"Assigned orders and completed orders do not duplicate work cards");
Check(snapshot.Items.Single(i=>i.Number=="open").Lane=="Unassigned","Open order stays unassigned");
Check(snapshot.Items.Single(i=>i.Number=="draft").Lane=="Planned","Draft-held order is planned");
var route=snapshot.Items.Single(i=>i.Number=="M1");
Check(route.Due==handler.Due.LocalDateTime,"Completed appointments do not make a route late");
Check(route.Stops.Length==2,"Manifest map includes known stops");
Check(route.Issues.Contains("Driver unassigned"),"Route assignment blockers are shown");
Check(snapshot.Resources.Count==1,"Fleet directory loads");
handler.Fail=true;
try{await new DispatchBoardLoader(http).Load(true,date,true,default);throw new Exception("Failure was hidden");}
catch(FormatException){Console.WriteLine("PASS Failed core source rejects snapshot rather than claiming empty work");}

using var nativeHandler=new NativeFixture();
using var nativeHttp=new HttpClient(nativeHandler){BaseAddress=new Uri("https://fixture.invalid/")};
var native=await new DispatchBoardLoader(nativeHttp).Load(false,date,true,default);
Check(nativeHandler.Pages==2,"Native order pagination is exhausted");
Check(native.Items.Count==3,"Native assigned and completed orders are excluded from unassigned lane");
Check(native.Items.Single(i=>i.Number=="N2").Lane=="Planned","Native open-plan reservation is preserved");
var nativeRoute=native.Items.Single(i=>i.Number=="10");
Check(nativeRoute.Lane=="Dispatched" && nativeRoute.DriverId==7,"Native dispatch state and driver identity retained");
Check(nativeRoute.Due==date.AddHours(17),"Native completed stops excluded from overdue calculation");
Check(nativeRoute.Stops.Length==2,"Native stop map retains route sequence");
Check(!nativeHandler.ClosedRead,"Prior closed manifests do not trigger dispatch detail requests");

sealed class NativeFixture:HttpMessageHandler
{
    public int Pages;
    public bool ClosedRead;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var path=request.RequestUri!.AbsolutePath;
        var date=new DateTime(2026,11,3);
        object payload;
        if(path.EndsWith("records/orders"))
        {
            Pages++;
            payload=request.RequestUri.Query.Contains("page=0")
                ? new NovaRecordPage([new(1,"N1","One","KC","Ready"){AppointmentFrom=date},new(3,"N3","Assigned","KC","Assigned"){ManifestNumbers=[10]},new(4,"N4","Done","KC","Custom done status"){Completed=true}],true)
                : new NovaRecordPage([new(2,"N2","Two","KC","Ready"){AppointmentFrom=date}],false);
        }
        else if(path=="/api/v1/tms/orders")payload=new TmsOrder[]{new(){NativeOrderId=2,OptimizationDraftId=Guid.NewGuid()}};
        else if(path.EndsWith("/manifests"))payload=new NovaManifestList(DateOnly.FromDateTime(date),[],[
            new(){Id=10,ManifestDate=date,StatusCode="ENR",Truck="T1",Driver="D1",Terminal="KC"},
            new(){Id=11,ManifestDate=date.AddDays(-1),StatusCode="CPLT"}]);
        else if(path.EndsWith("/10/dispatch"))payload=new NovaDispatchDocument(10,"rev","In transit",date,"KC",7,8,null,null,false,false,null,[new(7,"D1 · Alex","KC",null)],[],[]){StatusCode="ENR",TimeZone="America/New_York"};
        else if(path.EndsWith("/10/orders"))payload=new NovaManifestOrder[]{new(){Pro="1",Completed=true,AppointmentTo=date.AddDays(-1),Latitude=39,Longitude=-94,Sequence=1},new(){Pro="2",AppointmentTo=date.AddHours(17),Latitude=40,Longitude=-95,Sequence=2}};
        else if(path.EndsWith("/drivers"))payload=new DriverDirectory([],[],[]);
        else if(path.EndsWith("records/fleet"))payload=new NovaRecordPage([],false);
        else {ClosedRead=true;throw new Exception("Unexpected request "+path);}
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(payload,payload.GetType())});
    }
}

sealed class Fixture:HttpMessageHandler
{
    public bool Fail;
    public DateTimeOffset Due=new(2026,11,3,16,0,0,TimeSpan.Zero);
    readonly Guid manifest=Guid.NewGuid();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        if(Fail)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new ApiErrorDto("Unavailable"))});
        object payload=request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/tms/orders" => new TmsOrder[]{new(){Id="open"},new(){Id="draft",OptimizationDraftId=Guid.NewGuid()},new(){Id="done",Status="Delivered"},new(){Id="assigned",ManifestId=manifest,AppointmentTo=Due,Latitude=39,Longitude=-94},new(){Id="complete-stop",ManifestId=manifest,Status="Delivered",AppointmentTo=Due.AddDays(-1),Latitude=40,Longitude=-95}},
            "/api/v1/tms/manifests" => new TmsManifest[]{new(){Id=manifest,Number="M1",Vehicle="T1",Status="Dispatched"}},
            "/api/v1/tms/terminals" => Array.Empty<TerminalDto>(),
            "/api/v1/routing/equipment" => new EquipmentCatalogDto([], [new(){Id="T1",UseForRouting=true}]),
            "/api/v1/tms/drivers" => Array.Empty<NovaDriver>(),
            _ => throw new Exception("Unexpected request")
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(payload,payload.GetType())});
    }
}
