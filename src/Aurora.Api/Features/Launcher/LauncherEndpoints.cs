using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Api.Features.Launcher;

public static class LauncherEndpoints
{
    /// <summary>
    /// Products this tenant has bought, and whether the signed-in user holds any role in each.
    /// Drives the launcher tiles. Deliberately an endpoint rather than a token claim: an
    /// entitlement list in the token goes stale the moment a product is bought or cancelled,
    /// and would grow the token for every request that never needs it.
    /// </summary>
    private const string Sql =
        """
        SELECT p.code                                   AS "Code",
               p.name                                   AS "Name",
               p.description                            AS "Description",
               tp.instance_url                          AS "Url",
               EXISTS (
                   SELECT 1
                   FROM "AspNetUserRoles" ur
                   JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                   WHERE ur."UserId"   = @UserId
                     AND ur."TenantId" = @TenantId
                     AND r."Name" LIKE p.role_prefix || ':%'
               )                                        AS "HasAccess"
        FROM tenant_product tp
        JOIN product p ON p.code = tp.product_code
        WHERE tp.tenant_id = @TenantId
          AND tp.is_active
        ORDER BY p.sort_order;
        """;

    public static void MapLauncherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me").RequireAuthorization();

        group.MapGet("/tenant", async (NpgsqlConnectionFactory factory, ITenantContext tenant, CancellationToken ct) =>
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            var company=await db.QuerySingleAsync<WorkspaceTenantDto>(new CommandDefinition("SELECT id,name FROM tenant WHERE id=@TenantId AND is_active",new {tenant.TenantId},cancellationToken:ct));
            return Results.Ok(company);
        });

        group.MapGet("/products", async (
            NpgsqlConnectionFactory connectionFactory,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

            var products = await connection.QueryAsync<ProductDto>(new CommandDefinition(
                Sql,
                new { UserId = tenantContext.UserId, TenantId = tenantContext.TenantId },
                cancellationToken: cancellationToken));

            return Results.Ok(products.AsList());
        });
    }
}
