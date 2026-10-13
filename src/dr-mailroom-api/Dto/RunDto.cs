using System.Linq.Expressions;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Giro completo con contatori ed esito; SenderAddress solo per il cestino di un mittente (TrashSender).</summary>
public record RunDto(long Id, int MailboxId, RunTrigger Trigger, int? FilterId, string? RequestedProcesses, string? SenderAddress, RunStatus Status, DateTime StartedAtUtc, DateTime? EndedAtUtc, int MessagesRead, int MessagesProcessed, int ErrorCount, string? ErrorSummary)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<Run, RunDto>> Projection =>
        e => new(e.Id, e.MailboxId, e.Trigger, e.FilterId, e.RequestedProcesses, e.SenderAddress, e.Status, e.StartedAtUtc, e.EndedAtUtc, e.MessagesRead, e.MessagesProcessed, e.ErrorCount, e.ErrorSummary);
}
