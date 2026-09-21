using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aurora.Infrastructure.Data;

/// <summary>
/// OpenIddict's EF entities use plain <see cref="DateTime"/> (not <see cref="DateTimeOffset"/>).
/// Forced to UTC + timestamptz model-wide in <see cref="AuroraDbContext.ConfigureConventions"/>
/// so Npgsql doesn't default them to "timestamp without time zone" and doesn't throw on a
/// non-UTC Kind (doc §11 wants timestamptz everywhere anyway).
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue && v.Value.Kind != DateTimeKind.Utc ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
