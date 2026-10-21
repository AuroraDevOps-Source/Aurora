namespace Aurora.Contracts;

public sealed record FirebirdRoutingStatus(bool Enabled, int? ReadyStatus = null);
public sealed record NovaRoutingConfiguration(int[] ReadyStatuses, NovaManifestStatus[] AvailableStatuses);
public sealed record NovaRoutingConfigurationEdit(int[] ReadyStatuses);
public sealed record FirebirdSyncResult(int Orders, string[] Terminals, string[] Warnings, Guid[] SourceIds);
public sealed record NovaManifestStatus(int Id, string Name);
public sealed record NovaManifestCreate(string Origin,string Destination,DateOnly Date);
public sealed record NovaManifestList(DateOnly Today, NovaManifestStatus[] Statuses, NovaManifest[] Manifests);
public sealed class NovaManifest
{
    public string StartTerminal { get; set; } = "";
    public string EndTerminal { get; set; } = "";
    public double? EndLatitude { get; set; }
    public double? EndLongitude { get; set; }
    public bool IsTransfer => !string.IsNullOrWhiteSpace(EndTerminal) && !StartTerminal.Equals(EndTerminal,StringComparison.OrdinalIgnoreCase);
    public double? DepotLatitude { get; set; }
    public double? DepotLongitude { get; set; }
    public long Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ManifestDate { get; set; }
    public int? StatusId { get; set; }
    public string Status { get; set; } = "";
    public string Terminal { get; set; } = "";
    public string Truck { get; set; } = "";
    public string Driver { get; set; } = "";
    public int OrderCount { get; set; }
}
public sealed class NovaManifestOrder
{
    public string Status { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal? Pieces { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Cube { get; set; }
    public DateTime? AppointmentFrom { get; set; }
    public DateTime? AppointmentTo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public long Id { get; set; }
    public string Pro { get; set; } = "";
    public int Sequence { get; set; }
    public string Customer { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
}
public sealed class NovaOrderDetails
{
    public string BillTo { get; set; } = "";
    public string BillToAddress { get; set; } = "";
    public string PurchaseOrder { get; set; } = "";
    public string CustomerOrder { get; set; } = "";
    public string Commodity { get; set; } = "";
    public string Notes { get; set; } = "";
    public string CarrierReferences { get; set; } = "";
    public NovaOrderParty[] Stops { get; set; } = [];
    public NovaOrderItem[] Items { get; set; } = [];
    public string[] Agents { get; set; } = [];
}
public sealed class NovaOrderParty
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? AppointmentFrom { get; set; }
    public DateTime? AppointmentTo { get; set; }
}
public sealed class NovaOrderItem
{
    public string Description { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? Pieces { get; set; }
    public string? Weight { get; set; }
    public string? Cube { get; set; }
}
