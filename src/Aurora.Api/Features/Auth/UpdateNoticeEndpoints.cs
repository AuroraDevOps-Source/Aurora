using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Dapper;

namespace Aurora.Api.Features.Auth;

public static class UpdateNoticeEndpoints
{
    public static void MapUpdateNoticeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/update-notice", async (HttpContext context, NpgsqlConnectionFactory connections, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            await using var db = await connections.OpenConnectionAsync(ct);
            var row = await db.QuerySingleOrDefaultAsync<NoticeRow>(new CommandDefinition(
                "SELECT update_at AS UpdateAt, message_template AS MessageTemplate FROM public.update_notice WHERE update_at + interval '60 seconds' > now()", cancellationToken: ct));
            return Results.Ok(new UpdateNotice(row?.UpdateAt, row?.MessageTemplate));
        }).RequireAuthorization();

        var admin = app.MapGroup("/api/v1/admin/update-notice").RequireAuthorization(p => p.RequireRole("aurora:Admin"));
        admin.MapGet("/can-publish", async (NpgsqlConnectionFactory connections, CancellationToken ct) =>
        {
            await using var db = await connections.OpenConnectionAsync(ct);
            return Results.Ok(await db.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT public.can_edit_global_document_settings()", cancellationToken: ct)));
        });
        admin.MapPost("/", async (UpdateNotice notice, NpgsqlConnectionFactory connections, CancellationToken ct) =>
        {
            if (notice.UpdateAt is not {} scheduled || scheduled <= DateTimeOffset.UtcNow)
                return Results.BadRequest(new ApiErrorDto("Choose a future update time."));
            var message = notice.MessageTemplate?.Trim();
            if (message is { Length: 0 or > UpdateNotice.MaxMessageLength })
                return Results.BadRequest(new ApiErrorDto($"Enter a message of up to {UpdateNotice.MaxMessageLength} characters."));
            await using var db = await connections.OpenConnectionAsync(ct);
            var rows = await db.ExecuteAsync(new CommandDefinition(
                "UPDATE public.update_notice SET update_at = @scheduled, message_template = @message WHERE singleton AND public.can_edit_global_document_settings()",
                new { scheduled = scheduled.UtcDateTime, message }, cancellationToken: ct));
            return rows == 1 ? Results.NoContent() : Results.Forbid();
        });
    }
    private sealed record NoticeRow(DateTime UpdateAt, string? MessageTemplate);
}
