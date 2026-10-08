using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Filtro giri: solo numeri, enum e date, già garantiti dal model binding. Regole ereditate dalle colonne, tutti i campi facoltativi.</summary>
public class RunFilterValidator : IValidator<RunFilter>
{
    public ValidationResult Validate(RunFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        // TODO: validazione — nessuna regola inferibile dai metadati oltre al tipo
        return ValidationResult.From(errors);
    }
}
