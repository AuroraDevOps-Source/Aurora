using System.Net.Http.Json;
using Aurora.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aurora.Client.Pages.Shared;

public partial class NovaRecords
{
    readonly HashSet<long> _selectedOrders = [];
    Dictionary<long, TmsOrder> _planningOrders = [];
    OpenOrderDraftDto[] _openPlans = [];
    long? _selectionAnchor;
    int _selectionVersion;
    string? _planningError;
    string[] _planningWarnings=[];
    HashSet<long> _consolidationOrders=[];
    bool _showPlanSchedule;
    OrderDraftDto[] _createdPlans = [];
    DateTime? _planDate = DateTime.Today;
    DateTime? _planReturnDate;
    double _tripDriving=10,_tripWorking=12,_tripRest=12;
    TimeOnly? _planDepart = new(6,0), _planReturn = new(18,0);
    void StartPlan()
    {
        if (_busy || _selectedOrders.Count == 0 || _planningError is not null) return;
        _planDate=DateTime.Today; _planReturnDate=null; _planDepart=new(6,0); _planReturn=new(18,0);
        _error=null; _showPlanSchedule=true;
    }
    void ClosePlanSchedule() { if (!_busy) { _showPlanSchedule=false; _error=null; } }


    TmsOrder? PlanningOrder(NovaRecordSummary row) => _planningOrders.GetValueOrDefault(row.Id) is { } order
        && string.Equals(order.TerminalCode, row.Terminal, StringComparison.OrdinalIgnoreCase) ? order : null;
    bool CanSelect(NovaRecordSummary row) => _planningError is null && PlanningOrder(row) is { Status: WorkspaceOrderStatus.Ready, ManifestId: null, OptimizationDraftId: null };
    IEnumerable<NovaRecordSummary> PageAvailable => _rows.Where(CanSelect);
    bool AllPageSelected => PageAvailable.Any() && PageAvailable.All(row => _selectedOrders.Contains(row.Id));
    string SelectionHint(NovaRecordSummary row) => CanSelect(row) ? "Shift-click to select a range on this page"
        : _planningError is not null ? "Planning availability could not be loaded. Refresh to retry."
        : _consolidationOrders.Contains(row.Id)?"Customer consolidation: held or assigned automatically without route optimization."
        : PlanningOrder(row)?.OptimizationDraftId is not null ? "Already in an open plan. Resume or cancel that plan to release this order."
        : PlanningOrder(row)?.ManifestId is not null ? "Already assigned to a manifest."
        : row.Issues.Length > 0 ? string.Join(" ", row.Issues.Select(issue => issue.Message))
        : "No available planning record. Check the order's status and pickup/delivery manifest in Nova, then refresh.";
    void ClearOrderSelection() { _selectedOrders.Clear(); _selectionAnchor = null; _selectionVersion++; }
    void SelectPage(ChangeEventArgs e)
    {
        if (_busy) return;
        foreach (var row in PageAvailable)
            if (e.Value is true) _selectedOrders.Add(row.Id); else _selectedOrders.Remove(row.Id);
        _selectionAnchor = null;
        _selectionVersion++;
    }
    void SelectOrder(NovaRecordSummary row, MouseEventArgs e)
    {
        if (_busy || !CanSelect(row)) return;
        var anchor = Array.FindIndex(_rows, r => r.Id == _selectionAnchor);
        var target = Array.FindIndex(_rows, r => r.Id == row.Id);
        if (e.ShiftKey && anchor >= 0 && target >= 0)
        {
            for (var i = Math.Min(anchor, target); i <= Math.Max(anchor, target); i++)
                if (CanSelect(_rows[i])) _selectedOrders.Add(_rows[i].Id);
        }
        else
        {
            if (!_selectedOrders.Add(row.Id)) _selectedOrders.Remove(row.Id);
            _selectionAnchor = row.Id;
        }
        // Recreate checkboxes so a Shift-click on an already selected endpoint stays checked.
        _selectionVersion++;
    }
    async Task LoadPlanningOrders()
    {
        _planningError = null;
        try
        {
            using var refresh = await PlanningLoadRetry.SendAsync(() => Http.PostAsync("api/v1/aurora/firebird/refresh", null));
            await Check(refresh);
            var sync = await refresh.Content.ReadFromJsonAsync<FirebirdSyncResult>() ?? throw new FormatException("Orders could not be refreshed for planning.");
            _planningWarnings=sync.Warnings;_consolidationOrders=sync.ConsolidationOrders.ToHashSet();
            using var response = await PlanningLoadRetry.SendAsync(() => Http.GetAsync("api/v1/tms/orders?status=ReadyToRoute"));
            await Check(response);
            var orders = await response.Content.ReadFromJsonAsync<TmsOrder[]>() ?? [];
            // Native IDs, not display PRO numbers, identify the corresponding planning snapshots.
            _planningOrders = orders.Where(o => sync.SourceIds.Contains(o.SourceId) && o.Id.StartsWith("FB-", StringComparison.Ordinal))
                .Select(o => (Order: o, Id: o.NativeOrderId ?? (long.TryParse(o.Id.AsSpan(3), out var id) ? id : 0)))
                .Where(x => x.Id > 0).GroupBy(x => x.Id).Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single().Order);
            _selectedOrders.RemoveWhere(id => !_planningOrders.TryGetValue(id, out var o) || o.ManifestId is not null || o.OptimizationDraftId is not null);
        }
        catch (Exception ex)
        {
            _planningOrders.Clear(); ClearOrderSelection();
            _planningError = $"Route planning is unavailable: {ex.Message}";
        }
        // Existing plans remain reachable even if refreshing new orders fails.
        using var drafts = await PlanningLoadRetry.SendAsync(() => Http.GetAsync("api/v1/aurora/drafts"));
        await Check(drafts);
        _openPlans = await drafts.Content.ReadFromJsonAsync<OpenOrderDraftDto[]>() ?? [];
    }
    Task CreatePlan() => Run(async () =>
    {
        if (_planDate is null) throw new FormatException("Choose a route date.");
        if (_planDepart is not {} depart || _planReturn is not {} back)
            throw new FormatException("Enter departure and return times.");
        if (depart==back && !(_planReturnDate>_planDate)) throw new FormatException("Return time must differ from departure.");
        var ids = _selectedOrders.ToArray();
        await LoadPlanningOrders();
        if (_planningError is not null) throw new FormatException(_planningError);
        if (ids.Length == 0 || ids.Any(id => !_selectedOrders.Contains(id)))
            throw new FormatException("Some selected orders are no longer available. Review your selection and try again.");
        var orders = ids.Select(id => _planningOrders[id]).ToArray();
        if (orders.Select(o => o.SourceId).Distinct().Count() != 1)
            throw new FormatException("Select orders from one terminal to plan routes.");
        using var response = await Http.PostAsJsonAsync("api/v1/aurora/plans", new CreateOrderDraftDto(orders[0].SourceId,
            orders.Select(o => o.Id).ToArray(), DateOnly.FromDateTime(_planDate.Value), depart.ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture), back.ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture),ReturnDate:_planReturnDate is {} rd?DateOnly.FromDateTime(rd):null,RestRules:new(_tripDriving,_tripWorking,_tripRest)));
        await Check(response);
        var plans = await response.Content.ReadFromJsonAsync<OrderDraftDto[]>() ?? throw new FormatException("The plans could not be opened.");
        if (plans.Length == 0) throw new FormatException("No plans were created.");
        _showPlanSchedule=false;
        ClearOrderSelection();
        if (plans.Length == 1) Navigation.NavigateTo($"/aurora/planning?draft={plans[0].Id}");
        else
        {
            _createdPlans=plans;
            await LoadPlanningOrders();
        }
    });
    Task CancelPlan(Guid id) => Run(async () =>
    {
        using var response = await Http.PostAsync($"api/v1/aurora/drafts/{id}/cancel", null);
        await Check(response);
        await LoadRows();
    });
}
