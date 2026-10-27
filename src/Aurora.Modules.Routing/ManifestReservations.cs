using System.Text.Json;
using Aurora.Contracts;
using Dapper;
using Npgsql;

namespace Aurora.Modules.Routing;

internal static class ManifestReservations
{
    // Caller holds the tenant planning lock through validation and the manifest write.
    public static async Task Validate(Guid tenantId, Guid id, string vehicle, string status,
        DateOnly date, ManifestDetailsDto details, NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct)
    {
        var end = details.ReturnDate ?? date;
        if (end < date) throw new FormatException("Return date cannot precede the manifest date.");
        details.ManifestDate = date;
        if (status == "Complete") return;
        if (details.DriverId is {} driverId)
        {
            var json = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
                SELECT (data || jsonb_build_object('Id',id,'Number',number,'Terminal',terminal))::text
                FROM aurora_driver WHERE tenant_id=@tenantId AND id=@driverId AND deleted_at IS NULL
                """, new { tenantId, driverId }, tx, cancellationToken: ct));
            var driver = json is null ? throw new FormatException("Choose a saved driver.") : JsonSerializer.Deserialize<NovaDriver>(json)!;
            if (details.OriginTerminal.Length > 0 && driver.Terminal != details.OriginTerminal)
                throw new FormatException("Choose a driver from the origin terminal.");
            if (DriverPlanning.Unavailable(driver, new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)) is {} problem)
                throw new FormatException($"Driver {driver.Number}: {problem}");
            details.Driver = driver.Name;
        }
        var conflict = await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM aurora_manifest
            WHERE tenant_id=@tenantId AND id<>@id AND deleted_at IS NULL AND status<>'Complete'
              AND ((@vehicle<>'' AND vehicle=@vehicle) OR (@driverId IS NOT NULL AND NULLIF(details->>'DriverId','')::bigint=@driverId))
              AND (status IN ('Dispatched','En Route','Arrived') OR @active
                OR manifest_date<=@end AND COALESCE(NULLIF(details->>'ReturnDate','')::date,manifest_date)>=@date))
            """, new { tenantId, id, vehicle, driverId=details.DriverId, date=date.ToDateTime(TimeOnly.MinValue),
                end=end.ToDateTime(TimeOnly.MinValue), active=status is "Dispatched" or "En Route" or "Arrived" }, tx, cancellationToken: ct));
        if (conflict) throw new FormatException("This truck or driver is reserved by another manifest.");
    }
}
