using System.Text.Json;

namespace Aurora.Contracts;

public static class FreightVocabulary
{
    public static readonly string[] BillTypes = ["LTL", "FTL", "Air Freight", "Intermodal", "Brokerage", "Small Parcel", "Recovery"];
    public static readonly string[] Kinds = ["Movement", "Warehouse", "Storage"];
    public static readonly string[] MovementTypes = ["Pickup", "Linehaul", "Delivery", "Recovery", "Return to dock"];
    public static readonly string[] Roles = ["Carrier", "Managing partner", "Broker", "Information only"];
    public static readonly string[] Providers = ["Company", "Agent", "Interline", "Cartage", "Other"];
    public static readonly string[] ReferenceTypes = ["Company PRO", "Partner PRO", "Bill of lading", "Government BOL", "Purchase order", "Registration", "Original shipment", "Other"];
    public static readonly string[] TaskTypes = ["Unload", "Cross-dock", "Pick and stage", "Inspect", "Blanket wrap", "Assemble", "Load", "Helper", "Repack", "Other"];
    public static readonly string[] Responsibilities = ["Customer", "Carrier", "Partner", "Unknown"];
    public static readonly string[] Actions = ["Start", "Complete task", "Complete", "Fail", "Cancel"];
}

public sealed class FreightLocation
{
    public Guid? NodeId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
    public void Validate()
    {
        FreightWorkflow.Text(Name, 200, "Location name", true);
        foreach (var value in new[] { Address, City, State, PostalCode, Country }) FreightWorkflow.Text(value, 200, "Location");
        if (NodeId == Guid.Empty) throw new FormatException("Choose a valid terminal.");
    }
    public static FreightLocation From(TerminalDto n) => new() { NodeId = n.Id, Name = n.Name, Address = n.Address, City = n.City, State = n.State, PostalCode = n.PostalCode, Country = n.Country };
}

