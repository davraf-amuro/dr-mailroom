using dr_mailroom_api.Dto;

namespace dr_mailroom_api.Validators;

/// <summary>Validatore del giro manuale: casella facoltativa e positiva; esistenza e stato li verifica il servizio (D15).</summary>
public class StartRunRequestValidator : IValidator<StartRunRequest>
{
    public ValidationResult Validate(StartRunRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.OptionalMailboxId(errors, input.MailboxId);
        return ValidationResult.From(errors);
    }
}
