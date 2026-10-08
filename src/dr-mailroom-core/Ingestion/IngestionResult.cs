namespace dr_mailroom_core.Ingestion;

/// <summary>Esito dell'ingestione di una casella: messaggi nuovi e se il giro era di backfill (D12: niente filtri).</summary>
public sealed record IngestionResult(bool WasBackfill, IReadOnlyList<long> InsertedMessageIds, int MessagesRead);
