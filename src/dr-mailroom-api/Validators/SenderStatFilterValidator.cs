using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Filtro statistica mittenti: SenderAddress ≤ 320. Regole ereditate dalle colonne, tutti i campi facoltativi.</summary>
public class SenderStatFilterValidator : IValidator<SenderStatFilter>
{
    public ValidationResult Validate(SenderStatFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress, ColumnLengths.EmailAddress);
        return ValidationResult.From(errors);
    }
}
