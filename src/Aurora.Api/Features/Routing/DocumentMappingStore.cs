using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;

namespace Aurora.Api.Features.Routing;

public sealed class DocumentMappingStore(NpgsqlConnectionFactory connections, ITenantContext tenant)
{
    static string Key(string value)=>value.Trim().ToUpperInvariant();
    public async Task<IResult> Save(DocumentMappingRule rule,CancellationToken ct)
    {
        if(rule.Field is not ("PRO" or "BillTo") || new[]{rule.Sender,rule.Form,rule.Label}.Any(v=>string.IsNullOrWhiteSpace(v)||v.Length>160))
            return Results.BadRequest(new ApiErrorDto("A sender, form and printed field label are required."));
        await using var db=await connections.OpenConnectionAsync(ct);
        var saved=await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO document_mapping_rules(tenant_id,sender,form,field,label) VALUES(@TenantId,@sender,@form,@Field,@label)
            ON CONFLICT(tenant_id,sender,form,field) DO UPDATE SET label=excluded.label WHERE document_mapping_rules.label=excluded.label
            """,new{tenant.TenantId,sender=Key(rule.Sender),form=Key(rule.Form),rule.Field,label=Key(rule.Label)},cancellationToken:ct));
        return saved==1 ? Results.NoContent() : Results.Conflict(new ApiErrorDto("A different mapping is already saved for this sender and form. Apply this answer to this import only."));
    }

    public async Task Apply(List<OrderDocumentDraft> drafts,bool requirePrintedTag,CancellationToken ct)
    {
        var candidates=drafts.Where(d=>d.MappingQuestions.Length>0 && !string.IsNullOrWhiteSpace(d.MappingSender) && !string.IsNullOrWhiteSpace(d.MappingForm)).ToList();
        if(candidates.Count==0)return;
        await using var db=await connections.OpenConnectionAsync(ct);
        var rules=(await db.QueryAsync<DocumentMappingRule>(new CommandDefinition(
            "SELECT sender,form,field,label FROM document_mapping_rules WHERE tenant_id=@TenantId AND sender=ANY(@senders)",
            new{tenant.TenantId,senders=candidates.Select(d=>Key(d.MappingSender)).Distinct().ToArray()},cancellationToken:ct))).ToDictionary(r=>(r.Sender,r.Form,r.Field));
        foreach(var d in candidates)
        foreach(var question in d.MappingQuestions)
        {
            if(question.Field=="PRO" && (requirePrintedTag || string.Equals(d.DocumentType,"Lykes",StringComparison.OrdinalIgnoreCase)))continue;
            if(!rules.TryGetValue((Key(d.MappingSender),Key(d.MappingForm),question.Field),out var rule))continue;
            var matches=question.Candidates.Where(c=>Key(c.Label)==rule.Label).ToArray();
            if(matches.Length!=1)continue;
            try
            {
                var candidate=matches[0];
                DocumentMappings.Answer(d,question,candidate);
                var page=candidate.Page is >0 ? $" (page {candidate.Page})" : "";
                d.MappingDecisions.Add($"Saved {rule.Field} mapping: {candidate.Label} = {candidate.Value}{page}");
            }
            catch(FormatException){continue;}
        }
    }
}
