using dr_mailroom_api.Dto;
using dr_mailroom_core.Infrastructure.Mailroom;

namespace dr_mailroom_api.Validators;

/// <summary>
/// "Elimina tutte": casella e mittente obbligatori, mittente entro la lunghezza della colonna, data limite facoltativa e valida. Come nella vista del
/// mittente, niente controllo di formato email: l'indirizzo è quello salvato così come arrivato da Gmail.
/// </summary>
public class TrashSenderRequestValidator : IValidator<TrashSenderRequest>
{
    /// <summary>Valida il body prima di avviare il giro: errori raccolti per campo.</summary>
    public ValidationResult Validate(TrashSenderRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.RequiredMailboxId(errors, input.MailboxId);

        if (string.IsNullOrWhiteSpace(input.SenderAddress))
        {
            ValidationRules.Add(errors, "senderAddress", "Il mittente è obbligatorio");
        }

        ValidationRules.MaxLength(errors, "senderAddress", input.SenderAddress?.Trim(), ColumnLengths.EmailAddress);
        ValidationRules.ReceivedBefore(errors, input.ReceivedBeforeUtc);
        return ValidationResult.From(errors);
    }
}
