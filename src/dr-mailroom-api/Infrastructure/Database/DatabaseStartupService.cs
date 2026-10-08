using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.EntityFrameworkCore;

namespace dr_mailroom_api.Infrastructure.Database;

/// <summary>Stato del database e inizializzazione resiliente: non rilancia mai, l'API parte anche a DB giù (C7).</summary>
public class DatabaseStartupService(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<MailroomDbContext> contextFactory,
    ILogger<DatabaseStartupService> logger)
{
    /// <summary>Motivo registrato sui giri rimasti in corso dopo un arresto non pulito.</summary>
    public const string InterruptedRunReason = "Giro interrotto da un riavvio del servizio";

    public bool IsDatabaseReady { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LastCheckedUtc { get; private set; }

    /// <summary>Verifica la connessione e chiude i giri rimasti Running (unica inizializzazione). Non rilancia mai.</summary>
    public async Task<bool> TryInitializeAsync(CancellationToken cancellationToken)
    {
        LastCheckedUtc = DateTimeOffset.UtcNow;
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                IsDatabaseReady = false;
                LastError = "Il database non è raggiungibile.";
                logger.LogWarning("Database non raggiungibile durante l'inizializzazione");
                return false;
            }

            // I provider sono scoped: uno scope dedicato per l'inizializzazione
            await using var scope = scopeFactory.CreateAsyncScope();

            // Scrivo su DB la chiusura dei giri interrotti, così la casella non resta bloccata da un Running fantasma
            var closed = await scope.ServiceProvider.GetRequiredService<RunProvider>()
                .FailInterruptedRunsAsync(InterruptedRunReason, cancellationToken);
            if (closed > 0)
            {
                logger.LogWarning("Chiusi {Count} giri rimasti in corso dopo un riavvio", closed);
            }

            IsDatabaseReady = true;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            IsDatabaseReady = false;
            LastError = ex.Message;
            logger.LogError(ex, "Errore durante l'inizializzazione del database");
            return false;
        }
    }

    /// <summary>Probe live per GET status: connessione + lettura reale di una tabella.</summary>
    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        LastCheckedUtc = DateTimeOffset.UtcNow;
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                IsDatabaseReady = false;
                LastError = "Il database non è raggiungibile.";
                return false;
            }

            // Leggo da DB una tabella reale: un DB connettibile ma illeggibile deve risultare non pronto
            await db.Mailboxes.AsNoTracking().AnyAsync(cancellationToken);
            IsDatabaseReady = true;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            IsDatabaseReady = false;
            LastError = ex.Message;
            logger.LogWarning(ex, "Verifica connessione database fallita");
            return false;
        }
    }
}
