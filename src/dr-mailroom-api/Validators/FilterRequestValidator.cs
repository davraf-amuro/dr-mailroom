using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;

namespace dr_mailroom_api.Validators;

/// <summary>Validatore del body di creazione/modifica filtro, con le regole approvate (D15).</summary>
public class FilterRequestValidator : IValidator<FilterRequest>
{
    /// <summary>Applica tutte le regole del filtro; l'esistenza della casella la verifica il servizio.</summary>
    public ValidationResult Validate(FilterRequest input)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            ValidationRules.Add(errors, "name", "Il nome è obbligatorio");
        }
        else
        {
            ValidationRules.MaxLength(errors, "name", input.Name.Trim(), ColumnLengths.FilterName);
        }

        ValidationRules.SenderAddress(errors, input.SenderAddress);
        ValidationRules.OptionalMailboxId(errors, input.MailboxId);
        ValidationRules.SubjectCondition(errors, input.SubjectMatchType, input.SubjectValue);
        var processes = ValidationRules.Processes(errors, input.Processes, required: true);
        ValidationRules.AttachmentSubfolder(errors, input.AttachmentSubfolder, processes.Contains(ProcessType.AttachmentExtraction));

        return ValidationResult.From(errors);
    }
}
