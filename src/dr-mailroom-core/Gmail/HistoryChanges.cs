namespace dr_mailroom_core.Gmail;

/// <summary>Messaggi aggiunti dopo un historyId e nuovo historyId da salvare come checkpoint.</summary>
public sealed record HistoryChanges(IReadOnlyList<string> AddedMessageIds, ulong LatestHistoryId);
