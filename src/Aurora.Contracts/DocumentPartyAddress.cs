using System.Text.RegularExpressions;

namespace Aurora.Contracts;

internal static class DocumentPartyAddress
{
    private static readonly Dictionary<string,string> Regions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alabama"] = "AL",
        ["Alaska"] = "AK",
        ["Arizona"] = "AZ",
        ["Arkansas"] = "AR",
        ["California"] = "CA",
        ["Colorado"] = "CO",
        ["Connecticut"] = "CT",
        ["Delaware"] = "DE",
        ["District of Columbia"] = "DC",
        ["Florida"] = "FL",
        ["Georgia"] = "GA",
        ["Hawaii"] = "HI",
        ["Idaho"] = "ID",
        ["Illinois"] = "IL",
        ["Indiana"] = "IN",
        ["Iowa"] = "IA",
        ["Kansas"] = "KS",
        ["Kentucky"] = "KY",
        ["Louisiana"] = "LA",
        ["Maine"] = "ME",
        ["Maryland"] = "MD",
        ["Massachusetts"] = "MA",
        ["Michigan"] = "MI",
        ["Minnesota"] = "MN",
        ["Mississippi"] = "MS",
        ["Missouri"] = "MO",
        ["Montana"] = "MT",
        ["Nebraska"] = "NE",
        ["Nevada"] = "NV",
        ["New Hampshire"] = "NH",
        ["New Jersey"] = "NJ",
        ["New Mexico"] = "NM",
        ["New York"] = "NY",
        ["North Carolina"] = "NC",
        ["North Dakota"] = "ND",
        ["Ohio"] = "OH",
        ["Oklahoma"] = "OK",
        ["Oregon"] = "OR",
        ["Pennsylvania"] = "PA",
        ["Rhode Island"] = "RI",
        ["South Carolina"] = "SC",
        ["South Dakota"] = "SD",
        ["Tennessee"] = "TN",
        ["Texas"] = "TX",
        ["Utah"] = "UT",
        ["Vermont"] = "VT",
        ["Virginia"] = "VA",
        ["Washington"] = "WA",
        ["West Virginia"] = "WV",
        ["Wisconsin"] = "WI",
        ["Wyoming"] = "WY",
        ["American Samoa"] = "AS",
        ["Guam"] = "GU",
        ["Northern Mariana Islands"] = "MP",
        ["Puerto Rico"] = "PR",
        ["US Virgin Islands"] = "VI",
        ["Alberta"] = "AB",
        ["British Columbia"] = "BC",
        ["Manitoba"] = "MB",
        ["New Brunswick"] = "NB",
        ["Newfoundland and Labrador"] = "NL",
        ["Northwest Territories"] = "NT",
        ["Nova Scotia"] = "NS",
        ["Nunavut"] = "NU",
        ["Ontario"] = "ON",
        ["Prince Edward Island"] = "PE",
        ["Quebec"] = "QC",
        ["Québec"] = "QC",
        ["Saskatchewan"] = "SK",
        ["Yukon"] = "YT",
    };

    internal static string RegionCode(string? value) =>
        Regions.TryGetValue(value?.Trim() ?? "", out var code) ? code : value ?? "";

    private static readonly Regex Street = Pattern(@"^(?:P\s*\.?\s*O\s*\.?\s*BOX\s+[\w-]+|\d+[\w-]*\s+.+)$");
    private static readonly Regex Postal = Pattern(@"^(?:(?<city>.+?)[,\s]+)?(?<state>" + string.Join("|", Regions.Keys.Select(Regex.Escape)) + @"|[A-Z]{2})[,\s]+(?<zip>\d{5}(?:-\d{4})?|[A-Z]\d[A-Z]\s?\d[A-Z]\d)$");
    private static readonly Regex Country = Pattern(@"^(?:US|USA|U\.S\.A\.|UNITED STATES(?: OF AMERICA)?|CA|CAN|CANADA)$");
    private static readonly Regex Unit = Pattern(@"^(?:SUITE|STE\.?|APT\.?|UNIT|FLOOR|BLDG|BUILDING|#)\s*[-\w]+");
    private static Regex Pattern(string regex) => new(regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static void Normalize(TmsOrder order)
    {
        foreach (var (_,_,properties) in NovaImportAddress.Parties)
        {
            var fields=properties.Select(key=>typeof(TmsOrder).GetProperty(key)!).ToArray();
            var values=fields.Select(field=>(string?)field.GetValue(order)??"").ToArray();
            values[4] = RegionCode(values[4]);
            values[6] = NovaDocumentImport.CountryCode(values[6]);
            // The extractor can put the whole printed block in either the name or the street.
            for (int source=0; source<=1; source++)
            {
                var parts=values[source].Split([',','\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
                if(parts.Count<2)continue;
                int street=source==0 ? parts.FindIndex(1,p=>Street.IsMatch(p)) : 0;
                if(street<0 || !Street.IsMatch(parts[street]))continue;
                var parsed=new string[7];
                if(source==0)parsed[0]=string.Join(", ",parts.Take(street));
                parsed[1]=parts[street];
                var tail=parts.Skip(street+1).ToList();
                if(tail.Count>0 && Country.IsMatch(tail[^1])){parsed[6]=NovaDocumentImport.CountryCode(tail[^1]);tail.RemoveAt(tail.Count-1);}
                if(tail.Count>0 && Unit.IsMatch(tail[0])){parsed[2]=tail[0];tail.RemoveAt(0);}
                if(tail.Count>0)
                {
                    var location=Postal.Match(string.Join(", ",tail));
                    if(!location.Success)continue;
                    parsed[3]=location.Groups["city"].Value.TrimEnd(' ', ',');
                    parsed[4]=RegionCode(location.Groups["state"].Value);
                    parsed[5]=location.Groups["zip"].Value;
                }
                if(parsed.Where((v,i)=>i!=source && !string.IsNullOrEmpty(v) && !string.IsNullOrWhiteSpace(values[i])
                    && !string.Equals(v,values[i].Trim(),StringComparison.OrdinalIgnoreCase)).Any())continue;
                for(int i=0;i<fields.Length;i++)
                    if(!string.IsNullOrWhiteSpace(parsed[i]) && (i==source || string.IsNullOrWhiteSpace(values[i])))
                    {
                        values[i]=parsed[i];
                        fields[i].SetValue(order,values[i]);
                    }
            }
            fields[4].SetValue(order,values[4]);
            fields[6].SetValue(order,values[6]);
        }
    }
}
