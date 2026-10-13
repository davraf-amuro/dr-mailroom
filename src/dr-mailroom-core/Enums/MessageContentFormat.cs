namespace dr_mailroom_core.Enums;

/// <summary>Formato di download del contenuto archiviato di una mail.</summary>
public enum MessageContentFormat
{
    /// <summary>Messaggio originale RFC 822, come salvato da ContentArchive.</summary>
    Eml,

    /// <summary>Testo leggibile ricavato dal .eml al momento della richiesta.</summary>
    Txt,
}
