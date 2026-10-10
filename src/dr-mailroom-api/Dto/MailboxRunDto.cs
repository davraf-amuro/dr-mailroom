namespace dr_mailroom_api.Dto;

/// <summary>Giro in corso su una casella, con le mail lette finora (aggiornate a fine pagina).</summary>
public record MailboxRunDto(long Id, DateTime StartedAtUtc, int MessagesRead);
