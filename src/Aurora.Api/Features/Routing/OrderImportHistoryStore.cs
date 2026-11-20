using Aurora.Contracts;
using Aurora.Api.Features.Auth;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Api.Features.Routing;

public sealed class OrderImportHistoryStore(NpgsqlConnectionFactory factory, ITenantContext tenant, ILogger<OrderImportHistoryStore> logger)
{
    // Older clients recorded duplicate PROs before the operator marked those rows skipped.
    private const string VisibleFailure = """
        (COALESCE(h.skipped_count,0)=0 OR NOT (
            f.reason IN ('That PRO number already exists.', 'This PRO already exists. No order was created.')
            OR f.reason ~ '^PRO [0-9]+ already exists in (Aurora or Nova|Aurora [(]including deleted orders[)])[.] No order was created[.]$'))
        """;

    public async Task<IEnumerable<OrderImportHistory>> List(CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QueryAsync<OrderImportHistory>(new CommandDefinition($"""
            SELECT id, file_name AS FileName, created_at AS CreatedAt,
              CASE WHEN status='Converting' AND created_at < now()-interval '7 minutes' THEN 'Interrupted' ELSE status END AS Status,
              extracted_count AS ExtractedCount, cardinality(created_pros) AS CreatedCount, created_pros AS CreatedPros,
              skipped_count AS SkippedCount, failed_count AS FailedCount, failure_reason AS FailureReason,
              EXISTS(SELECT FROM order_import_failure f WHERE f.import_id=h.id AND {VisibleFailure}) AS HasOrderFailures
            FROM order_import_history h WHERE tenant_id=@TenantId ORDER BY created_at DESC,id DESC LIMIT 20
            """,new{tenant.TenantId},cancellationToken:ct));
    }

    public async Task<Guid> Start(string fileName,CancellationToken ct)
    {
        var name=Path.GetFileName(fileName.Replace('\\','/'));
        name=new string(name.Where(c=>!char.IsControl(c)).Take(255).ToArray());
        if(string.IsNullOrWhiteSpace(name))name="Document";
        var id=Guid.NewGuid();
        await using var db=await factory.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO order_import_history(id,tenant_id,user_id,file_name,status) VALUES(@id,@TenantId,@UserId,@name,'Converting')",new{id,tenant.TenantId,tenant.UserId,name},cancellationToken:ct));
        return id;
    }

