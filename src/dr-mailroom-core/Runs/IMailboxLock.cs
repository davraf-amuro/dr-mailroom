namespace dr_mailroom_core.Runs;

/// <summary>Lock esclusivo per casella: un solo giro alla volta (T8).</summary>
public interface IMailboxLock
{
    /// <summary>Prova a prendere il lock senza attendere; null se la casella è occupata. Il dispose lo rilascia.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(int mailboxId, CancellationToken cancellationToken);
}
