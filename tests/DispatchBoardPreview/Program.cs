using System.Net;
using System.Net.Http.Json;
using Aurora.Contracts;
using DispatchBoardPreview;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped(_=>new HttpClient(new PreviewData()){BaseAddress=new Uri("http://preview.invalid")});
var app=builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Preview>().AddInteractiveServerRenderMode();
app.Run();

sealed class PreviewData:HttpMessageHandler
{
    static readonly Guid terminal=Guid.NewGuid();
    static readonly Guid[] manifests=[Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var today=DateTime.Today;
        var orders=new List<TmsOrder>();
        string[] names=["Acme Distribution","Midwest Medical Supply","River Market Foods","Prairie Industrial","Westside Packaging","Central Supply Co."];
        for(var i=0;i<6;i++)orders.Add(new(){Id=$"10482{i}",Customer=names[i],TerminalId=terminal,TerminalCode=i==5?"DEN":"KC",ScheduledAt=today,AppointmentTo=i==0?DateTimeOffset.Now.AddHours(-2):null,Latitude=39.05+i*.025,Longitude=-94.64+i*.02,Status=i==1?"AwaitingAppointment":"ReadyToRoute"});
        orders.Add(new(){Id="104810",Customer="Northland Foods",ManifestId=manifests[1],Status="Dispatched",ScheduledAt=today,AppointmentTo=DateTimeOffset.Now.AddMinutes(-35),Latitude=39.27,Longitude=-94.56,StopNumber=1});
        orders.Add(new(){Id="104811",Customer="Summit Hardware",ManifestId=manifests[0],ScheduledAt=today,Latitude=39.17,Longitude=-94.46,StopNumber=1});
        object payload=request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/tms/orders"=>orders,
            "/api/v1/tms/manifests"=>new TmsManifest[]{
                new(){Id=manifests[0],Number="KC-021",ManifestDate=DateOnly.FromDateTime(today),Vehicle="TRK-204",OrderCount=1,Details=new(){OriginTerminal="KC",DestinationTerminal="North Kansas City"}},
                new(){Id=manifests[1],Number="KC-018",ManifestDate=DateOnly.FromDateTime(today),Status="En Route",Vehicle="TRK-108",OrderCount=1,Details=new(){OriginTerminal="KC",DestinationTerminal="Northland",Driver="Alex Morgan",DriverId=1}},
                new(){Id=manifests[2],Number="KC-017",ManifestDate=DateOnly.FromDateTime(today),Status="Complete",Vehicle="TRK-112",OrderCount=3,Details=new(){OriginTerminal="KC",DestinationTerminal="Downtown",Driver="Jordan Lee",DriverId=2}}},
            "/api/v1/tms/terminals"=>new TerminalDto[]{new(){Id=terminal,Code="KC",Name="Kansas City",Latitude=39.124,Longitude=-94.555}},
            "/api/v1/tms/drivers"=>new NovaDriver[]{new(){Id=1,FirstName="Alex",LastName="Morgan",Terminal="KC"},new(){Id=2,FirstName="Jordan",LastName="Lee",Terminal="KC"}},
            "/api/v1/routing/equipment"=>new EquipmentCatalogDto([],new[]{"TRK-108","TRK-112","TRK-204","TRK-208"}.Select(id=>new EquipmentUnitDto{Id=id,Terminal="KC",UseForRouting=true}).ToArray()),
            _=>throw new Exception("Unexpected preview request: "+request.RequestUri.AbsolutePath)
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(payload,payload.GetType())});
    }
}
