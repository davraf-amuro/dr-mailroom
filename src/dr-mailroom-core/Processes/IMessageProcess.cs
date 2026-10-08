using dr_mailroom_core.Enums;

namespace dr_mailroom_core.Processes;

/// <summary>Processo di lavorazione di un messaggio filtrato; idempotente, registra il proprio esito in ProcessExecution.</summary>
public interface IMessageProcess
{
    /// <summary>Tipo di processo, usato dall'orchestratore per scegliere i processi da applicare (anche un sottoinsieme, D13).</summary>
    ProcessType Type { get; }

    /// <summary>Lavora il messaggio. Gli errori del singolo messaggio diventano esito Failed, non eccezioni.</summary>
    Task<ProcessExecutionStatus> ExecuteAsync(ProcessContext context, CancellationToken cancellationToken);
}
