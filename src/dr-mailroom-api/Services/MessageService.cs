using dr_mailroom_api.Dto;
using dr_mailroom_core.Infrastructure.Mailroom;

namespace dr_mailroom_api.Services;

/// <summary>Servizio dei messaggi indicizzati: allegati salvati collegati alla mail.</summary>
public class MessageService(MessageProvider messageProvider, AttachmentProvider attachmentProvider)
{
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
