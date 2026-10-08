using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace dr_mailroom_api.Infrastructure.Database;

/// <summary>Intercetta gli errori di connessione SQL a runtime e risponde 503 ProblemDetails (C7).</summary>
public class DatabaseExceptionHandler : IExceptionHandler
{
    /// <summary>Gestisce solo gli errori di database; gli altri passano al gestore successivo.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsDbConnectionError(exception))
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Base Dati non pronta",
            Detail = "Il database non è al momento raggiungibile.",
            Status = StatusCodes.Status503ServiceUnavailable,
        }, cancellationToken);
        return true;
    }

    /// <summary>True per SqlException o errori di connessione annidati.</summary>
    private static bool IsDbConnectionError(Exception ex) =>
        ex is SqlException
        || (ex is InvalidOperationException && IsSqlRelated(ex))
        || (ex.InnerException is not null && IsDbConnectionError(ex.InnerException));

    /// <summary>Riconosce dal messaggio gli errori di connessione non tipizzati.</summary>
    private static bool IsSqlRelated(Exception ex) =>
        ex.Message.Contains("sql", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("database", StringComparison.OrdinalIgnoreCase);
}
