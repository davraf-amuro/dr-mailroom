namespace dr_mailroom_api.Validators;

/// <summary>Esito di una validazione: errori per campo, pronti per TypedResults.ValidationProblem.</summary>
public record ValidationResult(bool IsValid, IDictionary<string, string[]> Errors)
{
    public static ValidationResult Success() => new(true, new Dictionary<string, string[]>());

    public static ValidationResult Failure(IDictionary<string, string[]> errors) => new(false, errors);

    /// <summary>Successo se non ci sono errori, altrimenti fallimento.</summary>
    public static ValidationResult From(IDictionary<string, string[]> errors) =>
        errors.Count == 0 ? Success() : Failure(errors);
}
