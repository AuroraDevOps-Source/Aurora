using System.Globalization;
using System.Text.Json;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Routing;

public static class RoadRoutingEndpoint
{
    public static async Task<IResult> Calculate(RoadRouteRequestDto request, IOptions<PtvSettings> settings, IHttpClientFactory clients, CancellationToken ct)
    {
        if (request.Points is null || request.Points.Count is < 2 or > 500 ||
            request.Points.Any(p => p is null || !double.IsFinite(p.Latitude) || !double.IsFinite(p.Longitude) || Math.Abs(p.Latitude) > 90 || Math.Abs(p.Longitude) > 180) ||
            string.IsNullOrWhiteSpace(request.Profile) || request.Profile.Length > 100)
            return Results.BadRequest(new ApiErrorDto("Provide valid stops and a routing profile."));
        if (string.IsNullOrWhiteSpace(settings.Value.ApiKey))
            return Results.Json(new ApiErrorDto("PTV road routing is not configured."), statusCode: 503);

        using var http = clients.CreateClient("PtvRoadRouting");
        http.DefaultRequestHeaders.Add("ApiKey", settings.Value.ApiKey);
        var points = new List<RoadPointDto>();
        var directions = new List<RoadDirectionDto>();
        try
        {
            // Overlapping chunks preserve the manifest order and keep URLs bounded.
            for (var start = 0; start < request.Points.Count - 1; start += 19)
            {
                var query = string.Join("&", request.Points.Skip(start).Take(20).Select(p =>
                    "waypoints=" + Uri.EscapeDataString(p.Latitude.ToString(CultureInfo.InvariantCulture) + "," + p.Longitude.ToString(CultureInfo.InvariantCulture) + ";includeLastMeters")));
                using var response = await http.GetAsync("https://api.myptv.com/routing/v1/routes?" + query +
                    "&results=POLYLINE,MANEUVER_EVENTS&options[language]=en&options[trafficMode]=AVERAGE&profile=" + Uri.EscapeDataString(request.Profile), ct);
                if (!response.IsSuccessStatusCode)
                    return Results.Json(new ApiErrorDto("PTV could not load road directions. Check Routing API access and the vehicle profile, then retry."), statusCode: 502);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (body.RootElement.TryGetProperty("violated", out var violated) && violated.GetBoolean())
                    return Results.Json(new ApiErrorDto("PTV reported road-access restrictions. The optimized plan is retained; bird’s-eye view remains available.", [$"Profile: {request.Profile}. Route points {start + 1}–{Math.Min(start + 20, request.Points.Count)} include a restricted road connection. Review delivery/depot coordinates and the vehicle profile before using road directions."]), statusCode: 422);
                using var geometry = JsonDocument.Parse(body.RootElement.GetProperty("polyline").GetString()!);
                foreach (var pair in geometry.RootElement.GetProperty("coordinates").EnumerateArray())
                    points.Add(new RoadPointDto(pair[1].GetDouble(), pair[0].GetDouble()));
                if (body.RootElement.TryGetProperty("events", out var events))
                    foreach (var item in events.EnumerateArray())
                        if (item.TryGetProperty("maneuver", out var maneuver))
                            directions.Add(new RoadDirectionDto(maneuver.GetProperty("description").GetString() ?? "Continue",
                                item.GetProperty("latitude").GetDouble(), item.GetProperty("longitude").GetDouble()));
            }
            if (points.Count < 2) throw new JsonException();
            return Results.Ok(new RoadRouteDto(points, directions));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Json(new ApiErrorDto("Road directions timed out. Please retry."), statusCode: 504);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Results.Json(new ApiErrorDto("Road directions are unavailable. Please retry."), statusCode: 502);
        }
    }
}
