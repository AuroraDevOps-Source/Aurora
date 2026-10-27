using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Microsoft.JSInterop;

namespace Aurora.Client.Pages.Optimization;

public partial class Routing
{
    [Microsoft.AspNetCore.Components.SupplyParameterFromQuery(Name = "draft")]
    public Guid? WorkspaceDraftId { get; set; }
    [Microsoft.AspNetCore.Components.SupplyParameterFromQuery(Name = "debugOptimization")]
    public bool DebugOptimization { get; set; }
    private string PlanningStorageKey => "aurora-planning-session" + (WorkspaceDraftId is { } id ? "-" + id : "");
    private Guid? _completedSessionId;
    private bool _workspaceFinished, _savingManifest, _planCancelled, _cancellingPlan;
    private string? _planName;
    private int AcceptedRoutes => _result?.Routes.Count(r => r.Pros.Count > 0) ?? 0;
    private ReplanManifest[] IncludedManifests => _sourceJson is null?[]:ManifestReplanning.Included(_sourceJson);
    private string[] UnscheduledManifestOrders => IncludedManifests.SelectMany(m=>m.OrderIds).Where(id=>_result is not null && !_result.Routes.Any(r=>r.Pros.Any(p=>p.ProNumber==id))).ToArray();
    private async Task CancelPlan()
    {
        if (WorkspaceDraftId is not { } draft || IsWorking || _workspaceFinished || _planCancelled) return;
        _cancellingPlan = true; _error = null;
        try
        {
            using var response = await Http.PostAsync($"api/v1/aurora/drafts/{draft}/cancel", null);
            if (!response.IsSuccessStatusCode) throw new FormatException((await ReadApiError(response)).Error);
            _planCancelled = true;
            // Browser storage cleanup must not strand a successfully cancelled plan.
            try { await JS.InvokeVoidAsync("sessionStorage.removeItem", PlanningStorageKey); } catch (JSException) { }
            Navigation.NavigateTo("/aurora/orders", replace: true);
        }
        catch (Exception ex) { _error = "Could not cancel the plan. " + ex.Message; }
        finally { _cancellingPlan = false; }
    }
    private async Task FinishWorkspace()
    {
        if (WorkspaceDraftId is not { } draft || _completedSessionId is not { } session || _savingManifest || _workspaceFinished) return;
        _savingManifest = true; _error = null;
        try
        {
            using var response = await Http.PostAsJsonAsync($"api/v1/aurora/drafts/{draft}/finish", new FinishOrderDraftDto(session));
            if (!response.IsSuccessStatusCode) throw new FormatException((await ReadApiError(response)).Error);
            _workspaceFinished = true;
            try { await JS.InvokeVoidAsync("auroraWorkspace.changed"); } catch (JSException) { }
            Navigation.NavigateTo("/aurora/manifests");
        }
        catch (Exception ex)
        {
            if (_workspaceFinished) Navigation.NavigateTo("/aurora/manifests");
            else _error = "Could not confirm manifest creation. You can retry Finish safely. " + ex.Message;
        }
        finally { _savingManifest = false; }
    }
    private bool _disposed, _showPlanning, _accepting;
    private bool _reusePrevious = true;
    private Guid? _sessionId;
    private string? _resultInputJson;
    private PlanningSessionDto? _session;
    private void ApplyFleet(string json)
    {
        SetInput(json, _sourceName, preserveResult: true);
        _setupMessage = _result is null ? null : "Truck settings updated. Re-optimize to update the routes.";
    }
    private async Task<bool> StartPlanning(string json, string fileName, bool quick)
    {
        if (IsWorking) return false;
        var inputs = RoutingInput.Parse(json);
        inputs["settings"] ??= new JsonObject();
        const int duration = 10;
        inputs["settings"]!["duration"] = duration;
        json = inputs.ToJsonString();
        SetInput(json, fileName, preserveResult: true);
        _busy = true; _error = null; _showPlanning = true;
        _sessionId = Guid.NewGuid(); _session = null;
        _busyLabel = "Submitting your plan..."; _setupMessage = null;
        StateHasChanged();
        var id = _sessionId.Value;
        try
        {
            await JS.InvokeVoidAsync("sessionStorage.setItem", PlanningStorageKey, id.ToString());
            using var response = await Http.PostAsJsonAsync("api/v1/routing/sessions",
                new StartPlanningDto(id, fileName, json, CanQuickUpdate && (quick || _reusePrevious) ? _previousResult!.RawResponse : null, (int)duration, false, WorkspaceDraftId));
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadApiError(response);
                _error = error?.Error + (error?.Details is { Count: > 0 } ? " " + string.Join(" ", error.Details) : "");
                if ((int)response.StatusCode == 400) { _sessionId = null; await JS.InvokeVoidAsync("sessionStorage.removeItem", PlanningStorageKey); }
                return false;
            }
            _session = await response.Content.ReadFromJsonAsync<PlanningSessionDto>();
            return await MonitorPlanning();
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _error = "Could not confirm the update. Reconnect to this session before starting another run. " + ex.Message; return false; }
        finally { _busy = false; }
    }
    private async Task<bool> MonitorPlanning()
    {
        if (_disposed) return false;
        _cts?.Dispose(); _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var restoreInputs = true;
        while (_sessionId is { } id && !ct.IsCancellationRequested)
        {
            using var response = await Http.GetAsync($"api/v1/routing/sessions/{id}" + (restoreInputs ? "?inputs=true" : ""), ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadApiError(response);
                _error = error?.Error ?? "Connection interrupted. Reconnect to keep monitoring.";
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) { _sessionId = null; await JS.InvokeVoidAsync("sessionStorage.removeItem", PlanningStorageKey); }
                return false;
            }
            var update = await response.Content.ReadFromJsonAsync<PlanningSessionDto>(cancellationToken: ct);
            if (_sessionId != id || _accepting) { if (_sessionId != id) return false; await Task.Delay(200,ct); continue; }
            _session = update;
            if (_session is null) throw new InvalidOperationException("The planning session was empty.");
            if (restoreInputs && !string.IsNullOrEmpty(_session.RequestJson)) SetInput(_session.RequestJson, _session.FileName, preserveResult: true);
            restoreInputs = false;
            _busyLabel = _session.Status switch { "QUEUING" => "Waiting for a planning slot", "PREPARING" => "Preparing travel times", "RUNNING" => "Searching for a better plan", "STOPPING" => "Stopping and retrieving the best plan", _ => _session.Status };
            if (_session.Status == "SUBMITTING") { _error = _session.Warning; return false; }
            if (_session.Status == "FAILED")
            {
                _error = _session.Warning ?? "The plan failed. Review the inputs and try again.";
                if (_session.Candidates.Count == 0) { _sessionId = null; await JS.InvokeVoidAsync("sessionStorage.removeItem", PlanningStorageKey); }
                return false;
            }
            if (_session.AcceptedCandidateId is not null && _session.Result is { } accepted)
            {
                await UsePlan(accepted,id); return true;
            }
            if (_session.Status == "SUCCEEDED")
            {
                if (!DebugOptimization)
                {
                    var best = PlanningCandidateSelection.LeastMiles(_session.Candidates);
                    if (best is null) { _error = "The search finished without a usable route distance. Reconnect to retry retrieving the result."; return false; }
                    _busyLabel = "Applying the plan with the fewest miles…";
                    StateHasChanged();
                    await AcceptCandidate(best.Id);
                    return _completedSessionId == id;
                }
                _busyLabel = "Search complete. Choose a plan to use.";
                StateHasChanged(); return false;
            }
            StateHasChanged();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
        return false;
    }
    private async Task UsePlan(OptimizationResultDto result, Guid id)
    {
        if (_previousResult?.Summary is { } baseline) _comparison = new(_previousResult.FileName, baseline, _previousResult.Routes);
        await JS.InvokeVoidAsync("routeMap.dispose");
        _resultInputJson = string.IsNullOrEmpty(_session?.RequestJson) ? _sourceJson : _session.RequestJson;
        _result=result; _previousResult=result; _completedSessionId=id;
        _sessionId=null; _selectedVehicle=null; _tab=ResultTab.Manifest;
        _renderMap=true; _error=null;
        _showPlanning=false; _setupMessage=null;
        await JS.InvokeVoidAsync("sessionStorage.setItem",PlanningStorageKey,id.ToString());
        StateHasChanged();
    }

    private bool IsRemoved(string orderId) => _sourceJson is not null && PlanOrderDecisions.Get(RoutingInput.Parse(_sourceJson), orderId) == PlanOrderDecisions.Excluded;
    private IReadOnlyList<DroppedOrderDto> LeftOffOrders => _sourceJson is not null && _result is not null ? PlannerLeftOff.Read(_sourceJson, _result) : [];
    private bool CanEditOrders => !IsWorking && !_workspaceFinished && !_planCancelled && !_appointmentEditing;
    private DotNetObjectReference<Routing>? _dragReference;
    [JSInvokable]
    public Task MoveDelivery(string order, string truck) => InvokeAsync(() => MoveOrder((order, truck)));
    private async Task MoveOrder((string Order, string Truck) move)
    {
        if (!CanEditOrders || !CanOptimize || _sourceJson is null) return;
        try
        {
            ApplyOrderDecision(PlanOrderDecisions.Set(_sourceJson, move.Order, "truck:" + move.Truck));
            await Optimize();
        }
        catch (Exception ex) { _error = "Could not move the delivery. " + ex.Message; }
    }
    private void ApplyOrderDecision(string json)
    {
        if (!CanEditOrders) return;
        if (_result is not null) json = PlanOrderDecisions.PreserveLeftOff(json, _result);
        SetInput(json, _sourceName, preserveResult: true);
        _setupMessage = "Re-optimize to update the routes and map.";
    }
    private IReadOnlyList<string> UnscheduledPins
    {
        get
        {
            if (_completedSessionId is null || _result is null || _sourceJson is null) return [];
            return PlanOrderDecisions.UnscheduledPins(_sourceJson, _result);
        }
    }
    private async Task AcceptCandidate(string candidateId)
    {
        if (_sessionId is not { } id || _accepting) return;
        _accepting=true; _error=null;
        try
        {
            using var response=await Http.PostAsync($"api/v1/routing/sessions/{id}/accept/{Uri.EscapeDataString(candidateId)}",null);
            if (!response.IsSuccessStatusCode) { _error=(await ReadApiError(response)).Error; return; }
            var accepted=await response.Content.ReadFromJsonAsync<PlanningSessionDto>();
            if (accepted?.Result is null) throw new InvalidOperationException("The selected plan was not returned.");
            _session=accepted;
            await UsePlan(accepted.Result,id);
            _cts?.Cancel();
        }
        catch(Exception ex) { _error="Could not accept this plan. Reconnect and retry the same result. "+ex.Message; }
        finally { _accepting=false; }
    }
    private async Task ResumePlanning()
    {
        if (_busy) return;
        _busy = true; _error = null; _showPlanning = true;
        try { await MonitorPlanning(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _error = "Could not reconnect. The optimization has not been resubmitted. " + ex.Message; }
        finally { _busy = false; }
    }
    private static async Task<ApiErrorDto> ReadApiError(HttpResponseMessage response)
    {
        try { return await response.Content.ReadFromJsonAsync<ApiErrorDto>() ?? new ApiErrorDto($"Server returned {(int)response.StatusCode}."); }
        catch (System.Text.Json.JsonException) { return new ApiErrorDto($"Server returned {(int)response.StatusCode}. Reconnect to the current session before starting another run."); }
    }
}
