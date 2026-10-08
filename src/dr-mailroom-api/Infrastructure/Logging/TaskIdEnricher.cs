using Serilog.Core;
using Serilog.Events;

namespace dr_mailroom_api.Infrastructure.Logging;

/// <summary>Aggiunge a ogni evento di log l'ID del Task async corrente ("-" fuori da un Task).</summary>
internal sealed class TaskIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory) =>
        logEvent.AddPropertyIfAbsent(
            factory.CreateProperty("TaskId", Task.CurrentId?.ToString() ?? "-"));
}
