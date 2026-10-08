using dr_mailroom_core.Enums;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Accesso dati agli esiti dei processi: un esito per (messaggio, processo), aggiornato a ogni esecuzione.</summary>
public class ProcessExecutionProvider(MailroomDbContext context, ILogger<ProcessExecutionProvider> logger)
{
    /// <summary>Crea o aggiorna l'esito del processo sul messaggio (UNIQUE MessageId, ProcessType).</summary>
    public async Task RecordAsync(
        long messageId,
        ProcessType processType,
        ProcessExecutionStatus status,
        string? error,
        CancellationToken cancellationToken)
    {
        // Leggo da DB l'eventuale esito precedente, con tracking per aggiornarlo
        var execution = await context.ProcessExecutions.AsTracking()
            .FirstOrDefaultAsync(e => e.MessageId == messageId && e.ProcessType == processType, cancellationToken);

        if (execution is null)
        {
            execution = new ProcessExecution { MessageId = messageId, ProcessType = processType };
            context.ProcessExecutions.Add(execution);
        }

        execution.Status = status;
        execution.Error = TextLimits.Truncate(error, ColumnLengths.ErrorText);
        execution.ProcessedAtUtc = DateTime.UtcNow;

        // Scrivo su DB l'esito
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Processo {ProcessType} sul messaggio {MessageId}: {Status}",
            processType, messageId, status);
    }
}
