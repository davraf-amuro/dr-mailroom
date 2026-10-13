using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;

namespace dr_mailroom_api.Validators;

/// <summary>Download del contenuto: format obbligatorio tra i valori di MessageContentFormat, maiuscole indifferenti.</summary>
public class MessageContentQueryValidator : IValidator<MessageContentQuery>
{
    /// <summary>Valida il formato prima di leggere il file: errori raccolti per campo.</summary>
    public ValidationResult Validate(MessageContentQuery input)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(input.Format))
        {
            ValidationRules.Add(errors, "format", "Il formato è obbligatorio (eml o txt)");
        }
        else if (!TryParse(input.Format, out _))
        {
            ValidationRules.Add(errors, "format", "Formato non valido: eml o txt");
        }

        return ValidationResult.From(errors);
    }

    /// <summary>Formato riconosciuto solo per nome (non per numero), maiuscole indifferenti.</summary>
    public static bool TryParse(string value, out MessageContentFormat format) =>
        Enum.TryParse(value.Trim(), ignoreCase: true, out format)
        && Enum.IsDefined(format)
        && !int.TryParse(value, out _);
}
