using System.Net;
using System.Net.Http.Json;
using Aurora.Contracts;
using PlannerPreview;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<PreviewHttp>();
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<PreviewHttp>(), false) { BaseAddress = new Uri("http://preview.invalid/") });
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Preview>().AddInteractiveServerRenderMode();
app.Run();

public sealed class PreviewHttp : HttpMessageHandler
{
    private EquipmentCatalogDto catalog = System.Text.Json.JsonSerializer.Deserialize<EquipmentCatalogDto>(File.ReadAllText("../../src/Aurora.Client/wwwroot/samples/equipment-catalog.json"))!;
    private readonly Dictionary<Guid, (StartPlanningDto Input, DateTimeOffset Start, bool Stopped)> sessions = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("appointment-test.json"))
            return new(HttpStatusCode.OK) { Content = new StringContent(await File.ReadAllTextAsync("../../src/Aurora.Client/wwwroot/samples/appointment-test.json", ct)) };
        if (request.RequestUri.AbsolutePath.Contains("/sessions"))
        {
            var path = request.RequestUri.AbsolutePath;
            if (path.EndsWith("/sessions"))
            {
                var input = (await request.Content!.ReadFromJsonAsync<StartPlanningDto>(ct))!;
                sessions[input.Id] = (input, DateTimeOffset.UtcNow, false);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PlanningSessionDto(input.Id, "QUEUING", input.FileName, input.RequestJson, 0, [], null)) };
            }
            var parts = path.Split('/');
            var id = Guid.Parse(parts[5]);
            if (!sessions.TryGetValue(id, out var session)) return new(HttpStatusCode.NotFound);
            if (path.EndsWith("/stop")) { session.Stopped = true; sessions[id] = session; }
            var elapsed = (DateTimeOffset.UtcNow - session.Start).TotalSeconds;
            var finished = session.Stopped || elapsed >= 45;
            var sample = new PlanningSampleDto(DateTimeOffset.UtcNow, 2, 1, 2, 240 - Math.Min(20, elapsed));
            var summary = new RunSummaryDto("SUCCEEDED", 2, 2, 2, 1, 30, 3600, 220, 0, 3, 4, 110, 15, 1, "mixed");
            var raw = "{\"status\":\"SUCCEEDED\",\"routes\":[]}";
            var result = finished ? new OptimizationResultDto(session.Input.FileName, id.ToString(), DateTimeOffset.UtcNow, "DEMO", "SUCCEEDED", 200, "200 OK", elapsed, false, summary, [], [], [], [], [], [], raw, null, null) : null;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PlanningSessionDto(id, finished ? "SUCCEEDED" : "RUNNING", session.Input.FileName, session.Input.RequestJson, elapsed, [sample], result)) };
        }
        if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.EndsWith("types"))
        {
            var item = (await request.Content!.ReadFromJsonAsync<EquipmentTypeDto>(ct))!;
            catalog = catalog with { Types = catalog.Types.Where(x => x.Code != item.Code).Append(item).ToArray() };
        }
        if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.EndsWith("units"))
        {
            var item = (await request.Content!.ReadFromJsonAsync<EquipmentUnitDto>(ct))!;
            catalog = catalog with { Units = catalog.Units.Where(x => x.Id != item.Id).Append(item).ToArray() };
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(catalog) };
    }
}
