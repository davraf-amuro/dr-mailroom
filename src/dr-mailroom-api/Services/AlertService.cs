using dr_mailroom_api.Dto;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Services;

/// <summary>Servizio delle allerte per il software esterno: lettura e consumo (D3).</summary>
public class AlertService(AlertProvider alertProvider)
{
    /// <summary>Allerte che rispettano il filtro, DTO completo.</summary>
    public Task<List<AlertDto>> GetAllAsync(AlertListFilter filter, CancellationToken cancellationToken) =>
        alertProvider.GetAlertAsync(filter, AlertDto.Projection, cancellationToken);

    /// <summary>Allerte che rispettano il filtro, DTO ridotto.</summary>
    public Task<List<AlertSummaryDto>> GetSummariesAsync(AlertListFilter filter, CancellationToken cancellationToken) =>
        alertProvider.GetAlertAsync(filter, AlertSummaryDto.Projection, cancellationToken);

    /// <summary>Marca l'allerta come consumata; idempotente.</summary>
    public async Task<ServiceResult<bool>> ConsumeAsync(long alertId, CancellationToken cancellationToken) =>
        await alertProvider.ConsumeAlertAsync(alertId, cancellationToken)
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.NotFound(ServiceMessages.AlertNotFound);
}
