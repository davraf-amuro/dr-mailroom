using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Mail per anno: mailboxId obbligatorio, senderAddress facoltativo ≤ 320.</summary>
public class YearStatFilterValidator : IValidator<YearStatFilter>
{
    public ValidationResult Validate(YearStatFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.RequiredMailboxId(errors, input.MailboxId);
        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress, ColumnLengths.EmailAddress);
        return ValidationResult.From(errors);
    }
}
