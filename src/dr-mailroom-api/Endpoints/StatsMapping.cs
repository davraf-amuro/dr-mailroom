using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint delle statistiche per mittente (T5, D9).</summary>
public static class StatsMapping
{
    /// <summary>Gruppo api/v1/stats.</summary>
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        var group = routes.MapGroup("api/v{version:apiVersion}/stats")
            .WithTags("Stats")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .RequireAuthorization();

        group.MapGet("/senders", GetSendersHandlerAsync)
            .Produces<List<SenderStatDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetSenderStatList")
            .WithSummary("Mail per mittente")
            .WithDescription("Conteggio assoluto delle mail lette per mittente e casella, dal più attivo. 404 se vuoto.");

        group.MapGet("/senders/monthly", GetMonthlyHandlerAsync)
            .Produces<List<SenderMonthlyStatDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetSenderMonthlyStatList")
            .WithSummary("Mail per mittente e mese")
            .WithDescription("Conteggio per mittente, casella e mese (YearMonth = yyyyMM). 404 se vuoto.");

        return routes;
    }

    /// <summary>Totali per mittente.</summary>
    private static async Task<IResult> GetSendersHandlerAsync(
        [AsParameters] SenderStatFilter filter, IValidator<SenderStatFilter> validator, StatsService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetSenderStatsAsync(filter, cancellationToken));
    }

    /// <summary>Conteggi mensili per mittente.</summary>
    private static async Task<IResult> GetMonthlyHandlerAsync(
        [AsParameters] SenderMonthlyStatFilter filter, IValidator<SenderMonthlyStatFilter> validator, StatsService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        return EndpointResults.ListOrNoData(await service.GetSenderMonthlyStatsAsync(filter, cancellationToken));
    }
}
