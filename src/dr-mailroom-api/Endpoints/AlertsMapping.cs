using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint delle allerte, consumati dal software esterno che avvisa l'utente (D3).</summary>
public static class AlertsMapping
{
    /// <summary>Gruppo api/v1/alerts.</summary>
    public static IEndpointRouteBuilder MapAlertsEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        var group = routes.MapGroup("api/v{version:apiVersion}/alerts")
            .WithTags("Alerts")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .RequireAuthorization();

        group.MapGet("/", GetListHandlerAsync)
            .Produces<List<AlertDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetAlertList")
            .WithSummary("Elenco allerte")
            .WithDescription("Allerte dalla più vecchia; consumed=false restituisce quelle da notificare. 404 se nessuna corrisponde.");

        group.MapGet("/summary", GetSummaryHandlerAsync)
            .Produces<List<AlertSummaryDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetAlertSummary")
            .WithSummary("Elenco allerte ridotto")
            .WithDescription("Id, mittente, oggetto e ricezione delle allerte. 404 se nessuna corrisponde.");

        group.MapPost("/{id:long}/consume", ConsumeHandlerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("PostAlertConsume")
            .WithSummary("Consuma allerta")
            .WithDescription("Marca l'allerta come notificata (ConsumedAtUtc). Idempotente.");

        return routes;
    }

    /// <summary>Elenco completo, filtro validato prima della query.</summary>
    private static async Task<IResult> GetListHandlerAsync(
        [AsParameters] AlertListFilter filter, IValidator<AlertListFilter> validator, AlertService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetAllAsync(filter, cancellationToken));
    }

    /// <summary>Elenco ridotto.</summary>
    private static async Task<IResult> GetSummaryHandlerAsync(
        [AsParameters] AlertListFilter filter, IValidator<AlertListFilter> validator, AlertService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetSummariesAsync(filter, cancellationToken));
    }

    /// <summary>Consumo dell'allerta.</summary>
    private static async Task<IResult> ConsumeHandlerAsync(long id, AlertService service, CancellationToken cancellationToken)
    {
        var result = await service.ConsumeAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.NoContent() : EndpointResults.Failure(result);
    }
}
