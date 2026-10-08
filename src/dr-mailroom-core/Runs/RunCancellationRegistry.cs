using System.Collections.Concurrent;

namespace dr_mailroom_core.Runs;

/// <summary>
/// Giri in esecuzione in questo processo, annullabili su richiesta (API) o all'arresto del servizio.
/// Ogni giro lavora con un token collegato a quello del job: annullare uno non tocca gli altri.
/// </summary>
public sealed class RunCancellationRegistry
{
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _runs = new();
    private volatile bool _shuttingDown;

    /// <summary>Vero dopo CancelAll: distingue l'arresto del servizio dall'annullamento chiesto via API.</summary>
    public bool IsShuttingDown => _shuttingDown;

    /// <summary>Registra il giro; il dispose dell'handle lo rimuove. Durante l'arresto il token nasce già annullato.</summary>
    public RunRegistration Register(long runId, CancellationToken jobToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(jobToken);
        if (!_runs.TryAdd(runId, cts))
        {
            // Lo stesso giro eseguito due volte: il secondo non deve lavorare né chiudere il giro del primo
            cts.Dispose();
            throw new InvalidOperationException($"Giro {runId} già in esecuzione");
        }

        if (IsShuttingDown)
        {
            cts.Cancel();
        }

        return new RunRegistration(this, runId, cts);
    }

    /// <summary>Annulla il giro se è in esecuzione in questo processo; false se non è registrato.</summary>
    public bool TryCancel(long runId) =>
        _runs.TryGetValue(runId, out var cts) && TryCancel(cts);

    /// <summary>Annulla tutti i giri registrati (arresto del servizio).</summary>
    public void CancelAll()
    {
        _shuttingDown = true;
        foreach (var cts in _runs.Values)
        {
            TryCancel(cts);
        }
    }

    /// <summary>Rimuove il giro solo se l'handle è quello registrato.</summary>
    internal void Unregister(long runId, CancellationTokenSource cts) =>
        _runs.TryRemove(new KeyValuePair<long, CancellationTokenSource>(runId, cts));

    /// <summary>Annulla il token; false se il giro è appena terminato e il token è già stato liberato.</summary>
    private static bool TryCancel(CancellationTokenSource cts)
    {
        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
