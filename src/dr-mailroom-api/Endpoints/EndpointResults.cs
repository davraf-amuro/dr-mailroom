using dr_mailroom_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Traduzione uniforme di esiti e liste vuote in ProblemDetails.</summary>
public static class EndpointResults
{
    /// <summary>404 ProblemDetails per una lista senza risultati (regola 9).</summary>
    public static IResult NoData() => TypedResults.Problem(new ProblemDetails
    {
        Title = "Data Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = "No data for specified filters.",
    });

    /// <summary>404 ProblemDetails per una risorsa inesistente.</summary>
    public static IResult NotFound(string detail) => TypedResults.Problem(new ProblemDetails
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
    });

    /// <summary>Esito non Ok del servizio: 400 ValidationProblem, 404 o 409 ProblemDetails.</summary>
    public static IResult Failure<T>(ServiceResult<T> result) => result.Outcome switch
    {
        ServiceOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
        ServiceOutcome.NotFound => NotFound(result.Message!),
        ServiceOutcome.Conflict => TypedResults.Problem(new ProblemDetails
        {
            Title = "Conflict",
            Status = StatusCodes.Status409Conflict,
            Detail = result.Message,
        }),
        _ => throw new InvalidOperationException("Esito Ok passato a Failure"),
    };

    /// <summary>Lista: 200 con i dati oppure 404 se vuota.</summary>
    public static IResult ListOrNoData<T>(List<T> items) => items.Count == 0 ? NoData() : TypedResults.Ok(items);
}
