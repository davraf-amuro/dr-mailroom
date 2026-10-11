using dr_mailroom_core.Infrastructure.Mailroom;

namespace dr_mailroom_api.Dto;

/// <summary>Mittente (per indirizzo) con numero di mail, quante sono posta inviata, la più vecchia e la più recente (data e oggetto).</summary>
public record SenderOverviewDto(
    string SenderAddress,
    string? SenderName,
    int MailCount,
    int SentCount,
    DateTime FirstReceivedAtUtc,
    string? FirstSubject,
    DateTime LastReceivedAtUtc,
    string? LastSubject)
{
    public static SenderOverviewDto From(SenderOverview row) => new(
        row.SenderAddress,
        row.SenderName,
        row.MailCount,
        row.SentCount,
        row.FirstReceivedAtUtc,
        row.FirstSubject,
        row.LastReceivedAtUtc,
        row.LastSubject);
}

/// <summary>Pagina della panoramica mittenti: TotalCount = mittenti che rispettano il filtro.</summary>
public record SenderOverviewPageDto(IReadOnlyList<SenderOverviewDto> Items, int Page, int PageSize, int TotalCount);

/// <summary>Mail ricevute in un anno.</summary>
public record YearStatDto(int Year, int MailCount);
