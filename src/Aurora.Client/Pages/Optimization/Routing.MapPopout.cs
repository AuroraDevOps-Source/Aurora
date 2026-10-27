using Aurora.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
namespace Aurora.Client.Pages.Optimization;
public partial class Routing
{
    [SupplyParameterFromQuery(Name="mapOnly")] public bool MapOnly {get;set;}
    private DotNetObjectReference<Routing>? _mapReference;
    public sealed record MapSnapshot(OptimizationResultDto? Result, string? SourceJson);
    [JSInvokable] public Task ReceiveMapSnapshot(MapSnapshot? snapshot)
    {
        if(!MapOnly || _disposed)return Task.CompletedTask;
        _result=snapshot?.Result;_sourceJson=snapshot?.SourceJson;
        _error=_result is null?"The planner is no longer showing a route. Return to the planner to continue.":null;
        if(_selectedVehicle is not null && _result?.Routes.Any(r=>r.Vehicle==_selectedVehicle)!=true)_selectedVehicle=null;
        _renderMap=_result is not null;
        return InvokeAsync(StateHasChanged);
    }
}
