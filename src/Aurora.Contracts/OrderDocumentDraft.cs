using System.Globalization;
using System.Text.RegularExpressions;

namespace Aurora.Contracts;

public sealed record DocumentLogoCompany(string Name, int Page, string? Address=null, string? Address2=null,
    string? City=null, string? State=null, string? PostalCode=null, string? Country=null,
    string? Contact=null, string? Phone=null, string? Email=null);

public sealed class OrderDocumentDraft
{
    public string MappingAccount {get;set;}="";
    public string? ExtractionError {get;set;}
    public int[] SourcePages {get;set;}=[];
    public string MappingSender {get;set;}="";
    public List<string> MappingDecisions {get;set;}=[];
    public MappingCandidate[] PrintedIdentifiers {get;set;}=[];
    public string MappingForm {get;set;}="";
    public DocumentMappingQuestion[] MappingQuestions {get;set;}=[];
    public HashSet<string> EditedAddressParts {get;set;}=[];
    public string FreightTerms {get;set;}="";
    public DocumentLogoCompany? LogoCompany {get;set;}
    public string? BillToContact {get;set;}
    public string? BillToPhone {get;set;}
    public string? BillToEmail {get;set;}
    public string BillingEvidence {get;set;}="";
    public int ImportRowNumber { get; set; }
    public string FailureLabel()
    {
        var row = ImportRowNumber > 0 ? $"Order {ImportRowNumber}" : "Order";
        var identifier = string.IsNullOrWhiteSpace(TagNumber) ? Order?.Id : TagNumber;
        return !string.IsNullOrWhiteSpace(identifier) ? $"{row} (PRO/tag {identifier})" :
            !string.IsNullOrWhiteSpace(BillOfLading) ? $"{row} (BOL {BillOfLading})" : row;
    }

