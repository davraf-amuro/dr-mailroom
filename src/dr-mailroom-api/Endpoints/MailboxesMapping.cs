using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint delle caselle: elenco, stato, collegamento OAuth, eliminazione logica.</summary>
public static class MailboxesMapping
{
    /// <summary>Gruppo api/v1/mailboxes; il callback OAuth è anonimo perché lo chiama il browser di ritorno da Google.</summary>
    public static IEndpointRouteBuilder MapMailboxesEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        var group = routes.MapGroup("api/v{version:apiVersion}/mailboxes")
            .WithTags("Mailboxes")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .RequireAuthorization();

        group.MapGet("/", GetListHandlerAsync)
            .Produces<List<MailboxDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetMailboxList")
            .WithSummary("Elenco caselle")
            .WithDescription("Caselle con stato, collegamento, avanzamento del backfill ed eventuale errore. 404 se nessuna corrisponde ai filtri.");

        group.MapGet("/summary", GetSummaryHandlerAsync)
            .Produces<List<MailboxSummaryDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetMailboxSummary")
            .WithSummary("Elenco caselle ridotto")
            .WithDescription("Id, indirizzo e stato delle caselle. 404 se nessuna corrisponde ai filtri.");

        group.MapGet("/{id:int}", GetByIdHandlerAsync)
            .Produces<MailboxDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetMailboxById")
            .WithSummary("Dettaglio casella")
            .WithDescription("Stato della casella: token valido, backfill, ultimo giro, ultimo errore.");

        group.MapGet("/connect", GetConnectUrlHandler)
            .Produces<ConnectUrlDto>(StatusCodes.Status200OK)
            .WithName("GetMailboxConnectUrl")
            .WithSummary("URL di collegamento Gmail")
            .WithDescription("URL della pagina di consenso Google (sola lettura). Il frontend vi porta il browser; Google torna sul callback.");

        group.MapGet("/connect/callback", CallbackHandlerAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found)
            .WithName("GetMailboxConnectCallback")
            .WithSummary("Callback OAuth Google")
            .WithDescription("Riceve code e state da Google, salva il refresh token cifrato e reindirizza al frontend con l'esito in query string (oauth=ok|error).");

        group.MapPost("/{id:int}/pause", PauseHandlerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostMailboxPause")
            .WithSummary("Mette in pausa la casella")
            .WithDescription("Stato Disabled: nessun giro (cron, manuale, riapplica) finché non si riprende, anche dopo un riavvio. Annulla i giri in corso della casella. Idempotente. 404 se non esiste, 409 se eliminata o con token revocato.");

        group.MapPost("/{id:int}/resume", ResumeHandlerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostMailboxResume")
            .WithSummary("Riprende la casella in pausa")
            .WithDescription("Stato Disabled → Active: la casella torna nei giri dal prossimo scatto del cron; il backfill riprende dalla pagina salvata. Idempotente su Active. 404 se non esiste, 409 se non è in pausa.");

        group.MapDelete("/{id:int}", DeleteHandlerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("DeleteMailbox")
            .WithSummary("Elimina casella")
            .WithDescription("Eliminazione logica: stato Deleted e token rimosso; statistiche e storico restano. 409 se un giro è in corso.");

        return routes;
    }

    /// <summary>Elenco completo, filtro validato prima della query.</summary>
    private static async Task<IResult> GetListHandlerAsync(
        [AsParameters] MailboxFilter filter, IValidator<MailboxFilter> validator, MailboxService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetAllAsync(filter, cancellationToken));
    }

    /// <summary>Elenco ridotto, stesso filtro.</summary>
    private static async Task<IResult> GetSummaryHandlerAsync(
        [AsParameters] MailboxFilter filter, IValidator<MailboxFilter> validator, MailboxService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetSummariesAsync(filter, cancellationToken));
    }

    /// <summary>Una casella o 404.</summary>
    private static async Task<IResult> GetByIdHandlerAsync(int id, MailboxService service, CancellationToken cancellationToken) =>
        await service.GetByIdAsync(id, cancellationToken) is { } mailbox
            ? TypedResults.Ok(mailbox)
            : EndpointResults.NotFound(ServiceMessages.MailboxNotFound);

    /// <summary>URL di consenso con state firmato.</summary>
    private static IResult GetConnectUrlHandler(MailboxService service) => TypedResults.Ok(service.GetConnectUrl());

    /// <summary>Ritorno da Google: sempre redirect al frontend, mai dati sensibili in risposta.</summary>
    private static async Task<IResult> CallbackHandlerAsync(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, MailboxService service, CancellationToken cancellationToken) =>
        TypedResults.Redirect(await service.HandleCallbackAsync(code, state, error, cancellationToken));

    /// <summary>Pausa della casella.</summary>
    private static async Task<IResult> PauseHandlerAsync(int id, MailboxService service, CancellationToken cancellationToken)
    {
        var result = await service.PauseAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.NoContent() : EndpointResults.Failure(result);
    }

    /// <summary>Ripresa della casella.</summary>
    private static async Task<IResult> ResumeHandlerAsync(int id, MailboxService service, CancellationToken cancellationToken)
    {
        var result = await service.ResumeAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.NoContent() : EndpointResults.Failure(result);
    }

    /// <summary>Eliminazione logica (D18).</summary>
    private static async Task<IResult> DeleteHandlerAsync(int id, MailboxService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.NoContent() : EndpointResults.Failure(result);
    }
}
