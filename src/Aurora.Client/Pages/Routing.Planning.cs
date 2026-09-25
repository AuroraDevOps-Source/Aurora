using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Microsoft.JSInterop;

namespace Aurora.Client.Pages;

public partial class Routing
{
    [Microsoft.AspNetCore.Components.SupplyParameterFromQuery(Name = "draft")]
    public Guid? WorkspaceDraftId { get; set; }
    private string PlanningStorageKey => "aurora-planning-session" + (WorkspaceDraftId is { } id ? "-" + id : "");
    private Guid? _completedSessionId;
    private bool _workspaceFinished, _savingManifest;
    private async Task FinishWorkspace()
    {
        if (WorkspaceDraftId is not { } draft || _completedSessionId is not { } session || _savingManifest || _workspaceFinished) return;
        _savingManifest = true; _error = null;
        try
        {
            using var response = await Http.PostAsJsonAsync($"api/v1/aurora/drafts/{draft}/finish", new FinishOrderDraftDto(session));
            if (!response.IsSuccessStatusCode) throw new FormatException((await ReadApiError(response)).Error);
            _workspaceFinished = true;
            await JS.InvokeVoidAsync("auroraWorkspace.finish");
        }
        catch (Exception ex)
        {
            if (_workspaceFinished) Navigation.NavigateTo("/aurora/manifests");
            else _error = "Could not confirm manifest creation. You can retry Finish safely. " + ex.Message;
        }
        finally { _savingManifest = false; }
    }
    private int _step = 1;
    private bool _keepAssignments, _stopping, _disposed;
    private bool _reusePrevious = true;
    private Guid? _sessionId;
    private string? _resultInputJson;
    private PlanningSessionDto? _session;
    private void ApplyFleet(string json) => SetInput(json, _sourceName);
    private async Task GoToStep(int step)
    {
        if (IsWorking) return;
        if (step == 3 && _completedSessionId is null && CanOptimize) { await Optimize(); return; }
        await JS.InvokeVoidAsync("routeMap.dispose");
        if (step == 4) _result = _previousResult;
        _step = step;
        _renderMap = step == 4 && _result is not null;
    }
    private async Task<bool> StartPlanning(string json, string fileName, bool quick)
    {
        if (IsWorking) return false;
        var inputs = RoutingInput.Parse(json);
        inputs["settings"] ??= new JsonObject();
        inputs["settings"]!["duration"] = 60;
        json = inputs.ToJsonString();
        SetInput(json, fileName);
        _busy = true; _error = null; _step = 3; _variationOpen = false;
        _sessionId = Guid.NewGuid(); _session = null;
        _busyLabel = "Submitting your plan...";
        StateHasChanged();
        var id = _sessionId.Value;
        try
        {
            await JS.InvokeVoidAsync("sessionStorage.setItem", PlanningStorageKey, id.ToString());
            using var response = await Http.PostAsJsonAsync("api/v1/routing/sessions",
                new StartPlanningDto(id, fileName, json, CanQuickUpdate && _reusePrevious ? _previousResult!.RawResponse : null, 60, _keepAssignments && _reusePrevious, WorkspaceDraftId));
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
            _session = await response.Content.ReadFromJsonAsync<PlanningSessionDto>(cancellationToken: ct);
            if (_session is null) throw new InvalidOperationException("The planning session was empty.");
            if (restoreInputs && !string.IsNullOrEmpty(_session.RequestJson)) SetInput(_session.RequestJson, _session.FileName);
            restoreInputs = false;
            _busyLabel = _session.Status switch { "QUEUING" => "Waiting for a planning slot", "PREPARING" => "Preparing travel times", "RUNNING" => "Searching for a better plan", "STOPPING" => "Stopping and retrieving the best plan", _ => _session.Status };
            if (_session.Status == "SUBMITTING") { _error = _session.Warning; return false; }
            if (_session.Status == "FAILED")
            {
                _error = _session.Warning ?? "The plan failed. Review the inputs and try again.";
                _sessionId = null; await JS.InvokeVoidAsync("sessionStorage.removeItem", PlanningStorageKey); return false;
            }
            if (_session.Status == "SUCCEEDED" && _session.Result is { } result)
            {
                if (_previousResult?.Summary is { } baseline) _comparison = new(_previousResult.FileName, baseline, _previousResult.Routes);
                await JS.InvokeVoidAsync("routeMap.dispose");
                _resultInputJson = string.IsNullOrEmpty(_session.RequestJson) ? _sourceJson : _session.RequestJson;
                _result = result; _previousResult = result; _completedSessionId = id;
                _sessionId = null; _step = 3; _selectedVehicle = result.Routes.FirstOrDefault()?.Vehicle; _tab = ResultTab.Manifest;
                _renderMap = false; _error = null;
                StateHasChanged(); return true;
            }
            StateHasChanged();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
        return false;
    }
    private async Task ResumePlanning()
    {
        if (_busy) return;
        _busy = true; _error = null; _step = 3;
        try { await MonitorPlanning(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _error = "Could not reconnect. The PTV job has not been resubmitted. " + ex.Message; }
        finally { _busy = false; }
    }
    private static async Task<ApiErrorDto> ReadApiError(HttpResponseMessage response)
    {
        try { return await response.Content.ReadFromJsonAsync<ApiErrorDto>() ?? new ApiErrorDto($"Server returned {(int)response.StatusCode}."); }
        catch (System.Text.Json.JsonException) { return new ApiErrorDto($"Server returned {(int)response.StatusCode}. Reconnect to the current session before starting another run."); }
    }
    private async Task StopPlanning()
    {
        if (_sessionId is not { } id || _stopping) return;
        _stopping = true; _error = null;
        try
        {
            using var response = await Http.PostAsync($"api/v1/routing/sessions/{id}/stop", null);
            if (!response.IsSuccessStatusCode) { var error = await ReadApiError(response); _error = error?.Error ?? "Stop was not confirmed. Retry while monitoring."; }
            else { _session = await response.Content.ReadFromJsonAsync<PlanningSessionDto>(); if (!_busy) await ResumePlanning(); }
        }
        catch (Exception ex) { _error = "Stop was not confirmed. " + ex.Message; }
        finally { _stopping = false; }
    }
}
