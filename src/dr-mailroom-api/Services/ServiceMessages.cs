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
    public const string RunNotRunning = "Il giro non è in corso";
    public const string NoRunningRuns = "Nessun giro in corso";
    public const string MailboxPaused = "La casella è in pausa";
    public const string MailboxNotPaused = "La casella non è in pausa";
    public const string MailboxTokenRevoked = "La casella ha il token revocato: va ricollegata";
    public const string TrashNotAuthorized = "Il cestino della casella non è autorizzato: serve il consenso Google";
    public const string NothingToTrash = "Nessuna mail del mittente da spostare nel cestino";
}
