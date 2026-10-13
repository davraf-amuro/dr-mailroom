using System.Text;
using dr_mailroom_api.Dto;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Helpers;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using dr_mailroom_core.Processes;
using Microsoft.Extensions.Options;

namespace dr_mailroom_api.Services;

/// <summary>Servizio dei messaggi indicizzati: griglia delle mail di un mittente, allegati salvati e contenuto archiviato della mail.</summary>
public class MessageService(
    MessageProvider messageProvider,
    AttachmentProvider attachmentProvider,
    ArchiveProvider archiveProvider,
    MailboxProvider mailboxProvider,
    IOptions<AttachmentsOptions> attachmentsOptions,
    ILogger<MessageService> logger)
{
    /// <summary>Tipo del .eml: mai interpretato dal browser, solo scaricato.</summary>
    public const string EmlContentType = "application/octet-stream";

    /// <summary>Tipo del testo ricavato dal .eml.</summary>
    public const string TxtContentType = "text/plain; charset=utf-8";

    /// <summary>Estensione del download in formato testo.</summary>
    public const string TxtExtension = ".txt";

    /// <summary>
    /// Pagina delle mail di un mittente in una casella (filtro già validato: MailboxId e SenderAddress presenti).
    /// Default: dalla più recente, pagina 1 da 25. NotFound se la casella non esiste; nessuna mail → pagina vuota.
    /// </summary>
    public async Task<ServiceResult<SenderMessagePageDto>> GetSenderMessagesAsync(SenderMessageFilter filter, CancellationToken cancellationToken)
    {
        var mailboxId = filter.MailboxId!.Value;

        // Leggo da DB lo stato della casella per distinguere "casella inesistente" da "nessuna mail"
        if (await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken) is null)
        {
            return ServiceResult<SenderMessagePageDto>.NotFound(ServiceMessages.MailboxNotFound);
        }

        // Mittente salvato sempre minuscolo (D7); ricerca vuota = nessun filtro sull'oggetto
        filter.SenderAddress = filter.SenderAddress!.Trim().ToLowerInvariant();
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        filter.ReceivedBeforeUtc = UtcDates.ToUtc(filter.ReceivedBeforeUtc);

        var page = filter.Page ?? SenderMessageFilter.DefaultPage;
        var pageSize = filter.PageSize ?? SenderMessageFilter.DefaultPageSize;
        var sortDir = filter.SortDir ?? SortDirection.Desc;

        // Leggo da DB la pagina e il totale
        var (items, totalCount) = await messageProvider.GetSenderMessagesPageAsync(
            filter, sortDir, page, pageSize, SenderMessageDto.Projection, cancellationToken);

        return ServiceResult<SenderMessagePageDto>.Ok(new SenderMessagePageDto(items, page, pageSize, totalCount));
    }

    /// <summary>Allegati salvati del messaggio; NotFound se il messaggio non esiste.</summary>
    public async Task<ServiceResult<List<AttachmentDto>>> GetAttachmentsAsync(long messageId, CancellationToken cancellationToken)
    {
        // Leggo da DB l'esistenza del messaggio
        if (!await messageProvider.MessageExistsAsync(messageId, cancellationToken))
        {
            return ServiceResult<List<AttachmentDto>>.NotFound(ServiceMessages.MessageNotFound);
        }

        // Leggo da DB gli allegati salvati
        var attachments = await attachmentProvider.GetAttachmentsAsync(messageId, AttachmentDto.Projection, cancellationToken);
        return ServiceResult<List<AttachmentDto>>.Ok(attachments);
    }

    /// <summary>
    /// Contenuto archiviato della mail: il .eml dal disco o il testo ricavato. Il percorso viene solo dal DB e deve
    /// restare sotto Attachments:RootPath. NotFound se la mail non esiste, è cestinata, non ha contenuto o il file manca;
    /// Conflict se il .eml non si riesce a leggere come messaggio MIME.
    /// </summary>
    public async Task<ServiceResult<MessageContentFile>> GetContentAsync(long messageId, MessageContentFormat format, CancellationToken cancellationToken)
    {
        // Leggo da DB il contenuto archiviato della mail
        var archived = await archiveProvider.GetAsync(messageId, cancellationToken);
        if (archived is null)
        {
            return ServiceResult<MessageContentFile>.NotFound(ServiceMessages.ContentNotArchived);
        }

        var fullPath = ArchiveProvider.ResolveUnderRoot(attachmentsOptions.Value.RootPath, archived.RelativePath);
        if (fullPath is null)
        {
            logger.LogWarning("Contenuto del messaggio {MessageId} con percorso fuori dalla root: {RelativePath}", messageId, archived.RelativePath);
            return ServiceResult<MessageContentFile>.NotFound(ServiceMessages.ContentNotArchived);
        }

        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Contenuto del messaggio {MessageId} assente dal disco: {RelativePath}", messageId, archived.RelativePath);
            return ServiceResult<MessageContentFile>.NotFound(ServiceMessages.ContentFileMissing);
        }

        var fileName = Path.GetFileName(fullPath);
        if (format == MessageContentFormat.Eml)
        {
            return ServiceResult<MessageContentFile>.Ok(new MessageContentFile(fullPath, null, fileName, EmlContentType));
        }

        try
        {
            // Leggo dal disco il .eml e ne ricavo il testo
            await using var stream = File.OpenRead(fullPath);
            var text = await MessageTextExtractor.ToTextAsync(stream, cancellationToken);
            var textName = Path.GetFileNameWithoutExtension(fileName) + TxtExtension;
            return ServiceResult<MessageContentFile>.Ok(new MessageContentFile(null, Encoding.UTF8.GetBytes(text), textName, TxtContentType));
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Contenuto del messaggio {MessageId} non leggibile come MIME: {RelativePath}", messageId, archived.RelativePath);
            return ServiceResult<MessageContentFile>.Conflict(ServiceMessages.ContentUnreadable);
        }
    }
}
