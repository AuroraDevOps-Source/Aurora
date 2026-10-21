using System.Text.Json;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Entities;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aurora.Api.Features.Auth;

public sealed class AdminUsers(AuroraDbContext db,UserManager<ApplicationUser> users,NpgsqlConnectionFactory factory,ITenantContext tenant)
{
    static string Role(string level)=>level switch {"Administrator"=>"aurora:Admin","Power User"=>"aurora:PowerUser","User"=>"aurora:User",_=>throw new FormatException("Choose Administrator, Power User, or User.")};
    const string UserSql="""
        SELECT u."Id",u."UserName",u."Email",u."ConcurrencyStamp" AS Revision,u."ManagementTenantId" AS Owner,
            m.nova_database_id AS DatabaseId, (SELECT name FROM nova_database d WHERE d.tenant_id=m.tenant_id AND d.id=m.nova_database_id) AS DatabaseName,
            CASE WHEN EXISTS(SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id" AND ur."TenantId"=m.tenant_id AND r."Name"='aurora:Admin') THEN 'Administrator'
            WHEN EXISTS(SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id" AND ur."TenantId"=m.tenant_id AND r."Name"='aurora:PowerUser') THEN 'Power User' ELSE 'User' END AS Level,m.nova_user_id AS NovaUserId,m.nova_api_login AS NovaApiLogin
        FROM "AspNetUsers" u JOIN user_tenant m ON m.user_id=u."Id" WHERE m.tenant_id=@TenantId
        """;
    public async Task<AdminUserDto[]> List(CancellationToken ct)
    {
        await using var connection=await factory.OpenConnectionAsync(ct);
        var rows=await connection.QueryAsync<Row>(new CommandDefinition(UserSql+" ORDER BY u.\"UserName\"",new{tenant.TenantId},cancellationToken:ct));
        return rows.Select(row=>new AdminUserDto(row.Id,row.UserName,row.Email,row.Level,row.Revision,row.Owner==tenant.TenantId,row.DatabaseId,row.DatabaseName??"",row.NovaUserId,row.NovaApiLogin)).ToArray();
    }
    static void Check(IdentityResult result){if(!result.Succeeded)throw new FormatException(string.Join(" ",result.Errors.Select(e=>e.Description)));}
    public async Task<Guid> Save(Guid? id,AdminUserEdit input,CancellationToken ct)
    {
        var role=Role(input.Level);
        if(input.NovaApiLogin && input.Level!="User")throw new FormatException("Nova API sign-in is limited to the User access level.");
        var novaUserId=string.IsNullOrWhiteSpace(input.NovaUserId)?null:input.NovaUserId.Trim().ToUpperInvariant();
        if(novaUserId?.Length>128)throw new FormatException("Nova user ID must be 128 characters or fewer.");
        if(string.IsNullOrWhiteSpace(input.UserName) || input.UserName.Length>256 || string.IsNullOrWhiteSpace(input.Email) || input.Email.Length>256)
            throw new FormatException("Enter a username and email address.");
        if(id is null && string.IsNullOrEmpty(input.LoginPassword))throw new FormatException("Enter a login password for the new user.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var connection=db.Database.GetDbConnection();var transaction=tx.GetDbTransaction();
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"admin-users:{tenant.TenantId}"},transaction,cancellationToken:ct));
        Row? row=null;
        if(id is not null)
        {
            row=await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(UserSql+" AND u.\"Id\"=@id FOR UPDATE OF u,m",new{tenant.TenantId,id},transaction,cancellationToken:ct))??throw new KeyNotFoundException("User not found.");
            if(row.Owner!=tenant.TenantId)throw new FormatException("This shared login is managed by another company.");
            if(row.Revision!=input.Revision)throw new FormatException("This user changed. Reload before saving.");
            if(id==tenant.UserId && input.Level!=row.Level)throw new FormatException("Another administrator must change your access level.");
            if(row.Level=="Administrator" && input.Level!="Administrator")await KeepAdministrator(connection,transaction,ct);
        }
        if(input.DatabaseId is not {} databaseId)throw new FormatException("Select a database. Add one under Admin > Databases first.");
        var selected=await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT id FROM nova_database WHERE tenant_id=@TenantId AND id=@databaseId FOR SHARE",new{tenant.TenantId,databaseId},transaction,cancellationToken:ct));
        if(selected is null)throw new FormatException("The selected database is unavailable. Choose a database from this company.");
        var user=id is null?new ApplicationUser{Id=Guid.NewGuid()}:await users.FindByIdAsync(id.Value.ToString())??throw new KeyNotFoundException("User not found.");
        user.UserName=input.UserName.Trim();user.Email=input.Email.Trim();
        if(id is null)
        {
            Check(await users.CreateAsync(user,input.LoginPassword!));
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE "AspNetUsers" SET "ManagementTenantId"=@TenantId WHERE "Id"=@Id;
                INSERT INTO user_tenant(tenant_id,user_id,is_default) VALUES(@TenantId,@Id,true);
                """,new{tenant.TenantId,user.Id},transaction,cancellationToken:ct));
        }
        else
        {
            Check(await users.UpdateAsync(user));
            if(!string.IsNullOrEmpty(input.LoginPassword))Check(await users.ResetPasswordAsync(user,await users.GeneratePasswordResetTokenAsync(user),input.LoginPassword));
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE user_tenant SET nova_database_id=@databaseId,nova_user_id=@novaUserId,nova_api_login=@NovaApiLogin WHERE tenant_id=@TenantId AND user_id=@Id;
            DELETE FROM "AspNetUserRoles" ur USING "AspNetRoles" r WHERE ur."RoleId"=r."Id" AND ur."TenantId"=@TenantId AND ur."UserId"=@Id AND r."Name" LIKE 'aurora:%';
            INSERT INTO "AspNetUserRoles"("TenantId","UserId","RoleId") SELECT @TenantId,@Id,"Id" FROM "AspNetRoles" WHERE "Name"=@role;
            """,new{tenant.TenantId,user.Id,databaseId,role,novaUserId,input.NovaApiLogin},transaction,cancellationToken:ct));
        await tx.CommitAsync(ct);return user.Id;
    }
    public async Task<InvitationEmailResult> SendInvitation(Guid id,IInvitationEmail email,CancellationToken ct)
    {
        await using var connection=await factory.OpenConnectionAsync(ct);
        var row=await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(UserSql+" AND u.\"Id\"=@id",new{tenant.TenantId,id},cancellationToken:ct))??throw new KeyNotFoundException("User not found.");
        if(row.Owner!=tenant.TenantId)throw new FormatException("This shared login is managed by another company.");
        return await email.Send(row.Id,row.UserName,row.Email,ct);
    }
    async Task KeepAdministrator(System.Data.Common.DbConnection connection,System.Data.Common.DbTransaction transaction,CancellationToken ct)
    {
        var count=await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."TenantId"=@TenantId AND r."Name"='aurora:Admin'
            """,new{tenant.TenantId},transaction,cancellationToken:ct));
        if(count<=1)throw new FormatException("Keep at least one Administrator.");
    }
    public async Task Delete(Guid id,string revision,CancellationToken ct)
    {
        if(id==tenant.UserId)throw new FormatException("You cannot delete your own login.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var connection=db.Database.GetDbConnection();var transaction=tx.GetDbTransaction();
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"admin-users:{tenant.TenantId}"},transaction,cancellationToken:ct));
        var row=await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(UserSql+" AND u.\"Id\"=@id FOR UPDATE OF u,m",new{tenant.TenantId,id},transaction,cancellationToken:ct))??throw new KeyNotFoundException("User not found.");
        if(row.Owner!=tenant.TenantId)throw new FormatException("This shared login is managed by another company.");
        if(row.Revision!=revision)throw new FormatException("This user changed. Reload before deleting.");
        if(row.Level=="Administrator")await KeepAdministrator(connection,transaction,ct);
        Check(await users.DeleteAsync(await users.FindByIdAsync(id.ToString())??throw new KeyNotFoundException("User not found.")));
        await tx.CommitAsync(ct);
    }
    private sealed record Row(Guid Id,string UserName,string Email,string Revision,Guid? Owner,Guid? DatabaseId,string? DatabaseName,string Level,string? NovaUserId,bool NovaApiLogin);
}

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin/users").RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
        group.MapGet("",(AdminUsers service,CancellationToken ct)=>Guard(async()=>Results.Ok(await service.List(ct))));
        group.MapPost("",(AdminUserEdit input,AdminUsers service,IInvitationEmail email,CancellationToken ct)=>Guard(async()=>
        {
            var id=await service.Save(null,input,ct);
            // Send only after the transaction commits. Email failure must not undo a created user.
            var invitation=input.SendInvitation?await email.Send(id,input.UserName.Trim(),input.Email.Trim(),ct):new InvitationEmailResult(false,null);
            return Results.Ok(new AdminUserCreated(id,invitation));
        }));
        group.MapPost("/{id:guid}/invitation",(Guid id,AdminUsers service,IInvitationEmail email,CancellationToken ct)=>Guard(async()=>Results.Ok(await service.SendInvitation(id,email,ct))));
        group.MapPut("/{id:guid}",(Guid id,AdminUserEdit input,AdminUsers service,CancellationToken ct)=>Guard(async()=>Results.Ok(await service.Save(id,input,ct))));
        group.MapDelete("/{id:guid}",(Guid id,string revision,AdminUsers service,CancellationToken ct)=>Guard(async()=>{await service.Delete(id,revision,ct);return Results.Ok();}));
    }
    internal static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try{return await action();}
        catch(FormatException ex){return Results.BadRequest(new ApiErrorDto(ex.Message));}
        catch(KeyNotFoundException ex){return Results.NotFound(new ApiErrorDto(ex.Message));}
        catch(FbException ex) when(ex.Errors.Cast<FbError>().Any(e=>e.Number==335544472)){return Results.BadRequest(new ApiErrorDto("Firebird rejected the Nova username or password. Enter the credentials for this Firebird installation; leaving the password blank reuses the saved password."));}
        catch(FbException){return Results.BadRequest(new ApiErrorDto("Could not connect to Nova. Check the server, database path, and credentials."));}
        catch(TimeoutException){return Results.Json(new ApiErrorDto("The database connection timed out. Check the server address, port, and network connection. For Firebird installed on this PC, use 127.0.0.1."),statusCode:503);}
        catch(NotSupportedException ex) when(ex.Message.StartsWith("Not supported plugin '",StringComparison.Ordinal)){return Results.BadRequest(new ApiErrorDto(Aurora.Modules.Routing.NovaConnectionErrors.UnsupportedPlugin(ex)));}
        catch(Npgsql.PostgresException ex) when(ex.SqlState=="23514"){return Results.BadRequest(new ApiErrorDto("Nova sign-in requires the user’s default company and an assigned database."));}
        catch(Npgsql.PostgresException ex) when(ex.SqlState=="23503"){return Results.BadRequest(new ApiErrorDto("This database is assigned to users. Reassign them before deleting it."));}
        catch(Npgsql.PostgresException ex) when(ex.SqlState=="23505"){return Results.BadRequest(new ApiErrorDto("That name, email, or Nova user mapping is already in use."));}
        catch(System.Security.Cryptography.CryptographicException){return Results.BadRequest(new ApiErrorDto("Re-enter the Nova password to replace the saved credential."));}
    }
}
