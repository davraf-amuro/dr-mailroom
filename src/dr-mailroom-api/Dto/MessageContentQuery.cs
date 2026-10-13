namespace dr_mailroom_api.Dto;

/// <summary>Query di GET /messages/{id}/content: format eml | txt (maiuscole indifferenti), obbligatorio.</summary>
public class MessageContentQuery
{
    public string? Format { get; set; }
}
