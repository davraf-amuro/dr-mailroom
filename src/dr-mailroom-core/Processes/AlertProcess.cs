using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Processes;

/// <summary>Registra un'allerta per il messaggio (mittente, oggetto, ricezione); la notifica la fa il software esterno (D3).</summary>
public class AlertProcess(
    AlertProvider alertProvider,
    ProcessExecutionProvider executionProvider,
    ILogger<AlertProcess> logger) : IMessageProcess
{
    public ProcessType Type => ProcessType.Alert;

    /// <summary>Crea l'allerta una sola volta e collega tutti i filtri corrispondenti che prevedono l'allerta.</summary>
    public async Task<ProcessExecutionStatus> ExecuteAsync(ProcessContext context, CancellationToken cancellationToken)
    {
        var message = context.Message;
        try
        {
            var filterIds = context.Match.MatchedFilters
                .Where(f => f.Processes.Any(p => p.ProcessType == ProcessType.Alert))
                .Select(f => f.Id)
                .ToList();

            // Scrivo su DB l'allerta (o i soli nuovi collegamenti ai filtri)
            await alertProvider.EnsureAlertAsync(message, filterIds, cancellationToken);

            // Scrivo su DB l'esito del processo
            await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Succeeded, null, cancellationToken);
            return ProcessExecutionStatus.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TokenRevokedException)
        {
            logger.LogError(ex, "Creazione allerta fallita per il messaggio {MessageId}", message.Id);

            // Scrivo su DB l'esito di errore: il giro prosegue con gli altri messaggi
            await executionProvider.RecordAsync(message.Id, Type, ProcessExecutionStatus.Failed, ex.Message, cancellationToken);
            return ProcessExecutionStatus.Failed;
        }
    }
}
