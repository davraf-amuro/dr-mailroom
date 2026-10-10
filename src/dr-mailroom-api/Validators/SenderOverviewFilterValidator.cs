using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Panoramica mittenti: mailboxId obbligatorio, anno plausibile, pagina ≥ 1, pageSize 1–100, ricerca ≤ 320.</summary>
public class SenderOverviewFilterValidator : IValidator<SenderOverviewFilter>
{
    public ValidationResult Validate(SenderOverviewFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.RequiredMailboxId(errors, input.MailboxId);
        ValidationRules.OptionalYear(errors, input.Year);
        ValidationRules.MaxLength(errors, "search", input.Search, ColumnLengths.EmailAddress);

        if (input.Page is < 1)
        {
            ValidationRules.Add(errors, "page", "La pagina parte da 1");
        }

        if (input.PageSize is < 1 or > SenderOverviewFilter.MaxPageSize)
        {
            ValidationRules.Add(errors, "pageSize", $"pageSize va da 1 a {SenderOverviewFilter.MaxPageSize}");
        }

        return ValidationResult.From(errors);
    }
}
