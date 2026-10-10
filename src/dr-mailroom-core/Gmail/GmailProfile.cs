namespace dr_mailroom_core.Gmail;

/// <summary>Profilo della casella: historyId corrente e totale dei messaggi (Spam e Cestino inclusi), null se Gmail non lo restituisce.</summary>
public sealed record GmailProfile(ulong HistoryId, int? MessagesTotal);
