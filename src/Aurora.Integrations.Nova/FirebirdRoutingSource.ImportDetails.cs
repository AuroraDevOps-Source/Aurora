using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Globalization;

namespace Aurora.Integrations.Nova;

public sealed partial class FirebirdRoutingSource
{
    static async Task WriteImportLines(FbConnection db, FbTransaction tx, long id, long stop, OrderDocumentDraft draft, CancellationToken ct)
    {
        var lines = draft.FreightLines ?? [];
        if (lines.Length == 0 && (draft.Order.Pieces != 0 || draft.Order.Weight != 0 || draft.Order.Cube != 0))
            lines = [new DocumentFreightLine()];
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var single = lines.Length == 1;
            var description = (line.Description ?? "").Trim();
            // Nova's freight grid has a short description; keep the full document text in notes.
            var args = new { id, stop, seq = index + 1, description = description[..Math.Min(description.Length,50)],
                pieces = single ? draft.Order.Pieces : line.Pieces, weight = single ? draft.Order.Weight : line.WeightLb,
                cube = (line.Volume ?? (single && draft.Order.Cube>0 ? draft.Order.Cube : (decimal?)null))?.ToString("0.####",CultureInfo.InvariantCulture) };
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_LINE_ITEMS (OPL_ORDER_ID,OPL_STOP_ID,OPL_SEQUENCE,OPL_DESC,OPL_PIECES,OPL_WEIGHT,OPL_CUBE)
                VALUES (@id,@stop,@seq,@description,@pieces,@weight,@cube)
                """,args,tx,cancellationToken:ct));
        }
    }

    static async Task WriteImportReferences(FbConnection db,FbTransaction tx,long id,long stop,OrderDocumentDraft draft,OrderDocumentDraft? previous,CancellationToken ct)
    {
        foreach(var (code,oldValue,newValue) in new[]{
            ("HB",previous?.HouseAirWaybill,draft.HouseAirWaybill),
            ("MB",previous?.MasterAirWaybill,draft.MasterAirWaybill),
            ("BM",previous?.BillOfLading,draft.BillOfLading)})
        {
            var value = newValue.Trim();
            if (oldValue?.Trim() == value) continue;
            var args = new { id, stop, code, oldValue, value };
            if (!string.IsNullOrWhiteSpace(oldValue))
                await db.ExecuteAsync(new CommandDefinition("DELETE FROM OPS_NUMBERS WHERE OPN_ORDER_ID=@id AND OPN_STOP_ID=@stop AND OPN_TYPE='' AND OPN_QUALIFIER=@code AND OPN_NUMBER=@oldValue",args,tx,cancellationToken:ct));
            if (value.Length == 0) continue;
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_NUMBERS (OPN_ORDER_ID,OPN_STOP_ID,OPN_SEQUENCE,OPN_TYPE,OPN_QUALIFIER,OPN_NUMBER)
                SELECT @id,@stop,(SELECT COALESCE(MAX(OPN_SEQUENCE),0)+1 FROM OPS_NUMBERS WHERE OPN_ORDER_ID=@id),'',@code,@value
                FROM RDB$DATABASE WHERE NOT EXISTS (SELECT 1 FROM OPS_NUMBERS WHERE OPN_ORDER_ID=@id AND OPN_STOP_ID=@stop AND OPN_QUALIFIER=@code AND OPN_NUMBER=@value)
                """,args,tx,cancellationToken:ct));
        }
    }
}
