using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
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
            foreach(var batch in names.Chunk(100))accounts.AddRange(await db.QueryAsync<NovaImportAddress>(new CommandDefinition("""
                SELECT AC_CUST AS Code, AC_NAME AS Name, AC_ADDR1 AS Address, AC_ADDR2 AS Address2,
                    AC_ADDR3 AS Address3, AC_CITY AS City, AC_STATE AS State, AC_ZIP AS Zip,
                    AC_COUNTRY_CD AS Country, AC_LATITUDE AS Latitude, AC_LONGITUDE AS Longitude
                FROM ACCOUNT WHERE CAST(UPPER(TRIM(AC_NAME)) AS VARCHAR(256)) IN @names
                   OR CAST(UPPER(TRIM(AC_CUST)) AS VARCHAR(256)) IN @names
                """,new{names=batch},commandTimeout:15,cancellationToken:ct)));
            foreach(var draft in rows)result[draft]=NovaImportAddress.Apply(draft,accounts.DistinctBy(a=>a.Code));
        }
        catch(Exception ex) when(ex is FbException or TimeoutException)
        {foreach(var draft in rows)draft.AddressLookupMessages=["Nova address lookup was unavailable; kept the imported addresses."];}
        return result;
    }
}
