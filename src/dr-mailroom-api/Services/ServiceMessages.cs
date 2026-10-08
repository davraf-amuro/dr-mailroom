namespace dr_mailroom_api.Services;

/// <summary>Messaggi restituiti dai servizi nei ProblemDetails: fonte unica per handler e test.</summary>
public static class ServiceMessages
{
    public const string MailboxNotFound = "Casella inesistente";
    public const string MailboxDeleted = "La casella è eliminata: i suoi dati e filtri non sono modificabili";
    public const string MailboxNotActive = "La casella non è attiva";
    public const string MailboxBusy = "La casella ha già un giro in corso";
    public const string NoActiveMailboxes = "Nessuna casella attiva";
    public const string AllMailboxesBusy = "Tutte le caselle hanno già un giro in corso";
    public const string FilterNotFound = "Filtro inesistente";
    public const string AlertNotFound = "Allerta inesistente";
    public const string MessageNotFound = "Messaggio inesistente";
    public const string RunNotFound = "Giro inesistente";
    public const string ProcessNotInFilter = "Il processo non è previsto dal filtro";
}
