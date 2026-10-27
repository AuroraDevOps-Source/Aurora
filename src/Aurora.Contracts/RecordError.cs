namespace Aurora.Contracts;

public sealed record RecordErrorLink(string Kind, string Id, string Text);

public sealed class RecordErrorException(string message, RecordErrorLink link) : FormatException(message)
{
    public RecordErrorLink Link { get; } = link;
}
