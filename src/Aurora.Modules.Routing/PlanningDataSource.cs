using Aurora.Contracts;
using Npgsql;

namespace Aurora.Modules.Routing;

// Transitional boundary: the Nova adapter implements this while Aurora's local
// implementation is introduced. Core planning has no Firebird driver dependency.
public interface IPlanningDataSource
{
    bool Enabled { get; }
    DateOnly PlanningToday { get; }
    Task Lock(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct);
    Task<TerminalDto[]> ReadTerminals(CancellationToken ct);
    Task<EquipmentCatalogDto> ReadFleet(CancellationToken ct);
    Task ResolveNodeForPlanning(TerminalDto node, CancellationToken ct);
    Task<string> ResolveOrdersForPlanning(string json, CancellationToken ct);
    Task RejectConsolidationOrders(string json, CancellationToken ct);
    Task<(string Request,ReplanManifest[] Manifests)> IncludeOpenManifests(string request,TerminalDto origin,TerminalDto? destination,DateOnly date,CancellationToken ct);
    string PrepareDraft(string json, TerminalDto node);
    void ValidateSource(string json);
    Task<string> ExcludeBookedTrucks(string request,DateOnly date,CancellationToken ct);
    Task<string> ApplyDrivers(string json,string basis,bool initialize,CancellationToken ct);
    Task<Dictionary<string,long>> Export(Guid draftId,Guid sessionId,string draftJson,IReadOnlyList<RouteSummaryDto> routes,CancellationToken ct,bool rollbackOnly=false);
}

public interface IExternalWorkspaceReset
{
    bool Enabled { get; }
    Task<NovaRoutingConfiguration> RoutingConfiguration(CancellationToken ct);
    Task<long[]> ResetManifestIds(CancellationToken ct);
    Task<int> ResetManifests(long[] ids,int readyStatus,CancellationToken ct,bool rollbackOnly=false);
}

public interface ILocalPlanningDataSource:IPlanningDataSource
{
    Task<string> Snapshot(Guid sourceId,string[] ids,CancellationToken ct);
    Task ValidateCommit(string request,IReadOnlyList<RouteSummaryDto> routes,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct);
}
