namespace dr_mailroom_core.Gmail;

/// <summary>Da dove viene la data di ricezione di un messaggio.</summary>
public enum ReceivedDateSource
{
    /// <summary>internalDate di Gmail (T5): il caso normale.</summary>
    InternalDate,

    /// <summary>internalDate assente o non valido: header Date del messaggio.</summary>
    DateHeader,

    /// <summary>Né internalDate né header Date validi: ora in cui il messaggio è stato letto.</summary>
    IngestionTime,
}
