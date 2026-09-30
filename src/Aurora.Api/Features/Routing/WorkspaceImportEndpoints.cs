using Aurora.Contracts;
using Aurora.Modules.Routing;
using Npgsql;

namespace Aurora.Api.Features.Routing;

public static class WorkspaceImportEndpoints
{
    public static void MapWorkspaceImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/tms/import").RequireAuthorization(p=>p.RequireRole("aurora:Admin"));
        group.MapPost("/validate",(HttpRequest request,WorkspaceImportStore store,CancellationToken ct)=>Handle(request,store,false,ct));
        group.MapPost("/replace",(HttpRequest request,WorkspaceImportStore store,CancellationToken ct)=>Handle(request,store,true,ct));
    }
    private static async Task<IResult> Handle(HttpRequest request,WorkspaceImportStore store,bool replace,CancellationToken ct)
    {
        if(replace && request.Headers["X-Aurora-Replace"]!="company-data") return Results.BadRequest(new ApiErrorDto("Confirm replacement of this company's data."));
        try
        {
            // Bound the actual stream as well as Content-Length (chunked uploads have no length).
            if(request.ContentLength>WorkspaceImport.MaxBytes) return Results.BadRequest(new ApiErrorDto("Choose a JSON file up to 25 MB."));
            using var buffer=new MemoryStream();
            var chunk=new byte[81920];
            int read;
            while((read=await request.Body.ReadAsync(chunk,ct))>0)
            {
                if(buffer.Length+read>WorkspaceImport.MaxBytes) return Results.BadRequest(new ApiErrorDto("Choose a JSON file up to 25 MB."));
                await buffer.WriteAsync(chunk.AsMemory(0,read),ct);
            }
            var data=WorkspaceImport.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF'));
            return Results.Ok(await store.Execute(data,replace,ct));
        }
        catch(FormatException ex) { return Results.BadRequest(new ApiErrorDto(ex.Message)); }
        catch(PostgresException ex) when(ex.SqlState is "23505" or "23503" or "23514" or "22001" or "22P05" or "22021") { return Results.BadRequest(new ApiErrorDto("The file contains invalid or conflicting records. No data was replaced.")); }
    }
}
