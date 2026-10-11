using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>
/// Mail di un mittente: mailboxId e senderAddress obbligatori, ricerca ≤ lunghezza dell'oggetto, pagina ≥ 1, pageSize 1–100.
/// Il mittente non passa dal controllo di formato email: deve poter aprire anche indirizzi salvati così come arrivati da Gmail.
/// </summary>
public class SenderMessageFilterValidator : IValidator<SenderMessageFilter>
{
    /// <summary>Valida il filtro prima della query: errori raccolti per campo.</summary>
    public ValidationResult Validate(SenderMessageFilter input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.RequiredMailboxId(errors, input.MailboxId);

        if (string.IsNullOrWhiteSpace(input.SenderAddress))
        {
            ValidationRules.Add(errors, "senderAddress", "Il mittente è obbligatorio");
        }

        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress?.Trim(), ColumnLengths.EmailAddress);
        ValidationRules.MaxLength(errors, "search", input.Search, ColumnLengths.Subject);

        if (input.Page is < 1)
        {
            ValidationRules.Add(errors, "page", "La pagina parte da 1");
        }

        if (input.PageSize is < 1 or > SenderMessageFilter.MaxPageSize)
        {
            ValidationRules.Add(errors, "pageSize", $"pageSize va da 1 a {SenderMessageFilter.MaxPageSize}");
        }

        return ValidationResult.From(errors);
    }
}
