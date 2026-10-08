namespace dr_mailroom_api.Dto;

/// <summary>Body del giro manuale: casella; null = tutte le caselle attive (D17).</summary>
public record StartRunRequest(int? MailboxId);
