using dr_mailroom_api.Dto;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using dr_mailroom_core.Runs;

namespace dr_mailroom_api.Services;

/// <summary>Servizio dei filtri: CRUD, anteprima sulle mail indicizzate e riapplica (D13, D16).</summary>
public class FilterService(
    FilterProvider filterProvider,
    MailboxProvider mailboxProvider,
    MessageProvider messageProvider,
    FilterMatcher filterMatcher,
    RunService runService)
{
    /// <summary>Numero massimo di messaggi restituiti dall'anteprima (i più recenti).</summary>
    public const int PreviewMaxMessages = 50;

    /// <summary>Filtri che rispettano il filtro di ricerca, DTO completo.</summary>
    public Task<List<FilterDto>> GetAllAsync(FilterListFilter filter, CancellationToken cancellationToken) =>
        filterProvider.GetFilterAsync(filter, FilterDto.Projection, cancellationToken);

    /// <summary>Filtri che rispettano il filtro di ricerca, DTO ridotto.</summary>
    public Task<List<FilterSummaryDto>> GetSummariesAsync(FilterListFilter filter, CancellationToken cancellationToken) =>
        filterProvider.GetFilterAsync(filter, FilterSummaryDto.Projection, cancellationToken);

    /// <summary>Un filtro, null se non esiste.</summary>
    public Task<FilterDto?> GetByIdAsync(int filterId, CancellationToken cancellationToken) =>
        filterProvider.GetFilterByIdAsync(filterId, FilterDto.Projection, cancellationToken);

    /// <summary>Crea un filtro validato; casella inesistente → Invalid, casella eliminata → Conflict.</summary>
    public async Task<ServiceResult<FilterDto>> CreateAsync(FilterRequest request, CancellationToken cancellationToken)
    {
        var mailboxCheck = await CheckMailboxAsync<FilterDto>(request.MailboxId, cancellationToken);
        if (mailboxCheck is not null)
        {
            return mailboxCheck;
        }

        // Scrivo su DB il nuovo filtro
        var filterId = await filterProvider.CreateFilterAsync(ToEntity(request), cancellationToken);
        var created = await filterProvider.GetFilterByIdAsync(filterId, FilterDto.Projection, cancellationToken);
        return ServiceResult<FilterDto>.Ok(created!);
    }

    /// <summary>Aggiorna un filtro; non modificabile se la sua casella (attuale o nuova) è eliminata (D18).</summary>
    public async Task<ServiceResult<bool>> UpdateAsync(int filterId, FilterRequest request, CancellationToken cancellationToken)
    {
        // Leggo da DB il filtro attuale
        var current = await filterProvider.GetFilterByIdAsync(filterId, FilterSummaryWithMailbox.Projection, cancellationToken);
        if (current is null)
        {
            return ServiceResult<bool>.NotFound(ServiceMessages.FilterNotFound);
        }

        var currentCheck = await CheckMailboxAsync<bool>(current.MailboxId, cancellationToken);
        if (currentCheck is not null)
        {
            return currentCheck;
        }

        var targetCheck = await CheckMailboxAsync<bool>(request.MailboxId, cancellationToken);
        if (targetCheck is not null)
        {
            return targetCheck;
        }

        var entity = ToEntity(request);
        entity.Id = filterId;

        // Scrivo su DB le modifiche
        await filterProvider.UpdateFilterAsync(entity, cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Cancella un filtro; non cancellabile se appartiene a una casella eliminata (D18).</summary>
    public async Task<ServiceResult<bool>> DeleteAsync(int filterId, CancellationToken cancellationToken)
    {
        // Leggo da DB il filtro attuale
        var current = await filterProvider.GetFilterByIdAsync(filterId, FilterSummaryWithMailbox.Projection, cancellationToken);
        if (current is null)
        {
            return ServiceResult<bool>.NotFound(ServiceMessages.FilterNotFound);
        }

        var check = await CheckMailboxAsync<bool>(current.MailboxId, cancellationToken);
        if (check is not null)
        {
            return check;
        }

        // Scrivo su DB la cancellazione
        await filterProvider.DeleteFilterAsync(filterId, cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Prova la condizione sulle mail già indicizzate (dry-run): nessuna scrittura, nessuna chiamata a Gmail.</summary>
    public async Task<ServiceResult<FilterPreviewDto>> PreviewAsync(FilterPreviewRequest request, CancellationToken cancellationToken)
    {
        if (request.MailboxId is int mailboxId && await mailboxProvider.GetMailboxStatusAsync(mailboxId, cancellationToken) is null)
        {
            return ServiceResult<FilterPreviewDto>.Invalid("mailboxId", ServiceMessages.MailboxNotFound);
        }

        // Leggo da DB i messaggi del mittente (in una casella o in tutte)
        var candidates = request.MailboxId is int id
            ? await messageProvider.GetMessagesBySenderAsync(id, request.SenderAddress!, cancellationToken)
            : await messageProvider.GetMessagesBySenderInAllMailboxesAsync(request.SenderAddress!, cancellationToken);

        var probe = new Filter
        {
            MailboxId = request.MailboxId,
            SenderAddress = request.SenderAddress!.Trim(),
            SubjectMatchType = ParseMatchType(request.SubjectMatchType),
            SubjectValue = string.IsNullOrEmpty(request.SubjectValue) ? null : request.SubjectValue,
            IsEnabled = true,
        };

        var matches = candidates
            .Where(m => filterMatcher.Match(m, [probe]).IsMatch)
            .OrderByDescending(m => m.ReceivedAtUtc)
            .ToList();
        var compile = MessageSummaryDto.Projection.Compile();
        return ServiceResult<FilterPreviewDto>.Ok(new FilterPreviewDto(matches.Count, matches.Take(PreviewMaxMessages).Select(compile).ToList()));
    }

    /// <summary>Riapplica il filtro sulle mail già indicizzate: un giro per casella (D16), solo i processi richiesti (D13).</summary>
    public async Task<ServiceResult<RunsStartedDto>> ReapplyAsync(int filterId, ReapplyRequest request, CancellationToken cancellationToken)
    {
        // Leggo da DB il filtro con i suoi processi
        var filter = await filterProvider.GetFilterWithProcessesAsync(filterId, cancellationToken);
        if (filter is null)
        {
            return ServiceResult<RunsStartedDto>.NotFound(ServiceMessages.FilterNotFound);
        }

        var filterProcesses = filter.Processes.Select(p => p.ProcessType).ToHashSet();
        var requested = request.Processes?.Select(Enum.Parse<ProcessType>).ToList();
        if (requested is not null && requested.Any(p => !filterProcesses.Contains(p)))
        {
            return ServiceResult<RunsStartedDto>.Invalid("processes", ServiceMessages.ProcessNotInFilter);
        }

        List<int> mailboxIds;
        if (filter.MailboxId is int mailboxId)
        {
            var check = await CheckMailboxAsync<RunsStartedDto>(mailboxId, cancellationToken);
            if (check is not null)
            {
                return check;
            }

            mailboxIds = [mailboxId];
        }
        else
        {
            // Leggo da DB le caselle attive: un giro di riapplica per ciascuna
            mailboxIds = await mailboxProvider.GetActiveMailboxIdsAsync(cancellationToken);
        }

        var processes = requested is null ? null : string.Join(RunOrchestrator.ProcessSeparator, requested);
        return await runService.StartRunsAsync(mailboxIds, RunTrigger.Reapply, filterId, processes, cancellationToken);
    }

    /// <summary>Null se la casella è assente o modificabile; Invalid se inesistente, Conflict se eliminata.</summary>
    private async Task<ServiceResult<T>?> CheckMailboxAsync<T>(int? mailboxId, CancellationToken cancellationToken)
    {
        if (mailboxId is not int id)
        {
            return null;
        }

        // Leggo da DB lo stato della casella
        return await mailboxProvider.GetMailboxStatusAsync(id, cancellationToken) switch
        {
            null => ServiceResult<T>.Invalid("mailboxId", ServiceMessages.MailboxNotFound),
            MailboxStatus.Deleted => ServiceResult<T>.Conflict(ServiceMessages.MailboxDeleted),
            _ => null,
        };
    }

    /// <summary>Mappa la richiesta validata nell'entità (i campi obbligatori sono garantiti dal validatore).</summary>
    private static Filter ToEntity(FilterRequest request) => new()
    {
        MailboxId = request.MailboxId,
        Name = request.Name!.Trim(),
        SenderAddress = request.SenderAddress!.Trim().ToLowerInvariant(),
        SubjectMatchType = ParseMatchType(request.SubjectMatchType),
        SubjectValue = string.IsNullOrEmpty(request.SubjectValue) ? null : request.SubjectValue,
        AttachmentSubfolder = string.IsNullOrWhiteSpace(request.AttachmentSubfolder) ? null : request.AttachmentSubfolder.Trim(),
        IsEnabled = request.IsEnabled ?? true,
        Processes = request.Processes!.Select(p => new FilterProcess { ProcessType = Enum.Parse<ProcessType>(p) }).ToList(),
    };

    /// <summary>Tipo di confronto già validato; null se assente.</summary>
    private static SubjectMatchType? ParseMatchType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<SubjectMatchType>(value);

    /// <summary>Proiezione interna minima: Id e casella del filtro, per i controlli D18.</summary>
    private sealed record FilterSummaryWithMailbox(int Id, int? MailboxId)
    {
        public static System.Linq.Expressions.Expression<Func<Filter, FilterSummaryWithMailbox>> Projection =>
            e => new(e.Id, e.MailboxId);
    }
}