    public async Task Converted(Guid id,int count,CancellationToken ct,ImportFailure[]? failures=null)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("UPDATE order_import_history SET status='Converted',extracted_count=@count,skipped_count=0,failed_count=@failed WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId",new{id,count,failed=failures?.Length??0,tenant.TenantId,tenant.UserId},tx,cancellationToken:ct));
        if (failures is {Length:>0})
            await db.ExecuteAsync(new CommandDefinition(InsertFailure,failures.Select(f=>FailureParameters(id,f)),tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }

    public async Task<bool> SaveReviewCounts(Guid id, ImportReviewCounts counts, CancellationToken ct)
    {
        var failures=counts.ClientFailures ?? [];
        if (counts.Skipped < 0 || counts.Failed < 0 || failures.Length>5000 ||
            failures.Any(f=>f is null || f.RowId==Guid.Empty || string.IsNullOrWhiteSpace(f.Reason) ||
                f.Reason.Length>1000 || string.IsNullOrWhiteSpace(f.OrderLabel) || f.OrderLabel.Length>1000)) return false;
        await using var db = await factory.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        var updated=await db.ExecuteAsync(new CommandDefinition("""
            UPDATE order_import_history SET skipped_count=@Skipped, failed_count=@Failed
            WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId
              AND status IN ('Converted','Orders created')
              AND CAST(@Skipped AS bigint) + @Failed <= extracted_count - cardinality(created_pros)
            """, new { id, tenant.TenantId, tenant.UserId, counts.Skipped, counts.Failed }, tx, cancellationToken: ct));
        if (updated!=1) return false;
        if (failures.Length>0)
            await db.ExecuteAsync(new CommandDefinition(InsertFailure,failures.Select(f=>FailureParameters(id,f)),tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<IEnumerable<ImportFailure>> Failures(Guid id, CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        return await db.QueryAsync<ImportFailure>(new CommandDefinition($"""
            SELECT row_id AS RowId, order_label AS OrderLabel, reason FROM order_import_failure f
            JOIN order_import_history h ON h.id=f.import_id
            WHERE h.id=@id AND h.tenant_id=@TenantId AND {VisibleFailure} ORDER BY occurred_at,row_id
            """,new{id,tenant.TenantId},cancellationToken:ct));
    }

    private const string InsertFailure = """
        INSERT INTO order_import_failure(import_id,row_id,order_label,reason)
        SELECT id,@RowId,@OrderLabel,@Reason FROM order_import_history
        WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND status IN ('Converted','Orders created')
        ON CONFLICT DO NOTHING
        """;
    private object FailureParameters(Guid id, ImportFailure failure) => new
    {
        id, tenant.TenantId, tenant.UserId, failure.RowId,
        OrderLabel=AppLog.SafeDetail(failure.OrderLabel), Reason=AppLog.SafeDetail(failure.Reason)
    };

    public async Task<IResult> TrackCreation(OrderDocumentDraft draft, Func<Task<IResult>> create)
    {
        string? reason=null;
        try
        {
            var result=await create();
            if (result is IValueHttpResult {Value:ApiErrorDto error}) reason=error.Error;
            return result;
        }
        catch (Exception ex)
        {
            reason=ex is OperationCanceledException ? "Order request interrupted. Check Orders before retrying."
                : "Unexpected order creation error. Check Orders before retrying.";
            logger.LogError(ex,"Import {ImportId} row {RowId} could not complete",draft.ImportId,draft.ImportRowId);
            throw;
        }
        finally
        {
            if (draft.ImportId is {} id && reason is not null)
            {
                var failure=new ImportFailure(draft.ImportRowId==Guid.Empty ? Guid.NewGuid() : draft.ImportRowId,draft.FailureLabel(),reason);
                await TryUpdate(id,InsertFailure,FailureParameters(id,failure));
            }
        }
    }

    public async Task Validate(Guid? id,CancellationToken ct)
    {
        if(id is null)return; // Older/manual clients have no conversion batch.
        await using var db=await factory.OpenConnectionAsync(ct);
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM order_import_history WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND status IN ('Converted','Orders created'))",new{id,tenant.TenantId,tenant.UserId},cancellationToken:ct)))
            throw new FormatException("This import is unavailable. Convert the document again.");
    }

    public async Task RequireCreatedOrder(Guid id,string pro,CancellationToken ct)
    {
        await using var db=await factory.OpenConnectionAsync(ct);
        if(!await db.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM order_import_history WHERE id=@id AND tenant_id=@TenantId AND @pro=ANY(created_pros))",new{id,pro,tenant.TenantId},cancellationToken:ct)))
            throw new KeyNotFoundException("This order is not in your company's import history.");
    }

    // History is secondary to the successful business write. Never report a saved order
    // as failed (and invite a duplicate retry) because the history update failed.
    public async Task RecordCreated(Guid? id,string pro)
    {
        if(id is null)return;
        await TryUpdate(id.Value,"UPDATE order_import_history SET status='Orders created',created_pros=array_append(created_pros,@pro) WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND NOT (@pro=ANY(created_pros))",new{id,tenant.TenantId,tenant.UserId,pro});
    }
    public Task Failed(Guid id,string reason)=>TryUpdate(id,"UPDATE order_import_history SET status='Failed',failure_reason=@reason WHERE id=@id AND tenant_id=@TenantId AND user_id=@UserId AND status='Converting'",new{id,tenant.TenantId,tenant.UserId,reason=AppLog.SafeDetail(reason)});
    async Task TryUpdate(Guid id,string sql,object parameters)
    {
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var db=await factory.OpenConnectionAsync(timeout.Token);
            await db.ExecuteAsync(new CommandDefinition(sql,parameters,cancellationToken:timeout.Token));
        }
        catch(Exception ex){logger.LogError(ex,"Could not update import history {ImportId}",id);}
    }
}
