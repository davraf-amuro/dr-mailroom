using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint dei filtri: CRUD, anteprima sulle mail indicizzate, riapplica (D13).</summary>
public static class FiltersMapping
{
    /// <summary>Gruppo api/v1/filters.</summary>
    public static IEndpointRouteBuilder MapFiltersEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        var group = routes.MapGroup("api/v{version:apiVersion}/filters")
            .WithTags("Filters")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .RequireAuthorization();

        group.MapGet("/", GetListHandlerAsync)
            .Produces<List<FilterDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetFilterList")
            .WithSummary("Elenco filtri")
            .WithDescription("Filtri con condizione sull'oggetto e processi. 404 se nessuno corrisponde ai filtri di ricerca.");

        group.MapGet("/summary", GetSummaryHandlerAsync)
            .Produces<List<FilterSummaryDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetFilterSummary")
            .WithSummary("Elenco filtri ridotto")
            .WithDescription("Id, nome, mittente e stato dei filtri. 404 se nessuno corrisponde.");

        group.MapGet("/{id:int}", GetByIdHandlerAsync)
            .Produces<FilterDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetFilterById")
            .WithSummary("Dettaglio filtro")
            .WithDescription("Filtro completo con i processi associati.");

        group.MapPost("/", PostHandlerAsync)
            .Produces<FilterDto>(StatusCodes.Status201Created)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostFilter")
            .WithSummary("Crea filtro")
            .WithDescription("Crea un filtro: mittente obbligatorio, al più una condizione sull'oggetto (StartsWith, Contains, Regex), almeno un processo. 409 se la casella è eliminata.");

        group.MapPut("/{id:int}", PutHandlerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PutFilter")
            .WithSummary("Modifica filtro")
            .WithDescription("Sostituisce campi e processi del filtro. 409 se la casella è eliminata.");

        group.MapDelete("/{id:int}", DeleteHandlerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("DeleteFilter")
            .WithSummary("Cancella filtro")
            .WithDescription("Cancella il filtro; le allerte già create restano. 409 se la casella è eliminata.");

        group.MapPost("/preview", PreviewHandlerAsync)
            .Produces<FilterPreviewDto>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .WithName("PostFilterPreview")
            .WithSummary("Anteprima filtro")
            .WithDescription($"Prova la condizione sulle mail già indicizzate senza scrivere nulla: totale e le {FilterService.PreviewMaxMessages} più recenti.");

        group.MapPost("/{id:int}/reapply", ReapplyHandlerAsync)
            .Produces<RunsStartedDto>(StatusCodes.Status202Accepted)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostFilterReapply")
            .WithSummary("Riapplica filtro")
            .WithDescription("Lavora le mail già indicizzate che corrispondono al filtro, con un sottoinsieme facoltativo dei suoi processi. Un giro per casella; 409 se tutte le caselle sono occupate.");

        return routes;
    }

    /// <summary>Elenco completo, filtro di ricerca validato prima della query.</summary>
    private static async Task<IResult> GetListHandlerAsync(
        [AsParameters] FilterListFilter filter, IValidator<FilterListFilter> validator, FilterService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetAllAsync(filter, cancellationToken));
    }

    /// <summary>Elenco ridotto, stesso filtro di ricerca.</summary>
    private static async Task<IResult> GetSummaryHandlerAsync(
        [AsParameters] FilterListFilter filter, IValidator<FilterListFilter> validator, FilterService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetSummariesAsync(filter, cancellationToken));
    }

    /// <summary>Un filtro o 404.</summary>
    private static async Task<IResult> GetByIdHandlerAsync(int id, FilterService service, CancellationToken cancellationToken) =>
        await service.GetByIdAsync(id, cancellationToken) is { } filter
            ? TypedResults.Ok(filter)
            : EndpointResults.NotFound(ServiceMessages.FilterNotFound);

    /// <summary>Creazione: validazione del body, poi controlli sulla casella nel servizio.</summary>
    private static async Task<IResult> PostHandlerAsync(
        FilterRequest request, IValidator<FilterRequest> validator, FilterService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.CreateAsync(request, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok
            ? TypedResults.Created($"/api/v1/filters/{result.Value!.Id}", result.Value)
            : EndpointResults.Failure(result);
    }

    /// <summary>Modifica: stesso validatore della creazione.</summary>
    private static async Task<IResult> PutHandlerAsync(
        int id, FilterRequest request, IValidator<FilterRequest> validator, FilterService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.NoContent() : EndpointResults.Failure(result);
    }

    /// <summary>Cancellazione del filtro.</summary>
    private static async Task<IResult> DeleteHandlerAsync(int id, FilterService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.NoContent() : EndpointResults.Failure(result);
    }

    /// <summary>Anteprima dry-run.</summary>
    private static async Task<IResult> PreviewHandlerAsync(
        FilterPreviewRequest request, IValidator<FilterPreviewRequest> validator, FilterService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.PreviewAsync(request, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Ok(result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>Riapplica: body facoltativo, processi ⊆ processi del filtro.</summary>
    private static async Task<IResult> ReapplyHandlerAsync(
        int id, ReapplyRequest? request, IValidator<ReapplyRequest> validator, FilterService service, CancellationToken cancellationToken)
    {
        var body = request ?? new ReapplyRequest(null);
        var validation = validator.Validate(body);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.ReapplyAsync(id, body, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Accepted((string?)null, result.Value) : EndpointResults.Failure(result);
    }
}
