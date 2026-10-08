namespace dr_mailroom_core.Runs;

/// <summary>Avvia subito, in background, un giro già creato (manuale o riapplica, D11/D13).</summary>
public interface IRunDispatcher
{
    /// <summary>Affida il giro allo scheduler; ritorna appena è accodato, non a giro concluso.</summary>
    Task DispatchAsync(long runId, CancellationToken cancellationToken);
}
