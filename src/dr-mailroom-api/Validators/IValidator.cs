namespace dr_mailroom_api.Validators;

/// <summary>Validatore esplicito di un input esterno (body o filtro di query).</summary>
public interface IValidator<T>
{
    /// <summary>Valida l'input; gli errori sono indicizzati per nome del campo.</summary>
    ValidationResult Validate(T input);
}