public sealed class FreightReference
{
    public string Type { get; set; } = "Partner PRO";
    public string Partner { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class FreightTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "Unload";
    public string Instructions { get; set; } = "";
    public bool Required { get; set; } = true;
    // Estimates for the whole task, in the workflow currency. Actual costs are execution events.
    public decimal EstimatedCost { get; set; }
    public decimal PlannedCharge { get; set; }
}

public sealed class FreightActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "Movement";
    public string MovementType { get; set; } = "Delivery";
    public string Role { get; set; } = "Carrier";
    public string ProviderType { get; set; } = "Company";
    public string Provider { get; set; } = "";
    public FreightLocation Origin { get; set; } = new();
    // Only movement activities have a destination; warehouse/storage use Origin as their location.
    public FreightLocation? Destination { get; set; } = new();
    public string Instructions { get; set; } = "";
    public Guid? RetryOf { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal PlannedCharge { get; set; }
    // Storage is charged per started 24-hour period after grace, per activity (not per piece).
    public int StorageFreeHours { get; set; }
    public decimal StorageDailyCharge { get; set; }
    public List<FreightTask> Tasks { get; set; } = [];
}

public sealed record FreightEvent(Guid Id, Guid ActivityId, Guid? TaskId, string Action,
    DateTimeOffset RecordedAt, Guid RecordedBy, string Note, string Responsibility, decimal? ActualCost);

public sealed class FreightExecution
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public int Revision { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? TaskId { get; set; }
    public string Action { get; set; } = "Start";
    public string Note { get; set; } = "";
    public string Responsibility { get; set; } = "Unknown";
    public decimal? ActualCost { get; set; }
}

public sealed class FreightWorkflow
{
    public int Revision { get; set; }
    public string BillType { get; set; } = "LTL";
    public string Service { get; set; } = "";
    public string PricingAgreement { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public List<FreightReference> References { get; set; } = [];
    // List order is execution order. Task order is local to an activity.
    public List<FreightActivity> Activities { get; set; } = [];
    public List<FreightEvent> Events { get; set; } = [];

    public string Status(Guid id) => Events.LastOrDefault(e => e.ActivityId == id && e.TaskId is null)?.Action switch
    {
        "Start" => "In progress", "Complete" => "Completed", "Fail" => "Failed", "Cancel" => "Cancelled", _ => "Planned"
    };
    public bool TaskComplete(Guid id) => Events.Any(e => e.TaskId == id && e.Action == "Complete task");
    public bool Finished(Guid id) => Status(id) is "Completed" or "Failed" or "Cancelled";
    public decimal EstimatedCost => Activities.Sum(a => a.EstimatedCost + a.Tasks.Sum(t => t.EstimatedCost));
    public decimal PlannedCharge => Activities.Sum(a => a.PlannedCharge + a.Tasks.Sum(t => t.PlannedCharge));
    public decimal RecordedCost => Events.Sum(e => e.ActualCost ?? 0);
    public decimal StorageCharge(FreightActivity activity, DateTimeOffset asOf)
    {
        if (activity.Kind != "Storage") return 0;
        var start = Events.FirstOrDefault(e => e.ActivityId == activity.Id && e.Action == "Start");
        if (start is null) return 0;
        var end = Events.FirstOrDefault(e => e.ActivityId == activity.Id && e.Action is "Complete" or "Fail" or "Cancel");
        var hours = Math.Max(0, ((end?.RecordedAt ?? asOf) - start.RecordedAt).TotalHours - activity.StorageFreeHours);
        return (decimal)Math.Ceiling(hours / 24) * activity.StorageDailyCharge;
    }

    public void Validate()
    {
        if (!FreightVocabulary.BillTypes.Contains(BillType)) throw new FormatException("Choose a freight bill type.");
        Text(Service, 200, "Service"); Text(PricingAgreement, 200, "Pricing agreement");
        if (Currency is null || Currency.Length != 3 || Currency.Any(c => c < 'A' || c > 'Z')) throw new FormatException("Use a three-letter uppercase currency code.");
        if (References is null || References.Count > 100 || References.Any(r => r is null)) throw new FormatException("Up to 100 references are allowed.");
        foreach (var r in References)
        {
            if (!FreightVocabulary.ReferenceTypes.Contains(r.Type)) throw new FormatException("Choose a reference type.");
            Text(r.Partner, 200, "Reference partner"); Text(r.Value, 200, "Reference value", true);
            if (r.Type == "Partner PRO") Text(r.Partner, 200, "Partner for partner PRO", true);
        }
        if (Activities is null || Activities.Count > 100 || Activities.Any(a => a is null)) throw new FormatException("Up to 100 activities are allowed.");
        var ids = new HashSet<Guid>();
        foreach (var a in Activities)
        {
            Unique(a.Id, ids);
            if (!FreightVocabulary.Kinds.Contains(a.Kind) || !FreightVocabulary.Roles.Contains(a.Role) || !FreightVocabulary.Providers.Contains(a.ProviderType)) throw new FormatException("Choose an activity kind, role, and provider type.");
            Text(a.Provider, 200, "Provider", a.ProviderType != "Company"); Text(a.Instructions, 2000, "Activity instructions");
            if (a.Origin is null) throw new FormatException("An activity needs a location.");
            a.Origin.Validate();
            if (a.Kind == "Movement")
            {
                if (!FreightVocabulary.MovementTypes.Contains(a.MovementType) || a.Destination is null) throw new FormatException("A movement needs a type and destination.");
                a.Destination.Validate();
                if (string.IsNullOrWhiteSpace(a.Origin.Address) || string.IsNullOrWhiteSpace(a.Destination.Address)) throw new FormatException("Movement origins and destinations need street addresses.");
            }
            else if (a.Destination is not null) throw new FormatException("Warehouse and storage activities have one location, not a movement destination.");
            Money(a.EstimatedCost); Money(a.PlannedCharge); Money(a.StorageDailyCharge);
            if (a.StorageFreeHours is < 0 or > 87600 || (a.Kind != "Storage" && (a.StorageFreeHours != 0 || a.StorageDailyCharge != 0))) throw new FormatException("Storage rules apply only to storage activities; free hours must be between 0 and 87,600.");
            if (a.Tasks is null || a.Tasks.Count > 100 || a.Tasks.Any(t => t is null)) throw new FormatException("Up to 100 tasks per activity are allowed.");
            foreach (var t in a.Tasks)
            {
                Unique(t.Id, ids);
                if (!FreightVocabulary.TaskTypes.Contains(t.Type)) throw new FormatException("Choose a task type.");
                Text(t.Instructions, 2000, "Task instructions"); Money(t.EstimatedCost); Money(t.PlannedCharge);
            }
            if (a.RetryOf is {} retry && (a.Kind != "Movement" || !Activities.TakeWhile(x => x.Id != a.Id).Any(x => x.Id == retry && x.Kind == "Movement") || Status(retry) != "Failed"))
                throw new FormatException("A delivery attempt must refer to an earlier failed movement.");
        }
    }

    // The server owns execution history. Once work starts, its definition and sequence are frozen.
    public void ValidateReplacement(FreightWorkflow current)
    {
        Events = current.Events;
        Validate();
        if (current.Events.Count == 0) return;
        if (Currency != current.Currency) throw new FormatException("Currency cannot change after execution starts.");
        var lastStarted = current.Activities.FindLastIndex(a => current.Events.Any(e => e.ActivityId == a.Id));
        for (var i = 0; i <= lastStarted; i++)
            if (Activities.Count <= i || JsonSerializer.Serialize(Activities[i]) != JsonSerializer.Serialize(current.Activities[i]))
                throw new FormatException("Started activities and their preceding sequence cannot be edited or removed. Add a new activity for further work.");
    }

    // Return false for an identical retry, before checking revision. A reused key with different data is invalid.
    public bool Execute(FreightExecution command, Guid userId, DateTimeOffset now)
    {
        if (command.CommandId == Guid.Empty || userId == Guid.Empty) throw new FormatException("Execution requires a command and an authenticated user.");
        var duplicate = Events.SingleOrDefault(e => e.Id == command.CommandId);
        if (duplicate is not null)
        {
            if (duplicate.ActivityId != command.ActivityId || duplicate.TaskId != command.TaskId || duplicate.Action != command.Action || duplicate.Note != command.Note || duplicate.Responsibility != command.Responsibility || duplicate.ActualCost != command.ActualCost || duplicate.RecordedBy != userId)
                throw new FormatException("This execution command was already used with different data.");
            return false;
        }
        if (command.Revision != Revision) throw new FreightConflictException();
        Validate();
        if (!FreightVocabulary.Actions.Contains(command.Action) || !FreightVocabulary.Responsibilities.Contains(command.Responsibility)) throw new FormatException("Choose an execution action and responsibility.");
        Text(command.Note, 2000, "Execution note", command.Action is "Fail" or "Cancel");
        if (command.ActualCost is {} cost) Money(cost);
        if (command.ActualCost is not null && command.Action is not ("Complete" or "Complete task" or "Fail")) throw new FormatException("Record actual costs when completing work or recording failure.");
        if ((command.Action == "Complete task") != (command.TaskId is not null)) throw new FormatException("Only task completion accepts a task identifier.");
        var index = Activities.FindIndex(a => a.Id == command.ActivityId);
        if (index < 0) throw new FormatException("Activity not found.");
        var a = Activities[index];
        var state = Status(a.Id);
        if (command.Action == "Start")
        {
            if (state != "Planned" || Activities.Take(index).Any(x => !Finished(x.Id))) throw new FormatException("Finish earlier activities before starting this one.");
        }
        else if (command.Action == "Cancel")
        {
            if (Finished(a.Id)) throw new FormatException("Finished activities cannot be cancelled.");
        }
        else
        {
            if (state != "In progress") throw new FormatException("Start the activity before recording its outcome.");
            if (command.Action == "Complete task")
            {
                var taskIndex = a.Tasks.FindIndex(t => t.Id == command.TaskId);
                if (taskIndex < 0 || TaskComplete(command.TaskId!.Value)) throw new FormatException("Task is missing or already completed.");
                if (a.Tasks.Take(taskIndex).Any(t => t.Required && !TaskComplete(t.Id))) throw new FormatException("Complete earlier required tasks first.");
            }
            if (command.Action == "Complete" && a.Tasks.Any(t => t.Required && !TaskComplete(t.Id))) throw new FormatException("Complete all required tasks before finishing the activity.");
        }
        if (Events.Count >= 20000) throw new FormatException("This workflow has reached its execution event limit.");
        Events.Add(new(command.CommandId, a.Id, command.TaskId, command.Action, now.ToUniversalTime(), userId, command.Note, command.Responsibility, command.ActualCost));
        return true;
    }

    public static FreightWorkflow FromOrder(TmsOrder order, IReadOnlyList<TerminalDto> nodes)
    {
        FreightLocation Location(Guid? nodeId, bool origin)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null) return FreightLocation.From(node);
            return origin
                ? new() { Name = order.Shipper, Address = order.OriginAddress, City = order.OriginCity, State = order.OriginState, PostalCode = order.OriginPostalCode, Country = order.OriginCountry }
                : new() { Name = order.Customer, Address = order.Address, City = order.City, State = order.State, PostalCode = order.PostalCode, Country = order.Country };
        }
        return new() { Service = order.ServiceType, References = [new() { Type = "Company PRO", Value = order.Id }],
            Activities = [new() { Origin = Location(order.PickupNodeId, true), Destination = Location(order.DeliverToNodeId, false) }] };
    }

    internal static void Text(string? value, int max, string name, bool required = false)
    {
        if (value is null || value.Length > max || (required && string.IsNullOrWhiteSpace(value))) throw new FormatException($"{name} {(required ? "is required and " : "")}allows up to {max:N0} characters.");
    }
    static void Money(decimal value) { if (value < 0 || value > 1_000_000_000 || decimal.Round(value, 2) != value) throw new FormatException("Amounts must be between 0 and 1,000,000,000 with at most two decimal places."); }
    static void Unique(Guid id, HashSet<Guid> ids) { if (id == Guid.Empty || !ids.Add(id)) throw new FormatException("Activities and tasks need distinct nonempty identifiers."); }
}

public sealed class FreightConflictException : Exception
{
    public FreightConflictException() : base("The freight workflow changed. Reload it before trying again.") { }
}
