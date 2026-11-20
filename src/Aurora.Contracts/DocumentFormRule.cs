using System.Text.RegularExpressions;

namespace Aurora.Contracts;

public sealed class DocumentFormRule
{
    public string Issuer { get; set; } = "";
    public string Form { get; set; } = "";
    public string Account { get; set; } = "";
    public bool UsePrintedPro { get; set; }
    public string Instructions { get; set; } = "";

    private static string Key(string? value) => Regex.Replace(value?.Trim() ?? "", @"\s+", " ").ToUpperInvariant();

    // Co-branded headers and branch suffixes seen on the same freight forms.
    private static string IssuerKey(string? value)
    {
        var name=Key(value).TrimEnd('.');
        return name switch
        {
            "PGL (PEGASUS LOGISTICS GROUP)" or "PEGASUS LOGISTICS GROUP (PGL)" => "PGL",
            "ALLSTATES WORLDCARGO - BNA" => "ALLSTATES WORLDCARGO",
            "R+L CARRIERS / R+L GLOBAL LOGISTICS / R+L TRUCKLOAD SERVICES" or "R+L TRUCKLOAD SERVICES" => "R+L CARRIERS",
            "LAUFER GROUP INTERNATIONAL ON BEHALF OF FORWARDER MAURICE WARD & CO SRL" => "LAUFER GROUP INTERNATIONAL",
            "CAPITAL LOGISTICS INTERNATIONAL, LLC" => "CAPITAL LOGISTICS INTERNATIONAL LLC",
            "MAVERICK ACQUISITION VENTURES, INC" => "MAVERICK ACQUISITION VENTURES INC",
            "BEYOND TRANSPORTATION, INC" => "BEYOND TRANSPORTATION INC",
            "ALLIANCE STORE FIXTURES, INC" => "ALLIANCE STORE FIXTURES INC",
            _ => name
        };
    }

    public static List<DocumentFormRule> Validate(List<DocumentFormRule> rules)
    {
        if (rules.Count > 200) throw new FormatException("Use up to 200 PDF form rules per company.");
        var seen = new HashSet<(string, string, string)>();
        foreach (var rule in rules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.Issuer) || string.IsNullOrWhiteSpace(rule.Instructions) ||
                new[] {rule.Issuer,rule.Form,rule.Account}.Any(v => v is null || v.Length > 160 || v.Contains('\0')) ||
                rule.Instructions.Length > 4000 || rule.Instructions.Contains('\0'))
                throw new FormatException("Each PDF rule needs an issuer and instructions. Names and account codes allow 160 characters and instructions allow 4,000.");
            if (!seen.Add((IssuerKey(rule.Issuer),Key(rule.Form),Key(rule.Account))))
                throw new FormatException("Combine rules with the same issuer, form and account.");
        }
        return rules;
    }

    public bool Matches(string? issuer,string? form,string? account) =>
        IssuerKey(Issuer)==IssuerKey(issuer) && (Key(Form)=="" || Key(Form)==Key(form)) && (Key(Account)=="" || Key(Account)==Key(account));
}
