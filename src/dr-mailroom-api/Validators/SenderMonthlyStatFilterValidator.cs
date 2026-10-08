using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Filtro statistica mensile: SenderAddress ≤ 320. Regole ereditate dalle colonne, tutti i campi facoltativi.</summary>
public class SenderMonthlyStatFilterValidator : IValidator<SenderMonthlyStatFilter>
{
    public ValidationResult Validate(SenderMonthlyStatFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress, ColumnLengths.EmailAddress);

        // TODO: validazione — formato yyyyMM di YearMonthFrom/To non inferibile dai metadati
        return ValidationResult.From(errors);
    }
}
