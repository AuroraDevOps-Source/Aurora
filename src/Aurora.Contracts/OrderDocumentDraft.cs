using System.Globalization;
using System.Text.RegularExpressions;

namespace Aurora.Contracts;

public sealed class OrderDocumentDraft
{
    public Guid? ImportId { get; set; }
    public string[] AddressLookupMessages {get;set;}=[];
    public int? NativeStatusId { get; set; }
    public TmsOrder Order { get; set; } = new();
    public string TagNumber { get; set; } = "";
    public string BillOfLading { get; set; } = "";
    public string ShipmentReference { get; set; } = "";
    public string HouseAirWaybill { get; set; } = "";
    public string MasterAirWaybill { get; set; } = "";
    public string? TerminalCode {get;set;}
    public string? DestinationTerminalCode {get;set;}
    public string PrintedLykesTag {get;set;}="";
    public string DocumentType {get;set;}="";
    public decimal? PrintedTotalPieces {get;set;}
    public decimal? PrintedTotalWeightLb {get;set;}
    public decimal? PrintedTotalCubeFt3 {get;set;}
    public DocumentFreightLine[] FreightLines {get;set;}=[];
    public DateTime? LocalScheduledAt {get;set;}
    public DateTime? LocalAppointmentFrom {get;set;}
    public DateTime? LocalAppointmentTo {get;set;}

    public void ApplyLocalSchedule(TimeZoneInfo zone)
    {
        DateTimeOffset Utc(DateTime value)=>new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value,DateTimeKind.Unspecified),zone));
        if(LocalAppointmentFrom is {} from)Order.AppointmentFrom=Utc(from);
        if(LocalAppointmentTo is {} to)Order.AppointmentTo=Utc(to);
        if((LocalScheduledAt??LocalAppointmentFrom) is {} scheduled)Order.ScheduledAt=Utc(scheduled);
    }
    public void ChangeLocalSchedule(DateTime scheduled)
    {
        if(LocalScheduledAt is not {} previous)return;
        var change=scheduled-previous;
        LocalScheduledAt=scheduled;
        if(LocalAppointmentFrom is {} from)LocalAppointmentFrom=from+change;
        if(LocalAppointmentTo is {} to)LocalAppointmentTo=to+change;
    }

    public static (string Pro, string Terminal) SplitTag(string? value)
    {
        var match = Regex.Match(value?.Trim() ?? "", @"^(?:(?<terminal>[A-Za-z]+)[ -]?)?(?<pro>[0-9]{1,9})$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups["pro"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pro) || pro <= 0)
            throw new FormatException("Enter a Tag with a terminal prefix and 1–9 digits, or a positive numeric PRO (for example MCO3706592 or 3706592).");
        return (pro.ToString(CultureInfo.InvariantCulture), match.Groups["terminal"].Value.ToUpperInvariant());
    }

    public void Normalize()
    {
        if (Order is null) throw new FormatException("Order details are required.");
        foreach (var value in new[] { TagNumber, BillOfLading, ShipmentReference, HouseAirWaybill, MasterAirWaybill })
            if (value is null || value.Length > 200) throw new FormatException("Document identifiers allow up to 200 characters.");
        var (pro, terminal) = SplitTag(string.IsNullOrWhiteSpace(TagNumber) ? Order.Id : TagNumber);
        Order.Id = pro;
        Order.TerminalCode = terminal;
        // Only the explicitly extracted BOL is mapped to Nova PBBOL. Never borrow another identifier.
        Order.Reference = BillOfLading.Trim();
    }

    public string NotesWithReferences()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(Order.Notes)) lines.Add(Order.Notes);
        foreach (var (label, value) in new[] { ("Original Tag", TagNumber), ("Work type", Order.WorkType), ("Shipment reference", ShipmentReference), ("HAWB", HouseAirWaybill), ("MAWB", MasterAirWaybill) })
            if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value.Trim()}");
        return string.Join("\n", lines);
    }

    public void ApplyFreightTotals()
    {
        var lines=FreightLines??[];
        if(PrintedTotalPieces is {} pieces)Order.Pieces=pieces;
        else if(lines.Length>0 && lines.All(l=>l.Pieces is not null))Order.Pieces=lines.Sum(l=>l.Pieces!.Value);
        if(PrintedTotalWeightLb is {} weight)Order.Weight=weight;
        else if(lines.Length>0 && lines.All(l=>l.WeightLb is not null))Order.Weight=lines.Sum(l=>l.WeightLb!.Value);
        if(PrintedTotalCubeFt3 is {} cube){Order.Cube=cube;return;}
        if(lines.Length==0 || lines.Any(l=>l.Pieces is null or <=0 || l.Length is null or <=0 || l.Width is null or <=0 || l.Height is null or <=0) || lines.Sum(l=>l.Pieces!.Value)!=Order.Pieces)return;
        decimal total=0;bool assumed=false;
        foreach(var line in lines)
        {
            var unit=(line.DimensionUnit??"").Trim().ToLowerInvariant();
            if(unit=="" && (string.Equals(DocumentType,"Pegasus",StringComparison.OrdinalIgnoreCase) || string.Equals(DocumentType,"AIT",StringComparison.OrdinalIgnoreCase))){unit="in";assumed=true;}
            decimal divisor=unit switch {"in" or "inch" or "inches"=>1728m,"ft" or "feet"=>1m,"cm"=>28316.846592m,"m"=>0.028316846592m,_=>0};
            if(divisor==0)return;
            total+=line.Pieces!.Value*line.Length!.Value*line.Width!.Value*line.Height!.Value/divisor;
        }
        Order.Cube=decimal.Round(total,2,MidpointRounding.AwayFromZero);
        if(assumed)Order.Notes=(Order.Notes+$"\nCube calculated from dimensions; inches assumed for {DocumentType} form.").Trim();
    }
}

public sealed class DocumentFreightLine
{
    public decimal? Pieces {get;set;}
    public decimal? WeightLb {get;set;}
    public decimal? Length {get;set;}
    public decimal? Width {get;set;}
    public decimal? Height {get;set;}
    public string DimensionUnit {get;set;}="";
}
