namespace Aurora.Contracts;

public sealed record PdfConversionStatus(Guid Id, string Progress, List<OrderDocumentDraft>? Orders = null, string? Error = null)
{
    public List<OrderDocumentDraft>? ReadyOrders { get; init; }
}
