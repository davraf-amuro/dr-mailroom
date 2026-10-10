namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Riga della panoramica: un mittente (per indirizzo) con la mail più vecchia e la più recente.</summary>
public record SenderOverview(
    string SenderAddress,
    string? SenderName,
    int MailCount,
    DateTime FirstReceivedAtUtc,
    string? FirstSubject,
    DateTime LastReceivedAtUtc,
    string? LastSubject);

/// <summary>Una pagina della panoramica con il totale dei mittenti che rispettano il filtro.</summary>
public record SenderOverviewPage(IReadOnlyList<SenderOverview> Items, int TotalCount);

/// <summary>Mail ricevute in un anno.</summary>
public record YearStat(int Year, int MailCount);
