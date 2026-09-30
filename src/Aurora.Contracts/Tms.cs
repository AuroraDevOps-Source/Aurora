namespace Aurora.Contracts;

public sealed class TmsOrder
{
    public Guid SourceId { get; set; }
    public string Id { get; set; } = "";
    public Guid? TerminalId { get; set; }
    public string? TerminalCode { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public DateTimeOffset ScheduledAt { get; set; } = DateTimeOffset.Now;
    public string Customer { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Shipper { get; set; } = "";
    public string OriginAddress { get; set; } = "";
    public string OriginCity { get; set; } = "";
    public string OriginState { get; set; } = "";
    public string OriginPostalCode { get; set; } = "";
    public string BillTo { get; set; } = "";
    public string Address2 { get; set; } = "";
    public string Country { get; set; } = "";
    public string OriginAddress2 { get; set; } = "";
    public string OriginCountry { get; set; } = "";
    public string BillToAddress { get; set; } = "";
    public string BillToAddress2 { get; set; } = "";
    public string BillToCity { get; set; } = "";
    public string BillToState { get; set; } = "";
    public string BillToPostalCode { get; set; } = "";
    public string BillToCountry { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Pieces { get; set; }
    public decimal Weight { get; set; }
    public decimal Cube { get; set; }
    public string Notes { get; set; } = "";
    public string Status { get; set; } = WorkspaceOrderStatus.Ready;
    public int Revision { get; set; }
    public Guid? ManifestId { get; set; }
    public string? ManifestNumber { get; set; }
    public int? StopNumber { get; set; }
    public void CopyBillTo(bool fromShipper)
    {
        BillTo = fromShipper ? Shipper : Customer;
        BillToAddress = fromShipper ? OriginAddress : Address;
        BillToAddress2 = fromShipper ? OriginAddress2 : Address2;
        BillToCity = fromShipper ? OriginCity : City;
        BillToState = fromShipper ? OriginState : State;
        BillToPostalCode = fromShipper ? OriginPostalCode : PostalCode;
        BillToCountry = fromShipper ? OriginCountry : Country;
    }
    public void Validate() {
        if(string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Customer)) throw new FormatException("PRO and customer are required.");
        if(!WorkspaceOrderStatus.All.Contains(Status)) throw new FormatException("Unknown order status.");
        if(new[]{Id,Customer,Address,City,State,PostalCode,Shipper,OriginAddress,OriginCity,OriginState,OriginPostalCode,BillTo,Reference,Address2,Country,OriginAddress2,OriginCountry,BillToAddress,BillToAddress2,BillToCity,BillToState,BillToPostalCode,BillToCountry}.Any(v=>v is null || v.Length>200) || Notes is null || Notes.Length>4000) throw new FormatException("Fields allow 200 characters; notes allow 4,000.");
        if(Pieces<0 || Pieces!=decimal.Truncate(Pieces) || Weight<0 || Cube<0) throw new FormatException("Pieces must be a nonnegative whole number; weight and cube cannot be negative.");
    }
}
public sealed class TmsManifest
{
    public Guid Id { get; set; }
    public string Number { get; set; } = "";
    public DateOnly ManifestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Status { get; set; } = "Planning";
    public string Vehicle { get; set; } = "";
    public ManifestDetailsDto Details { get; set; } = new();
    public int Revision { get; set; }
    public int OrderCount { get; set; }
    public decimal Pieces { get; set; }
    public decimal Weight { get; set; }
    public static readonly string[] Statuses=["Planning","Loading","Loaded","Ready","Dispatched","En Route","Arrived","Complete"];
    public bool Editable => Status is "Planning" or "Loading" or "Loaded" or "Ready";
    public void Validate() {
        if(string.IsNullOrWhiteSpace(Number) || Number.Length>100 || Vehicle is null || Vehicle.Length>200) throw new FormatException("Manifest number is required (up to 100 characters); truck allows 200.");
        if(!Statuses.Contains(Status) || Details is null) throw new FormatException("Valid manifest status and details are required.");
        Details.Validate();
    }
}
public sealed record TmsOrderKey(Guid SourceId,string Id);
public sealed record TmsAssignmentChange(TmsOrderKey[] Orders,int Revision);
