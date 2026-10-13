namespace dr_mailroom_api.Dto;

/// <summary>Riga della tabella delle mail di un filtro; IsArchived = contenuto salvato, scaricabile da GET /messages/{id}/content.</summary>
public record FilterMessageDto(long Id, int MailboxId, string? Subject, DateTime ReceivedAtUtc, bool HasAttachments, bool IsArchived);
