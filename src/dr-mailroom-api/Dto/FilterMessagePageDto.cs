namespace dr_mailroom_api.Dto;

/// <summary>
/// Pagina delle mail di un filtro. MissingArchiveCount: mail corrispondenti senza contenuto salvato (su tutte le pagine);
/// HasContentArchive: il filtro prevede il processo ContentArchive, quindi la riapplica può archiviarle.
/// </summary>
public record FilterMessagePageDto(
    IReadOnlyList<FilterMessageDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int MissingArchiveCount,
    bool HasContentArchive);
