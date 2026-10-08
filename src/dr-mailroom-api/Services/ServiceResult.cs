namespace dr_mailroom_api.Services;

/// <summary>Risultato di un servizio: valore, errori di validazione (Invalid) o messaggio (NotFound/Conflict).</summary>
public sealed record ServiceResult<T>(ServiceOutcome Outcome, T? Value, IDictionary<string, string[]>? Errors, string? Message)
{
    public static ServiceResult<T> Ok(T value) => new(ServiceOutcome.Ok, value, null, null);

    public static ServiceResult<T> NotFound(string message) => new(ServiceOutcome.NotFound, default, null, message);

    public static ServiceResult<T> Conflict(string message) => new(ServiceOutcome.Conflict, default, null, message);

    public static ServiceResult<T> Invalid(string field, string message) =>
        new(ServiceOutcome.Invalid, default, new Dictionary<string, string[]> { [field] = [message] }, null);
}
