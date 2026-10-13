using System.Net;
using Google;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Gmail;

/// <summary>
/// Sessione del cestino: users.messages.batchModify con la label TRASH (fino a 1.000 id per chiamata); se Google
/// rifiuta la chiamata in blocco con 400, ripiega su users.messages.trash una mail alla volta. Quota e ritentativi
/// come la lettura (GmailCallExecutor).
/// </summary>
public sealed class GmailTrashSession(
    GmailService service,
    IAuthorizationCodeFlow flow,
    GmailRetrySettings retry,
    int quotaUnitsPerMinute,
    ILogger<GmailTrashSession> logger) : IGmailTrashSession
{
    private readonly GmailCallExecutor _executor = new(retry, quotaUnitsPerMinute, logger);

    /// <inheritdoc />
    public async Task TrashAsync(IReadOnlyList<string> gmailMessageIds, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gmailMessageIds.Count, GmailConstants.MaxBatchModifyIds);
        if (gmailMessageIds.Count == 0)
        {
            return;
        }

        var request = new BatchModifyMessagesRequest
        {
            Ids = [.. gmailMessageIds],
            AddLabelIds = [GmailConstants.TrashLabel],
        };

        try
        {
            // Chiamo Gmail: users.messages.batchModify, un blocco di mail nel cestino
            await _executor.ExecuteAsync(
                ct => service.Users.Messages.BatchModify(request, GmailConstants.Me).ExecuteAsync(ct),
                GmailConstants.BatchModifyCost,
                nameof(TrashAsync),
                cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Gmail batchModify rifiutato (HTTP 400: {GoogleMessage}): ripiego su messages.trash per {Count} mail",
                ex.Error?.Message, gmailMessageIds.Count);
            await TrashOneByOneAsync(gmailMessageIds, cancellationToken);
        }
    }

    /// <summary>Rilascia l'esecutore, il client HTTP di Gmail e quello del flusso OAuth.</summary>
    public void Dispose()
    {
        _executor.Dispose();
        service.Dispose();
        flow.Dispose();
    }

    /// <summary>Ripiego: una chiamata per mail; una mail che non esiste più in Gmail (404) è già fuori dalla casella.</summary>
    private async Task TrashOneByOneAsync(IReadOnlyList<string> gmailMessageIds, CancellationToken cancellationToken)
    {
        foreach (var id in gmailMessageIds)
        {
            try
            {
                // Chiamo Gmail: users.messages.trash di una mail
                await _executor.ExecuteAsync(
                    ct => service.Users.Messages.Trash(GmailConstants.Me, id).ExecuteAsync(ct),
                    GmailConstants.MessagesTrashCost,
                    nameof(TrashOneByOneAsync),
                    cancellationToken);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Gmail messages.trash: mail già assente in Gmail, la considero cestinata");
            }
        }
    }
}
