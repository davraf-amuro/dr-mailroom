using dr_mailroom_api.Dto;

namespace dr_mailroom_api.Validators;

/// <summary>Validatore dell'anteprima: stesse regole del filtro senza nome, processi e sottocartella (D15).</summary>
public class FilterPreviewRequestValidator : IValidator<FilterPreviewRequest>
{
    /// <summary>Valida mittente, casella e condizione sull'oggetto.</summary>
    public ValidationResult Validate(FilterPreviewRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.SenderAddress(errors, input.SenderAddress);
        ValidationRules.OptionalMailboxId(errors, input.MailboxId);
        ValidationRules.SubjectCondition(errors, input.SubjectMatchType, input.SubjectValue);
        return ValidationResult.From(errors);
    }
}
