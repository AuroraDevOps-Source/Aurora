using System.Text.Json.Nodes;
using Aurora.Modules.Sync;
using Npgsql;

namespace Aurora.Modules.Routing;

// Registered by the host's business-entity dispatcher, never by the Nova provider.
public sealed class DriverSyncEntities(DriverStore drivers):ISyncEntityStore
{
    public async Task<SyncEntity?> Read(string entityType,string localId,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        RequireDriver(entityType);
        await drivers.Lock(db,tx,ct);
        return await drivers.Read(localId,db,tx,ct);
    }
    public async Task<SyncEntity> Write(string entityType,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        RequireDriver(entityType);
        await drivers.Lock(db,tx,ct);
        return await drivers.Write(localId,data,deleted,db,tx,ct);
    }
    static void RequireDriver(string entityType)
    {
        if(entityType!="driver")throw new FormatException("This importer only handles driver records.");
    }
}
