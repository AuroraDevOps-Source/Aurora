using System.Globalization;
using System.Text.Json;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using Microsoft.Extensions.Options;

namespace Aurora.Modules.Routing;

public sealed class MasterAddressLookup(HttpClient client, IOptions<PtvSettings> settings)
{
    public async Task<(NovaMasterEdit Edit, string? Warning)> Resolve(string kind, NovaMasterEdit input, NovaMasterDocument? current, CancellationToken ct)
    {
        var prefix = kind == "terminals" ? "LM_" : "AC_";
        if (kind == "terminals" && input.UseCoordinates)
        {
            double? Number(string key) => double.TryParse(input.Values.GetValueOrDefault(prefix + key), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
            if (!DataRequirements.HasCoordinates(Number("LATITUDE"), Number("LONGITUDE")))
                throw new FormatException("Enter valid latitude and longitude together.");
            return (input, null);
        }
        var (values, warning) = await ResolveAddress(prefix, kind == "terminals" ? "COUNTRY" : "COUNTRY_CD", input.Values,
            current?.Fields.ToDictionary(f => f.Key, f => f.Value), ct);
        return (input with { Values = values }, warning);
    }

    public async Task<TerminalAddressLookupResult> LookUpTerminal(NovaMasterEdit input, CancellationToken ct)
    {
        var (values, warning) = await ResolveAddress("LM_", "COUNTRY", input.Values, null, ct);
        return new(values.GetValueOrDefault("LM_LATITUDE"), values.GetValueOrDefault("LM_LONGITUDE"),
            warning?.Replace("Saved, but ", "Address lookup failed: ").Replace("then save again", "then try again").Replace("Save again to retry.", "Try again."));
    }

    public Task<TerminalAddressLookupResult> LookUpOrderAddress(Dictionary<string,string?> values, CancellationToken ct) =>
        LookUpTerminal(new NovaMasterEdit("", values.Where(p=>p.Key.StartsWith("OPS_")).ToDictionary(p=>"LM_"+p.Key[4..],p=>p.Value)),ct);

    public async Task<(NovaRecordEdit Edit, string[] Warnings)> ResolveOrder(NovaRecordEdit input, NovaRecordDocument? current, CancellationToken ct)
    {
        var parts = input.Values.ToDictionary(p => p.Key, p => new Dictionary<string, string?>(p.Value));
        var warnings = new List<string>();
        foreach (var part in parts.Where(p => p.Key != "header").ToArray())
        {
            if (input.CoordinateParts.Contains(part.Key))
            {
                double? Number(string key) => double.TryParse(part.Value.GetValueOrDefault("OPS_"+key),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:null;
                if (!DataRequirements.HasCoordinates(Number("LATITUDE"),Number("LONGITUDE")))
                    throw new FormatException("Enter valid latitude and longitude together for " + part.Key + ".");
                continue;
            }
            var old = current?.Parts.Single(p => p.Key == part.Key);
            var (values, warning) = await ResolveAddress("OPS_", "COUNTRY", part.Value,
                old?.Fields.ToDictionary(f => f.Key, f => f.Value), ct);
            parts[part.Key] = values;
            if (warning is not null) warnings.Add($"{old?.Title ?? (part.Key == "pickup" ? "Shipper / pickup" : "Consignee / delivery")}: {warning}");
        }
        return (input with { Values = parts }, warnings.ToArray());
    }

    private async Task<(Dictionary<string, string?> Values, string? Warning)> ResolveAddress(string prefix, string countryKey,
        Dictionary<string, string?> input, Dictionary<string, string?>? old, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var values = new Dictionary<string, string?>(input);
        string? Get(string key) => values.GetValueOrDefault(prefix + key)?.Trim();
        var addressKeys = new[] { "ADDR1", "ADDR2", "ADDR3", "CITY", "STATE", "ZIP", countryKey };
        var addressChanged = old is null || addressKeys.Any(k => !string.Equals(Get(k), old.GetValueOrDefault(prefix + k)?.Trim(), StringComparison.OrdinalIgnoreCase));
        double? Number(string key) => double.TryParse(old?.GetValueOrDefault(prefix + key), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
        // Coordinates are server-managed. Keep the persisted point only while its address is unchanged.
        if (!addressChanged && DataRequirements.HasCoordinates(Number("LATITUDE"), Number("LONGITUDE")))
        {
            values[prefix + "LATITUDE"] = old![prefix + "LATITUDE"];
            values[prefix + "LONGITUDE"] = old[prefix + "LONGITUDE"];
            return (values, null);
        }
        values[prefix + "LATITUDE"] = null;
        values[prefix + "LONGITUDE"] = null;
        const string correction = "Check the street address, city, state and ZIP, then save again.";
        if (string.IsNullOrWhiteSpace(Get("ADDR1")) || (string.IsNullOrWhiteSpace(Get("CITY")) && string.IsNullOrWhiteSpace(Get("ZIP"))))
            return (values, "Saved, but the address is incomplete. " + correction);
        if (string.IsNullOrWhiteSpace(settings.Value.ApiKey)) return (values, "Saved, but automatic address lookup is not configured. Contact your administrator.");
        try
        {
            var address = string.Join(", ", addressKeys.Select(Get).Where(v => !string.IsNullOrWhiteSpace(v)));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.myptv.com/geocoding/v1/locations/by-text?searchText=" + Uri.EscapeDataString(address));
            request.Headers.Add("ApiKey", settings.Value.ApiKey);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var reason = (int)response.StatusCode switch
                {
                    401 or 403 => "the address service rejected access. Ask your administrator to check the API key and geocoding permissions.",
                    429 => "the address service request limit was reached. Try again later.",
                    _ => $"the address service returned HTTP {(int)response.StatusCode}. Try again later."
                };
                return (values, "Saved, but " + reason);
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var point = SelectPosition(json.RootElement, Get("ADDR1"), Get("CITY"), Get("STATE"), Get("ZIP"), Get(countryKey));
            if (point is null) return (values, "Saved, but " + MatchFailure(json.RootElement) + " " + correction);
            values[prefix + "LATITUDE"] = point.Value.Latitude.ToString("R", CultureInfo.InvariantCulture);
            values[prefix + "LONGITUDE"] = point.Value.Longitude.ToString("R", CultureInfo.InvariantCulture);
            return (values, null);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return (values, "Saved, but " + (e is JsonException ? "the address service returned an unreadable response." : e is OperationCanceledException ? "the address lookup timed out." : "the address service could not be reached. Check the server connection and try again."));
        }
    }

    private static string MatchFailure(JsonElement root)
    {
        if (!root.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
            return "the address service returned no usable location data.";
        if (locations.GetArrayLength() == 0) return "no matching address was found.";
        var exact = locations.EnumerateArray().Where(l => l.TryGetProperty("locationType", out var type) && type.GetString() is "EXACT_ADDRESS" or "INTERPOLATED_ADDRESS").ToArray();
        if (exact.Length == 0) return "only street or area matches were found; a house-number address is required.";
        var scores = exact.Select(l => l.TryGetProperty("quality", out var q) && q.TryGetProperty("totalScore", out var s) && s.TryGetDouble(out var n) ? n : 0).ToArray();
        if (scores.Max() < 90) return $"the best address match scored {scores.Max():0.#}; at least 90 is required.";
        return "the results did not identify one unambiguous location matching the address with valid coordinates. Interpolated matches must agree on house number, street, city, state and ZIP.";
    }

    public static (double Latitude, double Longitude)? SelectPosition(JsonElement root, string? street = null, string? city = null, string? state = null, string? postalCode = null, string? country = null)
    {
        if (!root.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array) return null;
        var matches = new List<(double Latitude, double Longitude)>();
        foreach (var location in locations.EnumerateArray())
        {
            if (!location.TryGetProperty("locationType", out var type) ||
                !(type.GetString() == "EXACT_ADDRESS" || type.GetString() == "INTERPOLATED_ADDRESS" && InterpolatedAddressMatches(location, street, city, state, postalCode, country))
                || !location.TryGetProperty("quality", out var quality) || !quality.TryGetProperty("totalScore", out var score)
                || !score.TryGetDouble(out var value) || value < 90) continue;
            if (!location.TryGetProperty("referencePosition", out var point)
                || !point.TryGetProperty("latitude", out var lat) || !point.TryGetProperty("longitude", out var lon)
                || !lat.TryGetDouble(out var latitude) || !lon.TryGetDouble(out var longitude)
                || !DataRequirements.HasCoordinates(latitude, longitude)) continue;
            matches.Add((latitude, longitude));
        }
        return matches.Distinct().Take(2).ToArray() is [var only] ? only : null;
    }

    private static bool InterpolatedAddressMatches(JsonElement location, string? street, string? city, string? state, string? postalCode, string? country)
    {
        // Be conservative: this normalization covers US addresses only. Never use a score
        // to substitute a different street, house number or locality.
        static string Normalize(string? value) => System.Text.RegularExpressions.Regex.Replace((value??"").ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
        static string Street(string? value)
        {
            var words=Normalize(value).Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(w=>w switch {
                "DRIVE"=>"DR", "ROAD"=>"RD", "STREET"=>"ST", "AVENUE"=>"AVE", "BOULEVARD"=>"BLVD",
                "LANE"=>"LN", "COURT"=>"CT", "CIRCLE"=>"CIR", "PLACE"=>"PL", "PARKWAY"=>"PKWY",
                "NORTH"=>"N", "SOUTH"=>"S", "EAST"=>"E", "WEST"=>"W", _=>w });
            return System.Text.RegularExpressions.Regex.Replace(string.Join(" ",words), @"^(?:US ROUTE|US HIGHWAY|US HWY|US|ROUTE|HIGHWAY|HWY) (?=\d)", "ROUTE ");
        }
        if(!location.TryGetProperty("address",out var address))return false;
        string? Field(string name)=>address.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
        var requestedCountry=Normalize(country);
        if(requestedCountry is not ("" or "US" or "USA" or "UNITED STATES" or "UNITED STATES OF AMERICA") || Field("countryCodeIsoAlpha2")!="US")return false;
        var input=System.Text.RegularExpressions.Regex.Match(street??"", @"^\s*(\d+[A-Za-z]?(?:-\d+[A-Za-z]?)?)\s+(.+?)\s*$");
        if(!input.Success || Normalize(input.Groups[1].Value)!=Normalize(Field("houseNumber")) || Street(input.Groups[2].Value)!=Street(Field("street")))return false;
        if(string.IsNullOrWhiteSpace(city) || Normalize(city)!=Normalize(Field("city")))return false;
        var region=Field("countryCode");
        var returnedState=region?.StartsWith("US-",StringComparison.Ordinal)==true?region[3..]:Field("state");
        if(string.IsNullOrWhiteSpace(state) || Normalize(state)!=Normalize(returnedState))return false;
        static string? Zip(string? value) => System.Text.RegularExpressions.Regex.Match(value??"", @"^\s*(\d{5})(?:-\d{4})?\s*$") is {Success:true} match?match.Groups[1].Value:null;
        return Zip(postalCode) is {} zip && zip==Zip(Field("postalCode"));
    }
}
