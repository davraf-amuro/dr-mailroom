namespace dr_mailroom_api.Dto;

/// <summary>Body dell'anteprima: condizioni del filtro da provare sulle mail già indicizzate.</summary>
public record FilterPreviewRequest(string? SenderAddress, int? MailboxId, string? SubjectMatchType, string? SubjectValue);
