namespace dr_mailroom_core.Gmail;

/// <summary>La history Gmail non copre più l'historyId salvato (HTTP 404): serve una scansione completa (T3).</summary>
public sealed class HistoryExpiredException(ulong historyId, Exception innerException)
    : Exception($"History Gmail scaduta per historyId {historyId}", innerException)
{
    public ulong HistoryId { get; } = historyId;
}
