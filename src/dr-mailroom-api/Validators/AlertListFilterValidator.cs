using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Filtro allerte: SenderAddress ≤ 320. Regole ereditate dalle colonne, tutti i campi facoltativi.</summary>
public class AlertListFilterValidator : IValidator<AlertListFilter>
{
    public ValidationResult Validate(AlertListFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress, ColumnLengths.EmailAddress);
        return ValidationResult.From(errors);
    }
}
