using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint dei giri: avvio manuale immediato (D11) e cronologia.</summary>
public static class RunsMapping
{
    /// <summary>Gruppo api/v1/runs.</summary>
    public static IEndpointRouteBuilder MapRunsEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        var group = routes.MapGroup("api/v{version:apiVersion}/runs")
            .WithTags("Runs")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .RequireAuthorization();

        group.MapPost("/", PostHandlerAsync)
            .Produces<RunsStartedDto>(StatusCodes.Status202Accepted)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostRun")
            .WithSummary("Avvia giro manuale")
            .WithDescription("Avvia subito un giro su una casella o, senza mailboxId, su tutte le caselle attive. 409 se la casella (o tutte) ha già un giro in corso.");

        group.MapPost("/cancel", CancelAllHandlerAsync)
            .Produces<RunsCancelledDto>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostRunCancelAll")
            .WithSummary("Annulla tutti i giri in corso")
            .WithDescription("Chiede l'annullamento di tutti i giri Running. Il lavoro si ferma entro pochi secondi e i giri si chiudono Cancelled (da verificare con GET /runs). Non mette in pausa le caselle: il cron ne avvierà di nuovi. 409 se nessun giro è in corso.");

        group.MapPost("/{id:long}/cancel", CancelHandlerAsync)
            .Produces<RunsCancelledDto>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostRunCancel")
            .WithSummary("Annulla un giro in corso")
            .WithDescription("Chiede l'annullamento del giro. Il lavoro si ferma entro pochi secondi e il giro si chiude Cancelled; il backfill riprende dalla pagina salvata al giro successivo. 404 se il giro non esiste, 409 se non è in corso.");

        group.MapGet("/", GetListHandlerAsync)
            .Produces<List<RunDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetRunList")
            .WithSummary("Cronologia giri")
            .WithDescription("Giri dal più recente, con contatori ed esito. 404 se nessuno corrisponde ai filtri.");

        group.MapGet("/summary", GetSummaryHandlerAsync)
            .Produces<List<RunSummaryDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetRunSummary")
            .WithSummary("Cronologia giri ridotta")
            .WithDescription("Id, casella, origine, stato e inizio dei giri. 404 se nessuno corrisponde.");

        group.MapGet("/{id:long}", GetByIdHandlerAsync)
            .Produces<RunDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetRunById")
            .WithSummary("Dettaglio giro")
            .WithDescription("Stato e contatori del giro: da interrogare in polling dopo l'avvio.");

        return routes;
    }

    /// <summary>Avvio manuale: body facoltativo.</summary>
    private static async Task<IResult> PostHandlerAsync(
        StartRunRequest? request, IValidator<StartRunRequest> validator, RunService service, CancellationToken cancellationToken)
    {
        var body = request ?? new StartRunRequest(null);
        var validation = validator.Validate(body);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.StartManualAsync(body, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Accepted((string?)null, result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>Annullamento di un giro: 202 accettato, la chiusura Cancelled è asincrona.</summary>
    private static async Task<IResult> CancelHandlerAsync(long id, RunService service, CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Accepted((string?)null, result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>Annullamento di tutti i giri in corso.</summary>
    private static async Task<IResult> CancelAllHandlerAsync(RunService service, CancellationToken cancellationToken)
    {
        var result = await service.CancelAllAsync(cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Accepted((string?)null, result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>Cronologia completa, filtro validato prima della query.</summary>
    private static async Task<IResult> GetListHandlerAsync(
        [AsParameters] RunFilter filter, IValidator<RunFilter> validator, RunService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetAllAsync(filter, cancellationToken));
    }

    /// <summary>Cronologia ridotta.</summary>
    private static async Task<IResult> GetSummaryHandlerAsync(
        [AsParameters] RunFilter filter, IValidator<RunFilter> validator, RunService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetSummariesAsync(filter, cancellationToken));
    }

    /// <summary>Un giro o 404.</summary>
    private static async Task<IResult> GetByIdHandlerAsync(long id, RunService service, CancellationToken cancellationToken) =>
        await service.GetByIdAsync(id, cancellationToken) is { } run
            ? TypedResults.Ok(run)
            : EndpointResults.NotFound(ServiceMessages.RunNotFound);
}
