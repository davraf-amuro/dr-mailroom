namespace dr_mailroom_core.Gmail;

/// <summary>Valori fissi dell'API Gmail usati dal client.</summary>
public static class GmailConstants
{
    /// <summary>Utente "me": l'account a cui appartiene il refresh token.</summary>
    public const string Me = "me";

    /// <summary>Unico scope richiesto: sola lettura, lo stato letto/non letto resta intatto (T1).</summary>
    public const string ReadOnlyScope = "https://www.googleapis.com/auth/gmail.readonly";

    public const string FromHeader = "From";
    public const string SubjectHeader = "Subject";
    public const string DateHeader = "Date";
    public const string SpamLabel = "SPAM";
    public const string TrashLabel = "TRASH";

    /// <summary>Label della posta inviata dall'account (anche da alias send-as).</summary>
    public const string SentLabel = "SENT";
    public const string MultipartMixed = "multipart/mixed";

    /// <summary>Massimo consentito da users.messages.list per pagina.</summary>
    public const int MaxPageSize = 500;

    /// <summary>Codice OAuth restituito per un refresh token revocato o scaduto.</summary>
    public const string InvalidGrant = "invalid_grant";

    /// <summary>Reason del 403 per superamento della quota al minuto (progetto o utente): errore temporaneo.</summary>
    public const string RateLimitExceededReason = "rateLimitExceeded";
    public const string UserRateLimitExceededReason = "userRateLimitExceeded";

    /// <summary>Costo in unità di quota dei metodi usati (tabella "Per-method quota usage" di Google).</summary>
    public const int GetProfileCost = 1;
    public const int MessagesListCost = 5;
    public const int HistoryListCost = 2;
    public const int MessagesGetCost = 5;
    public const int AttachmentsGetCost = 5;
}
