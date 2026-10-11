namespace dr_mailroom_api.Dto;

/// <summary>Pagina della griglia delle mail di un mittente, con il totale per la paginazione.</summary>
public record SenderMessagePageDto(IReadOnlyList<SenderMessageDto> Items, int Page, int PageSize, int TotalCount);
