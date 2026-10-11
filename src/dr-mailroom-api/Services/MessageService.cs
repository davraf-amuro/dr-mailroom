using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;

namespace dr_mailroom_api.Services;

/// <summary>Servizio dei messaggi indicizzati: griglia delle mail di un mittente e allegati salvati collegati alla mail.</summary>
public class MessageService(MessageProvider messageProvider, AttachmentProvider attachmentProvider, MailboxProvider mailboxProvider)
{
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
}
