using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Filtro completo con i processi associati (la lista processi è una sotto-query tradotta da EF, non valutata in memoria).</summary>
public record FilterDto(int Id, int? MailboxId, string Name, string SenderAddress, SubjectMatchType? SubjectMatchType, string? SubjectValue, string? AttachmentSubfolder, bool IsEnabled, List<ProcessType> Processes)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Filter, FilterDto>> Projection =>
        e => new(e.Id, e.MailboxId, e.Name, e.SenderAddress, e.SubjectMatchType, e.SubjectValue, e.AttachmentSubfolder, e.IsEnabled, e.Processes.Select(p => p.ProcessType).ToList());
}
