using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Filtro elenco filtri: Name ≤ 100, SenderAddress ≤ 320. Regole ereditate dalle colonne, tutti i campi facoltativi.</summary>
public class FilterListFilterValidator : IValidator<FilterListFilter>
{
    public ValidationResult Validate(FilterListFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.MaxLength(errors, "name", input.Name, ColumnLengths.FilterName);
        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress, ColumnLengths.EmailAddress);
        return ValidationResult.From(errors);
    }
}
