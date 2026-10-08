using dr_mailroom_api.Dto;

namespace dr_mailroom_api.Validators;

/// <summary>Validatore della riapplica: processi facoltativi, ma se presenti non vuoti, ammessi e senza duplicati (D15).</summary>
public class ReapplyRequestValidator : IValidator<ReapplyRequest>
{
    /// <summary>Che i processi appartengano al filtro lo verifica il servizio, che conosce il filtro.</summary>
    public ValidationResult Validate(ReapplyRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        ValidationRules.Processes(errors, input.Processes, required: false);
        return ValidationResult.From(errors);
    }
}
