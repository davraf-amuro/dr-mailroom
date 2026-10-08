using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Filtro in forma ridotta.</summary>
public record FilterSummaryDto(int Id, string Name, string SenderAddress, bool IsEnabled)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Filter, FilterSummaryDto>> Projection =>
        e => new(e.Id, e.Name, e.SenderAddress, e.IsEnabled);
}
