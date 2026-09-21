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
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
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
