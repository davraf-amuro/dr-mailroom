namespace dr_mailroom_core.Runs;

/// <summary>Handle di un giro registrato in RunCancellationRegistry: espone il token da usare per tutto il lavoro.</summary>
public sealed class RunRegistration(RunCancellationRegistry registry, long runId, CancellationTokenSource cts) : IDisposable
{
    /// <summary>Token del giro: annullato dall'API, dall'arresto del servizio o dal job.</summary>
    public CancellationToken Token => cts.Token;

    /// <summary>Rimuove il giro dal registro e libera il token.</summary>
    public void Dispose()
    {
        registry.Unregister(runId, cts);
        cts.Dispose();
    }
}
