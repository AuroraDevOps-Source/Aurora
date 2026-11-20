using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Caching.Memory;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    private static readonly MemoryCache DailyBillToIndexes = new(new MemoryCacheOptions { SizeLimit = 16 });
    private const string AccountFields = """
        SELECT AC_CUST AS Code, AC_NAME AS Name, AC_ADDR1 AS Address, AC_ADDR2 AS Address2,
            AC_ADDR3 AS Address3, AC_CITY AS City, AC_STATE AS State, AC_ZIP AS Zip,
            AC_COUNTRY_CD AS Country, AC_LATITUDE AS Latitude, AC_LONGITUDE AS Longitude FROM ACCOUNT
        """;

    private async Task<Dictionary<string,NovaImportAddress?>> DailyBillToMatches(FbConnection db,
        OrderDocumentDraft[] rows, List<NovaImportAddress> accounts, CancellationToken ct)
    {
        var matches = new Dictionary<string,NovaImportAddress?>(StringComparer.OrdinalIgnoreCase);
        var payers = rows.Where(d => d.DailyReportBillToName is not null && string.IsNullOrWhiteSpace(d.BillToAccountId))
            .Select(d => d.BillToLookupName().Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (payers.Length == 0) return matches;

        var exact = accounts.DistinctBy(a => a.Code).ToLookup(a => a.Name?.Trim(), StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var payer in payers)
        {
            var candidates = exact[payer].ToArray();
            if (candidates.Length == 1) matches[payer] = candidates[0];
            else if (candidates.Length == 0) missing.Add(payer);
            else matches[payer] = null;
        }
        if (missing.Count == 0) return matches;

        var batch = rows[0].ImportId;
        var cacheKey = (tenant.TenantId, Options.SourceKey, batch);
        bool cacheable = batch.HasValue && rows.All(d => d.ImportId == batch);
        DailyBillToIndex? index = null;
        if (cacheable) DailyBillToIndexes.TryGetValue(cacheKey, out index);
        if (index is null)
        {
            var catalog = await db.QueryAsync<(string Code,string Name)>(new CommandDefinition(
                "SELECT AC_CUST, AC_NAME FROM ACCOUNT WHERE AC_NAME IS NOT NULL", commandTimeout:15, cancellationToken:ct));
            index = new DailyBillToIndex(catalog);
            if (cacheable) DailyBillToIndexes.Set(cacheKey, index, new MemoryCacheEntryOptions()
                .SetSize(1).SetAbsoluteExpiration(TimeSpan.FromMinutes(15)));
        }
        var selected = new List<(string Name,string Code)>();
        foreach (var name in missing)
            if (index.TryFind(name, out var code))
            {
                if (code is null) matches[name] = null;
                else selected.Add((name, code));
            }
        var byCode = new Dictionary<string,NovaImportAddress>();
        foreach (var codes in selected.Select(x => x.Code).Distinct().Chunk(100))
        {
            var found = await db.QueryAsync<NovaImportAddress>(new CommandDefinition(
                AccountFields + " WHERE AC_CUST IN @codes", new { codes }, commandTimeout:15, cancellationToken:ct));
            foreach (var account in found) byCode[account.Code] = account;
        }
        foreach (var (name, code) in selected)
            if (byCode.TryGetValue(code, out var account) && DailyBillToIndex.Key(account.Name) == DailyBillToIndex.Key(name))
                matches[name] = account;
        return matches;
    }
    public async Task<Dictionary<OrderDocumentDraft,Dictionary<string,NovaImportAddress>>> ResolveDocumentAddresses(IEnumerable<OrderDocumentDraft> drafts,CancellationToken ct)
    {
        var rows=drafts.ToArray();var result=new Dictionary<OrderDocumentDraft,Dictionary<string,NovaImportAddress>>();
        if(!Enabled)return result;
        var names=rows.SelectMany(d=>new[]{d.Order.Shipper,d.Order.Customer,d.Order.BillTo,d.BillToAccountId,d.ShipperAccountId,d.ConsigneeAccountId}).Where(n=>!string.IsNullOrWhiteSpace(n)).Select(n=>n.Trim().ToUpperInvariant()).Distinct().ToArray();
        // Firebird sizes IN parameters from the column expression. Names and codes share this
        // candidate list, so a long name must fit even when compared to the short code.
        names=names.Where(n=>n.Length<=256).ToArray();
        if(names.Length==0){foreach(var draft in rows)result[draft]=NovaImportAddress.Apply(draft,[]);return result;}
        try
        {
            await using var db=await Open(ct);var accounts=new List<NovaImportAddress>();
            foreach(var batch in names.Chunk(100))accounts.AddRange(await db.QueryAsync<NovaImportAddress>(new CommandDefinition(AccountFields + " WHERE " + """
                   CAST(UPPER(TRIM(AC_NAME)) AS VARCHAR(256)) IN @names
                   OR CAST(UPPER(TRIM(AC_CUST)) AS VARCHAR(256)) IN @names
                """,new{names=batch},commandTimeout:15,cancellationToken:ct)));
            var dailyMatches = await DailyBillToMatches(db, rows, accounts, ct);
            foreach(var draft in rows)result[draft]=NovaImportAddress.Apply(draft,accounts.DistinctBy(a=>a.Code),dailyMatches);
        }
        catch(Exception ex) when(ex is FbException or TimeoutException)
        {foreach(var draft in rows)draft.AddressLookupMessages=["Nova address lookup was unavailable; kept the imported addresses."];}
        return result;
    }
}
