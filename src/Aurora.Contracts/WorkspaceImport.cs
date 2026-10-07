using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aurora.Contracts;

public sealed class WorkspaceImport
{
    public const int MaxBytes = 25 * 1024 * 1024;
    public required int Version { get; set; }
    public required TerminalDto[] Terminals { get; set; }
    public required CustomerDto[] Customers { get; set; }
    public required TmsOrder[] Orders { get; set; }
    public required ImportManifest[] Manifests { get; set; }
    public required EquipmentUnitDto[] Trucks { get; set; }
    public EquipmentTypeDto[] EquipmentTypes { get; set; } = [];
    public ImportPlanningSource[] PlanningSources { get; set; } = [];
    public static WorkspaceImport Parse(string json)
    {
        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new FormatException("Choose a JSON file up to 25 MB.");
            var data = JsonSerializer.Deserialize<WorkspaceImport>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
                ?? throw new FormatException("The file must contain a JSON object.");
            data.Validate();
            using var document = JsonDocument.Parse(json);
            var orderArray = document.RootElement.EnumerateObject().Single(p=>p.Name.Equals("orders",StringComparison.OrdinalIgnoreCase)).Value;
            foreach(var order in orderArray.EnumerateArray())
                if(!order.EnumerateObject().Any(p=>p.Name.Equals("scheduledAt",StringComparison.OrdinalIgnoreCase)))
                    throw new FormatException("Every order needs an explicit scheduledAt date/time with UTC offset.");
            return data;
        }
        catch (JsonException ex) { throw new FormatException($"Invalid import JSON at {ex.Path ?? "root"}: {ex.Message}"); }
    }
    public ImportSummary Summary => new(Terminals.Length, Orders.Length, Manifests.Length, Trucks.Length, Customers.Length);
    public void Validate()
    {
        if (Version != 1) throw new FormatException("Use import version 1.");
        if (Terminals is null || Customers is null || Orders is null || Manifests is null || Trucks is null || EquipmentTypes is null || PlanningSources is null)
            throw new FormatException("Include terminals, customers, orders, manifests, and trucks arrays. Use [] for an empty collection.");
        if (Customers.Length>5000 || Orders.Length > 5000 || Trucks.Length > 5000 || Terminals.Length > 1000 || Manifests.Length > 5000 || EquipmentTypes.Length > 500 || PlanningSources.Length > 100)
            throw new FormatException("Import up to 5,000 orders, trucks or manifests, 1,000 terminals, 500 equipment types, and 100 planning sources.");
        if (Customers.Any(x=>x is null) || Terminals.Any(x=>x is null) || Orders.Any(x=>x is null) || Manifests.Any(x=>x is null) || Trucks.Any(x=>x is null) || EquipmentTypes.Any(x=>x is null) || PlanningSources.Any(x=>x is null))
            throw new FormatException("Import arrays cannot contain null records.");
        foreach(var t in Terminals) t.Validate();
        foreach(var c in Customers) c.Validate();
        Unique(Customers.Select(c=>c.Code), "customer codes", StringComparer.OrdinalIgnoreCase);
        foreach(var o in Orders) { o.Validate(); if(o.ScheduledAt == default) throw new FormatException($"Order {o.Id} needs a scheduled date/time."); }
        foreach(var t in Trucks) { t.Validate(); if(t.Terminal.Length > 80) throw new FormatException("Truck terminal codes allow 80 characters."); }
        foreach(var t in EquipmentTypes) t.Validate();
        Unique(Terminals.Select(t=>t.Code), "terminal codes", StringComparer.OrdinalIgnoreCase);
        if(Terminals.Where(t=>t.RoutingRole==NodeVocabulary.Hub && t.Region.Trim().Length>0).GroupBy(t=>t.Region.Trim().ToUpperInvariant()).FirstOrDefault(g=>g.Count()>1) is {} hubs)
            throw new FormatException($"Region {hubs.Key} has more than one hub. A region has one hub.");
        Unique(Terminals.Where(t=>t.Id!=Guid.Empty).Select(t=>t.Id.ToString()), "terminal ids");
        var nodeIds=Terminals.Where(t=>t.Id!=Guid.Empty).Select(t=>t.Id).ToHashSet();
        foreach(var t in Terminals) if(t.RouteViaNodeId is {} via && !nodeIds.Contains(via)) throw new FormatException($"Terminal {t.Code} routes via an id that is not a terminal in this file.");
        foreach(var o in Orders) if(new[]{o.PickupNodeId,o.DeliverToNodeId}.OfType<Guid>().Any(id=>!nodeIds.Contains(id))) throw new FormatException($"Order {o.Id} references a pick-up or deliver-to node that is not a terminal id in this file.");
        Unique(Orders.Select(o=>o.Id), "order IDs");
        Unique(Trucks.Select(t=>t.Id), "truck IDs");
        Unique(EquipmentTypes.Select(t=>t.Code), "equipment type codes");
        Unique(Manifests.Select(m=>m.Number), "manifest numbers");
        var terminals=Terminals.Select(t=>t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var customers=Customers.Select(c=>c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var o in Orders) if(string.IsNullOrWhiteSpace(o.CustomerCode) || !customers.Contains(o.CustomerCode)) throw new FormatException($"Order {o.Id} must reference a customerCode in this file.");
        var orders=Orders.ToDictionary(o=>o.Id);
        var trucks=Trucks.ToDictionary(t=>t.Id);
        foreach(var o in Orders) if(string.IsNullOrWhiteSpace(o.TerminalCode) || !terminals.Contains(o.TerminalCode)) throw new FormatException($"Order {o.Id} must reference a terminalCode in this file.");
        foreach(var t in Trucks) if(!terminals.Contains(t.Terminal)) throw new FormatException($"Truck {t.Id} references an unknown terminal.");
        var assigned=new HashSet<string>();
        foreach(var m in Manifests)
        {
            new TmsManifest {Number=m.Number,ManifestDate=m.ManifestDate,Status=m.Status,Vehicle=m.TruckId,Details=m.Details}.Validate();
            if(m.ManifestDate==default || m.OrderIds is null) throw new FormatException($"Manifest {m.Number} needs a date and orderIds array.");
            if(string.IsNullOrWhiteSpace(m.TruckId) || !trucks.TryGetValue(m.TruckId,out var truck)) throw new FormatException($"Manifest {m.Number} references an unknown truck.");
            foreach(var id in m.OrderIds)
            {
                if(id is null || !orders.TryGetValue(id,out var order)) throw new FormatException($"Manifest {m.Number} references an unknown order.");
                if(!assigned.Add(id)) throw new FormatException($"Order {id} is assigned more than once.");
                if(!string.Equals(order.TerminalCode,truck.Terminal,StringComparison.OrdinalIgnoreCase)) throw new FormatException($"Manifest {m.Number} mixes orders and a truck from different terminals.");
                if(order.Status!=m.OrderStatus) throw new FormatException($"Order {id} must have status {m.OrderStatus} for manifest {m.Number}.");
            }
        }
        foreach(var o in Orders) if(!assigned.Contains(o.Id) && o.Status is "Routed" or "Dispatched" or "InTransit" or "OutForDelivery") throw new FormatException($"Order {o.Id} requires a manifest assignment.");
    }
    private static void Unique(IEnumerable<string> values,string label,StringComparer? comparer=null)
    {
        var all=values.ToArray();
        if(all.Distinct(comparer ?? StringComparer.Ordinal).Count()!=all.Length) throw new FormatException($"The file contains duplicate {label}.");
    }
}
public sealed class ImportManifest
{
    public required string Number { get; set; }
    public required DateOnly ManifestDate { get; set; }
    public string Status { get; set; } = "Planning";
    public required string TruckId { get; set; }
    public ManifestDetailsDto Details { get; set; } = new();
    public required string[] OrderIds { get; set; }
    [JsonIgnore] public string OrderStatus => Status switch { "Complete"=>"Delivered", "Dispatched"=>"Dispatched", "En Route" or "Arrived"=>"OutForDelivery", _=>"Routed" };
}
public sealed class ImportPlanningSource
{
    public required string Name { get; set; }
    public required JsonObject Request { get; set; }
}
public sealed record ImportSummary(int Terminals,int Orders,int Manifests,int Trucks,int Customers);
