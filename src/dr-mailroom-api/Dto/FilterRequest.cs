namespace dr_mailroom_api.Dto;

/// <summary>Body di creazione/modifica di un filtro; tipi e processi come testo per messaggi di validazione chiari (D15).</summary>
public record FilterRequest(string? Name, string? SenderAddress, int? MailboxId, string? SubjectMatchType, string? SubjectValue, string? AttachmentSubfolder, bool? IsEnabled, List<string>? Processes);
