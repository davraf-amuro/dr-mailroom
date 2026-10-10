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

        group.MapGet("/senders/overview", GetOverviewHandlerAsync)
            .Produces<SenderOverviewPageDto>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetSenderOverview")
            .WithSummary("Panoramica mittenti di una casella")
            .WithDescription("Per indirizzo email (mai per nome): numero di mail, data e oggetto della più vecchia e della più recente. Paginata (pageSize 1–100, default 25), ordinabile (sortBy MailCount|LastReceivedAt|FirstReceivedAt|SenderAddress, sortDir Asc|Desc; default MailCount Desc), ricerca nell'indirizzo, year facoltativo. 404 se la casella non esiste; nessun mittente → 200 con totalCount 0.");

        group.MapGet("/years", GetYearsHandlerAsync)
            .Produces<List<YearStatDto>>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetYearStatList")
            .WithSummary("Mail per anno")
            .WithDescription("Mail ricevute per anno, dal più recente, della casella o di un suo mittente (senderAddress confrontato per uguaglianza). 404 se la casella non esiste; nessuna mail → 200 con lista vuota.");

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

    /// <summary>Panoramica paginata dei mittenti.</summary>
    private static async Task<IResult> GetOverviewHandlerAsync(
        [AsParameters] SenderOverviewFilter filter, IValidator<SenderOverviewFilter> validator, StatsService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.GetSenderOverviewAsync(filter, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Ok(result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>Mail per anno.</summary>
    private static async Task<IResult> GetYearsHandlerAsync(
        [AsParameters] YearStatFilter filter, IValidator<YearStatFilter> validator, StatsService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.GetYearStatsAsync(filter, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Ok(result.Value) : EndpointResults.Failure(result);
    }
}
