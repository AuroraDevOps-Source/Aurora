using Aurora.Api.Features.Routing;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

var settings = Options.Create(new PtvSettings { ApiKey = "test-key" });
var handler = new FakeRouting();
var factory = new FakeFactory(handler);
var request = new RoadRouteRequestDto([new(39, -94), new(40, -95)], "USA_5_DELIVERY");
void Check(bool pass, string name) { if (!pass) throw new Exception(name); }
async Task<IResult> Run(RoadRouteRequestDto value) => await RoadRoutingEndpoint.Calculate(value, settings, factory, default);
var result = await Run(request);
Check(((IStatusCodeHttpResult)result).StatusCode == 200, "success");
var road = (RoadRouteDto)((IValueHttpResult)result).Value!;
Check(road.Points[0] == new RoadPointDto(39, -94), "GeoJSON longitude/latitude conversion");
Check(road.Directions.Single().Description == "Turn right", "maneuver instruction");
Check(handler.Urls[0].Contains("profile=USA_5_DELIVERY") && handler.Urls[0].Contains("MANEUVER_EVENTS"), "profile and instructions requested");
Check(handler.Urls[0].IndexOf("39%2C-94", StringComparison.Ordinal) < handler.Urls[0].IndexOf("40%2C-95", StringComparison.Ordinal), "stop order retained");
result = await Run(request with { Points = [new(91, 0), new(40, -95)] });
Check(((IStatusCodeHttpResult)result).StatusCode == 400 && handler.Urls.Count == 1, "invalid coordinates rejected before provider call");
handler.Violated = true;
result = await Run(request);
Check(((IStatusCodeHttpResult)result).StatusCode == 422, "restricted routes rejected");
handler.Violated = false;
handler.Fail = true;
result = await Run(request);
Check(((IStatusCodeHttpResult)result).StatusCode == 502, "provider failure is actionable");
handler.Fail = false;
handler.Urls.Clear();
result = await Run(request with { Points = Enumerable.Range(0, 21).Select(i => new RoadPointDto(39 + i * .01, -94)).ToList() });
Check(((IStatusCodeHttpResult)result).StatusCode == 200 && handler.Urls.Count == 2, "long routes split into bounded chunks");
Check(handler.Urls.All(url => url.Contains("39.19%2C-94")), "chunk boundary overlaps without losing a leg");
Console.WriteLine("10 road routing checks passed.");

sealed class FakeFactory(FakeRouting handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
sealed class FakeRouting : HttpMessageHandler
{
    public List<string> Urls { get; } = [];
    public bool Violated { get; set; }
    public bool Fail { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Urls.Add(request.RequestUri!.AbsoluteUri);
        return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                violated = Violated,
                polyline = "{\"type\":\"LineString\",\"coordinates\":[[-94,39],[-95,40]]}",
                events = new[] { new { latitude = 39d, longitude = -94d, maneuver = new { description = "Turn right" } } }
            }))
        });
    }
}
