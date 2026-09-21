namespace Aurora.Contracts;

/// <summary>
/// CreatedUtc is <see cref="DateTime"/>, not <see cref="DateTimeOffset"/>: the column is
/// timestamptz and Npgsql hands those back as DateTime with Kind=Utc, which is what Dapper
/// matches against this record's constructor.
/// </summary>
public sealed record RoutePlanDto(Guid Id, string Name, DateTime CreatedUtc);