    public Guid ImportRowId { get; set; }
    public Guid? ImportId { get; set; }
    public bool IsSpreadsheetImport { get; set; }
    public string[] AddressLookupMessages {get;set;}=[];
    public int? NativeStatusId { get; set; }
    public TmsOrder Order { get; set; } = new();
    public string TagNumber { get; set; } = "";
    public bool UseReviewedPacketBol {get;set;}
    public string BillOfLading { get; set; } = "";
    public string PurchaseOrder {get;set;}="";
    public string BillToAccountId {get;set;}="";
    public string? DailyReportBillToName {get;set;}
    public string BillToLookupName() =>
        DailyReportBillToName is { Length: > 40 } full && Order.BillTo == full[..40] ? full : Order.BillTo;
    public string ShipperAccountId {get;set;}="";
    public string ConsigneeAccountId {get;set;}="";
    public string? IndexedBillToPayer {get;set;}
    public string? PrintedBillToPayer {get;set;}
    public string? PrintedThirdPartyPayer {get;set;}
    public string? PrintedBillToAccountLine {get;set;}
    public string AccountId(string part)=>part switch {"pickup"=>ShipperAccountId,"delivery"=>ConsigneeAccountId,"header"=>BillToAccountId,_=>throw new ArgumentException(nameof(part))};
    public void SetAccountId(string part,string value)
    {
        switch(part){case "pickup":ShipperAccountId=value;break;case "delivery":ConsigneeAccountId=value;break;case "header":BillToAccountId=value;break;default:throw new ArgumentException(nameof(part));}
    }
    public void SeparatePartyAddresses()=>DocumentPartyAddress.Normalize(Order);
    public bool ApplyPrintedAccount()
    {
        if(string.IsNullOrWhiteSpace(PrintedBillToAccountLine))return false;
        var match=Regex.Match(PrintedBillToAccountLine.Trim(),@"^(?:Account\s*:\s*)?(?<id>[\w.-]+)\s+[-\u2013\u2014]\s+(?<name>.+)$",RegexOptions.IgnoreCase);
        if(match.Success){BillToAccountId=match.Groups["id"].Value;Order.BillTo=match.Groups["name"].Value.Trim();}
        return match.Success;
    }
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
            throw new FormatException("Enter a positive PRO number with 1 to 9 digits. You can include a terminal prefix, as in MCO3706592.");
        return (pro.ToString(CultureInfo.InvariantCulture), match.Groups["terminal"].Value.ToUpperInvariant());
    }

    public void Normalize(bool allowBlankPro=false)
    {
        if (Order is null) throw new FormatException("Order details are required.");
        foreach (var value in new[] { TagNumber, BillOfLading, ShipmentReference, HouseAirWaybill, MasterAirWaybill })
            if (value is null || value.Length > 200) throw new FormatException("Document identifiers allow up to 200 characters.");
        var identifier = string.IsNullOrWhiteSpace(TagNumber) ? Order.Id : TagNumber;
        var (pro, terminal) = allowBlankPro && string.IsNullOrWhiteSpace(identifier) ? ("", "") : SplitTag(identifier);
        Order.Id = pro;
        Order.TerminalCode = terminal;
        // Only the explicitly extracted BOL is mapped to Nova PBBOL. Never borrow another identifier.
        Order.Reference = BillOfLading.Trim();
    }

    public string NotesWithReferences(bool includeLines=false)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(Order.Notes)) lines.Add(Order.Notes);
        foreach (var (label, value) in new[] { ("Shipper account ID",ShipperAccountId),("Consignee account ID",ConsigneeAccountId),("Bill-to account ID",BillToAccountId),("Purchase order",PurchaseOrder),("Original Tag", TagNumber), ("Work type", Order.WorkType), ("Shipment reference", ShipmentReference), ("HAWB", HouseAirWaybill), ("MAWB", MasterAirWaybill) })
            if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value.Trim()}");
        if(includeLines)
            foreach(var (label,value) in new[]{("Bill-to contact",BillToContact),("Bill-to phone",BillToPhone),("Bill-to email",BillToEmail)})
                if(!string.IsNullOrWhiteSpace(value))lines.Add($"{label}: {value.Trim()}");
        if(includeLines && FreightLines is {Length:>0})lines.Add("Imported freight rows: "+System.Text.Json.JsonSerializer.Serialize(FreightLines));
        return string.Join("\n", lines);
    }

    public void ApplyFreightTotals()
    {
        var lines=FreightLines??[];
        if(PrintedTotalPieces is {} pieces)Order.Pieces=pieces;
        else if(lines.Length>0 && lines.All(l=>l.Pieces is not null))Order.Pieces=lines.Sum(l=>l.Pieces!.Value);
        if(PrintedTotalWeightLb is {} weight)Order.Weight=weight;
        else if(lines.Length>0 && lines.All(l=>l.WeightLb is not null))Order.Weight=lines.Sum(l=>l.WeightLb!.Value);
        bool assumed=false;
        decimal total=0;
        foreach(var line in lines)
        {
            line.CalculatedCubeFt3=null;
            if(line.CubeFt3 is {} printed){total+=printed;continue;}
            if(line.Pieces is not >0 || line.Length is not >0 || line.Width is not >0 || line.Height is not >0)continue;
            var unit=(line.DimensionUnit??"").Trim().ToLowerInvariant();
            if(unit=="" && DocumentType?.ToUpperInvariant() is ("PEGASUS" or "AIT")){unit="in";assumed=true;}
            var divisor=unit switch {"in" or "inch" or "inches"=>1728m,"ft" or "feet"=>1m,"cm"=>28316.846592m,"m"=>0.028316846592m,_=>0};
            if(divisor==0)continue;
            var volume=(line.Pieces*line.Length*line.Width*line.Height)!.Value/divisor;
            line.CalculatedCubeFt3=decimal.Round(volume,2,MidpointRounding.AwayFromZero);
            total+=volume;
        }
        if(PrintedTotalCubeFt3 is {} cube)Order.Cube=cube;
        else if(lines.Length>0 && lines.All(l=>l.Volume.HasValue) && lines.Sum(l=>l.Pieces??0)==Order.Pieces)
            Order.Cube=decimal.Round(total,2,MidpointRounding.AwayFromZero);
        if(assumed)Order.Notes=(Order.Notes+$"\nCube calculated from dimensions; inches assumed for {DocumentType} form.").Trim();
    }
}

public sealed class DocumentFreightLine
{
    public decimal? Pieces {get;set;}
    public decimal? CubeFt3 {get;set;}
    public decimal? CalculatedCubeFt3 {get;set;}
    public decimal? Volume => CubeFt3 ?? CalculatedCubeFt3;
    public decimal? WeightLb {get;set;}
    public decimal? DimWeightLb {get;set;}
    public string? Description {get;set;}
    public decimal? Length {get;set;}
    public decimal? Width {get;set;}
    public decimal? Height {get;set;}
    public string DimensionUnit {get;set;}="";
}

public sealed record DuplicateImportPro(string Error,string? Code)
{
    public const string ErrorCode="duplicate_pro";
    public static DuplicateImportPro For(string pro)=>new($"PRO {pro} already exists. No order was created.",ErrorCode);
}

public sealed class DuplicateProException(string message) : FormatException(message);
