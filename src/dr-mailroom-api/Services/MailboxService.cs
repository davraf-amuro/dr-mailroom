using dr_mailroom_api.Dto;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using dr_mailroom_core.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace dr_mailroom_api.Services;

/// <summary>Servizio delle caselle: consultazione, collegamento OAuth ed eliminazione logica (D18).</summary>
public class MailboxService(
    MailboxProvider mailboxProvider,
    RunProvider runProvider,
    IGmailAuthorizer authorizer,
    ITokenProtector tokenProtector,
    OAuthStateProtector stateProtector,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<MailboxService> logger)
{
    /// <summary>Parametro di query con cui il frontend riceve l'esito del collegamento.</summary>
    public const string OAuthResultParameter = "oauth";

    /// <summary>Parametro di query con il motivo di un collegamento fallito.</summary>
    public const string OAuthReasonParameter = "reason";

    /// <summary>Pagina del frontend che riceve il ritorno dal consenso Google.</summary>
    public const string FrontendMailboxesPath = "/mailboxes";

    /// <summary>Caselle che rispettano il filtro, DTO completo.</summary>
    public Task<List<MailboxDto>> GetAllAsync(MailboxFilter filter, CancellationToken cancellationToken) =>
        mailboxProvider.GetMailboxAsync(filter, MailboxDto.Projection, cancellationToken);

    /// <summary>Caselle che rispettano il filtro, DTO ridotto.</summary>
    public Task<List<MailboxSummaryDto>> GetSummariesAsync(MailboxFilter filter, CancellationToken cancellationToken) =>
        mailboxProvider.GetMailboxAsync(filter, MailboxSummaryDto.Projection, cancellationToken);

    /// <summary>Una casella, null se non esiste.</summary>
    public Task<MailboxDto?> GetByIdAsync(int mailboxId, CancellationToken cancellationToken) =>
        mailboxProvider.GetMailboxByIdAsync(mailboxId, MailboxDto.Projection, cancellationToken);

    /// <summary>URL di consenso Google con uno state firmato e a scadenza.</summary>
    public ConnectUrlDto GetConnectUrl() => new(authorizer.BuildAuthorizationUrl(stateProtector.Create()));

    /// <summary>
    /// Gestisce il ritorno da Google: verifica lo state, scambia il code, cifra e salva il token.
    /// Restituisce sempre l'URL del frontend con l'esito in query string; mai il token.
    /// </summary>
    public async Task<string> HandleCallbackAsync(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("Consenso Google non concesso: {OAuthError}", error);
            return FrontendUrl("error", error);
        }

        if (!stateProtector.IsValid(state) || string.IsNullOrEmpty(code))
        {
            logger.LogWarning("Callback OAuth rifiutato: state non valido o code assente");
            return FrontendUrl("error", "invalid_state");
        }

        try
        {
            // Chiamo Google: code → refresh token e indirizzo della casella
            var connected = await authorizer.ExchangeCodeAsync(code, cancellationToken);

            // Scrivo su DB la casella con il token cifrato
            var mailboxId = await mailboxProvider.UpsertConnectedMailboxAsync(
                connected.EmailAddress, tokenProtector.Protect(connected.RefreshToken), cancellationToken);

            return mailboxId is null ? FrontendUrl("error", "mailbox_deleted") : FrontendUrl("ok", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Collegamento della casella fallito");
            return FrontendUrl("error", "exchange_failed");
        }
    }

    /// <summary>Eliminazione logica: 404 se non esiste, 409 se ha un giro in corso.</summary>
    public async Task<ServiceResult<bool>> DeleteAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB stato della casella e giri in corso
        var status = await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken);
        if (status is null)
        {
            return ServiceResult<bool>.NotFound(ServiceMessages.MailboxNotFound);
        }

        if (status == MailboxStatus.Deleted)
        {
            return ServiceResult<bool>.Ok(true);
        }

        if (await runProvider.HasRunningRunAsync(mailboxId, cancellationToken))
        {
            return ServiceResult<bool>.Conflict(ServiceMessages.MailboxBusy);
        }

        // Scrivo su DB l'eliminazione logica
        await mailboxProvider.MarkDeletedAsync(mailboxId, cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>URL del frontend con l'esito del collegamento.</summary>
    private string FrontendUrl(string result, string? reason)
    {
        var url = QueryHelpers.AddQueryString(frontendOptions.Value.BaseUrl.TrimEnd('/') + FrontendMailboxesPath, OAuthResultParameter, result);
        return reason is null ? url : QueryHelpers.AddQueryString(url, OAuthReasonParameter, reason);
    }
}
