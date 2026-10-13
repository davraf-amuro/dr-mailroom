using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Validators;

/// <summary>Mail di un filtro: pagina ≥ 1, pageSize 1–100; il verso dell'ordinamento lo valida già il binding dell'enum.</summary>
public class FilterMessageFilterValidator : IValidator<FilterMessageFilter>
{
    /// <summary>Valida la paginazione prima della query: errori raccolti per campo.</summary>
    public ValidationResult Validate(FilterMessageFilter input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.Page is < 1)
        {
            ValidationRules.Add(errors, "page", "La pagina parte da 1");
        }

        if (input.PageSize is < 1 or > FilterMessageFilter.MaxPageSize)
        {
            ValidationRules.Add(errors, "pageSize", $"pageSize va da 1 a {FilterMessageFilter.MaxPageSize}");
        }

        return ValidationResult.From(errors);
    }
}
