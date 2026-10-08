using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Infrastructure.Database;
using dr_mailroom_api.Properties;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Salute del servizio: /health per l'infrastruttura, status e retry del DB anonimi (C7).</summary>
public static class HealthMapping
{
    /// <summary>/health fuori da Scalar; api/v1/status sempre raggiungibile, anche a DB giù.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        routes.MapHealthChecks("/health").AllowAnonymous();

        var group = routes.MapGroup("api/v{version:apiVersion}/status")
            .WithTags("Status")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .AllowAnonymous();

        group.MapGet("/", GetStatusHandlerAsync)
            .Produces<DatabaseStatusResponse>(StatusCodes.Status200OK)
            .WithName("GetStatus")
            .WithSummary("Stato del database")
            .WithDescription("Verifica live la raggiungibilità del database; risponde sempre 200.");

        group.MapPost("/retry-database", RetryDatabaseHandlerAsync)
            .Produces<DatabaseStatusResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)
            .WithName("PostRetryDatabase")
            .WithSummary("Ritenta connessione al database")
            .WithDescription("Ritenta connessione e inizializzazione; 200 se pronto, 503 se ancora giù.");

        return routes;
    }

    /// <summary>Stato live del DB.</summary>
    private static async Task<IResult> GetStatusHandlerAsync(DatabaseStartupService database, CancellationToken cancellationToken)
    {
        await database.CheckConnectionAsync(cancellationToken);
        return TypedResults.Ok(new DatabaseStatusResponse(database.IsDatabaseReady, database.LastError, database.LastCheckedUtc));
    }

    /// <summary>Nuovo tentativo di inizializzazione.</summary>
    private static async Task<IResult> RetryDatabaseHandlerAsync(DatabaseStartupService database, CancellationToken cancellationToken) =>
        await database.TryInitializeAsync(cancellationToken)
            ? TypedResults.Ok(new DatabaseStatusResponse(true, null, database.LastCheckedUtc))
            : TypedResults.Problem(new ProblemDetails
            {
                Title = "Base Dati non pronta",
                Detail = database.LastError ?? "Il database non è al momento raggiungibile.",
                Status = StatusCodes.Status503ServiceUnavailable,
            });
}
