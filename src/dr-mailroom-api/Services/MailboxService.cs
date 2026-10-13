using System.Globalization;
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

/// <summary>Servizio delle caselle: consultazione, collegamento OAuth, pausa e ripresa, eliminazione logica (D18).</summary>
public class MailboxService(
    MailboxProvider mailboxProvider,
    RunProvider runProvider,
    RunService runService,
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

    /// <summary>Vista delle mail di un mittente: il ritorno dal consenso del cestino riporta l'utente lì.</summary>
    public const string FrontendSenderMessagesPath = "/stats/messages";

    /// <summary>Parametri di query della vista del mittente.</summary>
    public const string MailboxIdParameter = "mailboxId";
    public const string SenderParameter = "sender";

    /// <summary>Motivi di un'autorizzazione del cestino fallita (oltre a quelli del collegamento).</summary>
    public const string TrashMailboxMismatchReason = "trash_mailbox_mismatch";
    public const string TrashMailboxUnavailableReason = "trash_mailbox_unavailable";

    /// <summary>Caselle che rispettano il filtro, DTO completo.</summary>
    public Task<List<MailboxDto>> GetAllAsync(MailboxFilter filter, CancellationToken cancellationToken) =>
        mailboxProvider.GetMailboxAsync(filter, MailboxDto.Projection, cancellationToken);

    /// <summary>Caselle che rispettano il filtro, DTO ridotto.</summary>
    public Task<List<MailboxSummaryDto>> GetSummariesAsync(MailboxFilter filter, CancellationToken cancellationToken) =>
        mailboxProvider.GetMailboxAsync(filter, MailboxSummaryDto.Projection, cancellationToken);

    /// <summary>Una casella, null se non esiste.</summary>
    public Task<MailboxDto?> GetByIdAsync(int mailboxId, CancellationToken cancellationToken) =>
        mailboxProvider.GetMailboxByIdAsync(mailboxId, MailboxDto.Projection, cancellationToken);

    /// <summary>URL di consenso Google (sola lettura) con uno state firmato e a scadenza.</summary>
    public ConnectUrlDto GetConnectUrl() =>
        new(authorizer.BuildAuthorizationUrl(stateProtector.CreateForConnect(), OAuthPurpose.Connect));

    /// <summary>
    /// URL di consenso Google per il cestino della casella (scope gmail.modify, D-T1 bis); senderAddress è solo la vista
    /// a cui tornare. 404 se non esiste, 409 se non è attiva: il cestino si autorizza solo su una casella che si legge.
    /// </summary>
    public async Task<ServiceResult<ConnectUrlDto>> GetTrashAuthorizationUrlAsync(
        int mailboxId, string? senderAddress, CancellationToken cancellationToken)
    {
        // Leggo da DB lo stato della casella
        var status = await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken);
        if (status is null)
        {
            return ServiceResult<ConnectUrlDto>.NotFound(ServiceMessages.MailboxNotFound);
        }

        if (status != MailboxStatus.Active)
        {
            return ServiceResult<ConnectUrlDto>.Conflict(ServiceMessages.MailboxNotActive);
        }

        var state = stateProtector.CreateForTrash(mailboxId, senderAddress);
        return ServiceResult<ConnectUrlDto>.Ok(new(authorizer.BuildAuthorizationUrl(state, OAuthPurpose.Trash)));
    }

    /// <summary>
    /// Gestisce il ritorno da Google: verifica lo state, scambia il code, cifra e salva il token.
    /// Restituisce sempre l'URL del frontend con l'esito in query string; mai il token.
    /// </summary>
    public async Task<string> HandleCallbackAsync(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        var payload = stateProtector.Read(state);
        if (payload is null)
        {
            logger.LogWarning("Callback OAuth rifiutato: state non valido o scaduto");
            return FrontendUrl("error", string.IsNullOrEmpty(error) ? "invalid_state" : error);
        }

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("Consenso Google {Purpose} non concesso: {OAuthError}", payload.Purpose, error);
            return ReturnUrl(payload, "error", error);
        }

        if (string.IsNullOrEmpty(code))
        {
            logger.LogWarning("Callback OAuth rifiutato: code assente");
            return ReturnUrl(payload, "error", "invalid_state");
        }

        return payload.Purpose == OAuthPurpose.Trash
            ? await HandleTrashCallbackAsync(code, payload, cancellationToken)
            : await HandleConnectCallbackAsync(code, cancellationToken);
    }

    /// <summary>Collegamento: code → refresh token di lettura, casella creata o riattivata.</summary>
    private async Task<string> HandleConnectCallbackAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            // Chiamo Google: code → refresh token e indirizzo della casella
            var connected = await authorizer.ExchangeCodeAsync(code, OAuthPurpose.Connect, cancellationToken);

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

    /// <summary>
    /// Cestino: code → refresh token gmail.modify, salvato solo se l'account autorizzato è proprio quello della casella
    /// e la casella è ancora attiva. Il token di lettura non cambia.
    /// </summary>
    private async Task<string> HandleTrashCallbackAsync(string code, OAuthState payload, CancellationToken cancellationToken)
    {
        if (payload.MailboxId is not { } mailboxId)
        {
            return ReturnUrl(payload, "error", "invalid_state");
        }

        try
        {
            // Chiamo Google: code → refresh token del cestino e indirizzo dell'account autorizzato
            var connected = await authorizer.ExchangeCodeAsync(code, OAuthPurpose.Trash, cancellationToken);

            // Leggo da DB indirizzo e stato della casella da autorizzare
            var mailbox = await mailboxProvider.GetMailboxByIdAsync(
                mailboxId, m => new { m.EmailAddress, m.Status }, cancellationToken);
            if (mailbox is null || mailbox.Status != MailboxStatus.Active)
            {
                logger.LogWarning("Autorizzazione del cestino rifiutata: casella {MailboxId} assente o non attiva", mailboxId);
                return ReturnUrl(payload, "error", TrashMailboxUnavailableReason);
            }

            if (!string.Equals(mailbox.EmailAddress, connected.EmailAddress.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Autorizzazione del cestino rifiutata: account Google diverso dalla casella {MailboxId}", mailboxId);
                return ReturnUrl(payload, "error", TrashMailboxMismatchReason);
            }

            // Scrivo su DB il token del cestino cifrato
            await mailboxProvider.SetTrashTokenAsync(mailboxId, tokenProtector.Protect(connected.RefreshToken), cancellationToken);
            return ReturnUrl(payload, "ok", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Autorizzazione del cestino della casella {MailboxId} fallita", mailboxId);
            return ReturnUrl(payload, "error", "exchange_failed");
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

    /// <summary>
    /// Pausa: stato Disabled, nessun giro (cron, manuale, riapplica) finché non si riprende; annulla i giri in corso.
    /// 404 se non esiste, 409 se eliminata o con token revocato. Idempotente.
    /// </summary>
    public async Task<ServiceResult<bool>> PauseAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB lo stato della casella
        var status = await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken);
        switch (status)
        {
            case null:
                return ServiceResult<bool>.NotFound(ServiceMessages.MailboxNotFound);
            case MailboxStatus.Deleted:
                return ServiceResult<bool>.Conflict(ServiceMessages.MailboxDeleted);
            case MailboxStatus.TokenRevoked:
                return ServiceResult<bool>.Conflict(ServiceMessages.MailboxTokenRevoked);
            case MailboxStatus.Active:
                // Scrivo su DB la pausa prima di annullare: un giro che parte ora trova già la casella in pausa
                await mailboxProvider.SetStatusAsync(mailboxId, MailboxStatus.Disabled, cancellationToken);
                break;
        }

        await runService.CancelRunningRunsAsync(mailboxId, cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Ripresa: Disabled → Active, la casella torna nei giri. 404 se non esiste, 409 se non è in pausa. Idempotente su Active.</summary>
    public async Task<ServiceResult<bool>> ResumeAsync(int mailboxId, CancellationToken cancellationToken)
    {
        // Leggo da DB lo stato della casella
        var status = await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken);
        switch (status)
        {
            case null:
                return ServiceResult<bool>.NotFound(ServiceMessages.MailboxNotFound);
            case MailboxStatus.Active:
                return ServiceResult<bool>.Ok(true);
            case MailboxStatus.Disabled:
                // Scrivo su DB la ripresa
                await mailboxProvider.SetStatusAsync(mailboxId, MailboxStatus.Active, cancellationToken);
                return ServiceResult<bool>.Ok(true);
            default:
                return ServiceResult<bool>.Conflict(ServiceMessages.MailboxNotPaused);
        }
    }

    /// <summary>
    /// URL di ritorno con l'esito: per il cestino la vista del mittente da cui l'utente è partito (se nota),
    /// altrimenti la pagina delle caselle.
    /// </summary>
    private string ReturnUrl(OAuthState payload, string result, string? reason)
    {
        if (payload.Purpose != OAuthPurpose.Trash || payload.MailboxId is not { } mailboxId
            || string.IsNullOrEmpty(payload.SenderAddress))
        {
            return FrontendUrl(result, reason);
        }

        var url = frontendOptions.Value.BaseUrl.TrimEnd('/') + FrontendSenderMessagesPath;
        url = QueryHelpers.AddQueryString(url, MailboxIdParameter, mailboxId.ToString(CultureInfo.InvariantCulture));
        url = QueryHelpers.AddQueryString(url, SenderParameter, payload.SenderAddress);
        url = QueryHelpers.AddQueryString(url, OAuthResultParameter, result);
        return reason is null ? url : QueryHelpers.AddQueryString(url, OAuthReasonParameter, reason);
    }

    /// <summary>URL del frontend con l'esito del collegamento.</summary>
    private string FrontendUrl(string result, string? reason)
    {
        var url = QueryHelpers.AddQueryString(frontendOptions.Value.BaseUrl.TrimEnd('/') + FrontendMailboxesPath, OAuthResultParameter, result);
        return reason is null ? url : QueryHelpers.AddQueryString(url, OAuthReasonParameter, reason);
    }
}
