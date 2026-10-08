using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Filtro caselle: EmailAddress ≤ nvarchar(320). Regole ereditate dalle colonne, tutti i campi facoltativi.</summary>
public class MailboxFilterValidator : IValidator<MailboxFilter>
{
    public ValidationResult Validate(MailboxFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.MaxLength(errors, "emailAddress", input.EmailAddress, ColumnLengths.EmailAddress);
        return ValidationResult.From(errors);
    }
}
