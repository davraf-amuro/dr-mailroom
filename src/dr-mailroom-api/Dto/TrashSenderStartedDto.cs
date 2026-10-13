namespace dr_mailroom_api.Dto;

/// <summary>Giro di cestino avviato: Id del giro da seguire e mail che sposterà nel cestino.</summary>
public record TrashSenderStartedDto(long RunId, int MessagesToTrash);
