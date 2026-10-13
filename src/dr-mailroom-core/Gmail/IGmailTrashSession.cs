namespace dr_mailroom_core.Gmail;

/// <summary>Sessione del cestino di una casella: sposta mail nel cestino di Gmail, niente altro.</summary>
public interface IGmailTrashSession : IDisposable
{
    /// <summary>
    /// Sposta nel cestino un blocco di al più <see cref="GmailConstants.MaxBatchModifyIds"/> mail.
    /// invalid_grant → TokenRevokedException (si riferisce al solo token del cestino).
    /// </summary>
    Task TrashAsync(IReadOnlyList<string> gmailMessageIds, CancellationToken cancellationToken);
}
