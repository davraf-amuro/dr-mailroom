using dr_mailroom_core.Gmail;

namespace dr_mailroom_tests.Runs;

/// <summary>
/// Cestino Gmail finto: registra i blocchi ricevuti per token; può revocare il token o fallire da un certo blocco in poi,
/// e può eseguire un'azione a ogni blocco (per esempio annullare il giro).
/// </summary>
public sealed class FakeGmailTrasher : IGmailTrasher
{
    /// <summary>Blocchi di id Gmail ricevuti, nell'ordine, per token del cestino.</summary>
    public Dictionary<string, List<IReadOnlyList<string>>> Batches { get; } = [];

    /// <summary>Token per cui Google risponde invalid_grant.</summary>
    public HashSet<string> RevokedTokens { get; } = [];

    /// <summary>Indice (da 0) del blocco che fallisce con un errore generico; null = nessuno.</summary>
    public int? FailAtBatch { get; set; }

    /// <summary>Eseguita dopo ogni blocco riuscito, con il numero di blocchi fatti.</summary>
    public Action<int>? AfterBatch { get; set; }

    public IGmailTrashSession OpenSession(string trashRefreshToken) => new Session(this, trashRefreshToken);

    private sealed class Session(FakeGmailTrasher owner, string token) : IGmailTrashSession
    {
        public Task TrashAsync(IReadOnlyList<string> gmailMessageIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (owner.RevokedTokens.Contains(token))
            {
                throw new TokenRevokedException(new InvalidOperationException("invalid_grant"));
            }

            if (!owner.Batches.TryGetValue(token, out var batches))
            {
                batches = [];
                owner.Batches[token] = batches;
            }

            if (owner.FailAtBatch == batches.Count)
            {
                throw new InvalidOperationException("Gmail non raggiungibile");
            }

            batches.Add([.. gmailMessageIds]);
            owner.AfterBatch?.Invoke(batches.Count);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
