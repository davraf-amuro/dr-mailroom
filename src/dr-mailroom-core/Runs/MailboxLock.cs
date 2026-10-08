using System.Data;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Runs;

/// <summary>
/// Lock per casella su sp_getapplock (T8): risorsa "mailbox:&lt;id&gt;", timeout 0, owner Session su una connessione
/// dedicata tenuta aperta per tutto il giro. Se il processo muore, la connessione cade e SQL Server rilascia il lock.
/// </summary>
public sealed class MailboxLock(IConfiguration configuration, ILogger<MailboxLock> logger) : IMailboxLock
{
    /// <summary>Prefisso della risorsa applicativa del lock.</summary>
    public const string ResourcePrefix = "mailbox:";

    /// <summary>Prova a prendere il lock esclusivo della casella senza attesa.</summary>
    public async Task<IAsyncDisposable?> TryAcquireAsync(int mailboxId, CancellationToken cancellationToken)
    {
        var resource = ResourcePrefix + mailboxId;
        var connection = new SqlConnection(configuration.GetConnectionString(MailroomProviderExtensions.ConnectionStringName));

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", resource);
            command.Parameters.AddWithValue("@LockMode", "Exclusive");
            command.Parameters.AddWithValue("@LockOwner", "Session");
            command.Parameters.AddWithValue("@LockTimeout", 0);
            var result = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
            result.Direction = ParameterDirection.ReturnValue;

            // Chiedo a SQL Server il lock applicativo: >= 0 ottenuto, < 0 occupato o errore
            await command.ExecuteNonQueryAsync(cancellationToken);

            if ((int)result.Value < 0)
            {
                logger.LogInformation("Lock {Resource} occupato (esito {Result})", resource, (int)result.Value);
                await connection.DisposeAsync();
                return null;
            }

            return new Handle(connection, resource, logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Lock acquisito: il dispose lo rilascia esplicitamente e chiude la connessione.</summary>
    private sealed class Handle(SqlConnection connection, string resource, ILogger logger) : IAsyncDisposable
    {
        /// <summary>Rilascia il lock; un errore di rilascio non è grave, la chiusura della sessione lo libera comunque.</summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "sp_releaseapplock";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Resource", resource);
                command.Parameters.AddWithValue("@LockOwner", "Session");

                // Rilascio su SQL Server il lock applicativo
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                logger.LogWarning(ex, "Rilascio del lock {Resource} fallito: lo libera la chiusura della sessione", resource);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
