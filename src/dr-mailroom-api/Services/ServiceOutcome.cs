namespace dr_mailroom_api.Services;

/// <summary>Esito di un'operazione di servizio, tradotto dagli handler nello status HTTP.</summary>
public enum ServiceOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict,
}
