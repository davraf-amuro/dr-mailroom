using dr_mailroom_core;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Runs;
using dr_mailroom_core.Security;
using dr_mailroom_tests.Infrastructure;
using dr_mailroom_tests.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace dr_mailroom_tests.Runs;

/// <summary>Lock finto in memoria: caselle occupate a piacere, stessa semantica "senza attesa" di sp_getapplock.</summary>
public sealed class FakeMailboxLock : IMailboxLock
{
    private readonly HashSet<int> _held = [];

    /// <summary>Simula un giro in corso altrove sulla casella.</summary>
    public void Occupy(int mailboxId)
    {
        lock (_held)
        {
            _held.Add(mailboxId);
        }
    }

    public Task<IAsyncDisposable?> TryAcquireAsync(int mailboxId, CancellationToken cancellationToken)
    {
        lock (_held)
        {
            return Task.FromResult<IAsyncDisposable?>(_held.Add(mailboxId) ? new Release(this, mailboxId) : null);
        }
    }

    private sealed class Release(FakeMailboxLock owner, int mailboxId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            lock (owner._held)
            {
                owner._held.Remove(mailboxId);
            }

            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Composizione DI reale di dr-mailroom con DB InMemory, Gmail finto, lock finto e cartella allegati temporanea.</summary>
public sealed class RunTestHarness : IAsyncDisposable
{
    public InMemoryMailroomDb Db { get; } = new();

    public FakeMultiMailboxReader Gmail { get; } = new();

    public FakeGmailTrasher Trasher { get; } = new();

    public FakeMailboxLock Lock { get; } = new();

    public string AttachmentsRoot { get; } = Path.Combine(Path.GetTempPath(), "dr-mailroom-tests", Guid.NewGuid().ToString("N"));

    public ServiceProvider Services { get; }

    public RunTestHarness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Options.Create(new ProcessingOptions { MaxParallelMailboxes = 2, MaxParallelMessages = 2 }));
        services.AddSingleton(Options.Create(new FilteringOptions { RegexTimeoutMs = 250 }));
        services.AddSingleton(Options.Create(new AttachmentsOptions { RootPath = AttachmentsRoot, MaxSizeBytes = 1_000_000 }));
        services.AddSingleton<IDbContextFactory<MailroomDbContext>>(Db);
        services.AddScoped(_ => Db.CreateDbContext());

        services.AddMailroomCore();

        // Sostituzioni: le ultime registrazioni vincono
        services.AddSingleton<IMailboxLock>(Lock);
        services.AddSingleton<IGmailReader>(Gmail);
        services.AddSingleton<IGmailTrasher>(Trasher);
        services.AddSingleton<ITokenProtector, PlainTokenProtector>();

        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Crea una casella collegata con la sua sessione Gmail finta.</summary>
    public async Task<FakeGmailSession> AddMailboxAsync(int mailboxId, bool backfillCompleted = false, ulong? historyId = null)
    {
        var session = new FakeGmailSession();
        Gmail.Sessions["token-" + mailboxId] = session;

        await using var db = Db.CreateDbContext();
        db.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId,
            EmailAddress = $"casella{mailboxId}@example.com",
            EncryptedRefreshToken = "token-" + mailboxId,
            BackfillCompleted = backfillCompleted,
            HistoryId = historyId,
        });
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>Crea un filtro sul mittente delle sessioni finte con i processi indicati.</summary>
    public async Task<int> AddFilterAsync(int filterId, int? mailboxId, params ProcessType[] processes)
    {
        await using var db = Db.CreateDbContext();
        db.Filters.Add(new Filter
        {
            Id = filterId,
            MailboxId = mailboxId,
            Name = "filtro " + filterId,
            SenderAddress = "fornitore@example.com",
            AttachmentSubfolder = "fornitore",
            Processes = processes.Select(p => new FilterProcess { FilterId = filterId, ProcessType = p }).ToList(),
        });
        await db.SaveChangesAsync();
        return filterId;
    }

    /// <summary>Crea un giro come farebbe l'API ed eseguilo come farebbe il job on-demand.</summary>
    public async Task<Run> RunAsync(
        int mailboxId, RunTrigger trigger, int? filterId = null, string? processes = null, string? senderAddress = null,
        DateTime? trashReceivedBeforeUtc = null, bool trashWithoutAttachmentsOnly = false)
    {
        long runId;
        await using (var scope = Services.CreateAsyncScope())
        {
            runId = await scope.ServiceProvider.GetRequiredService<RunProvider>()
                .CreateRunAsync(mailboxId, trigger, filterId, processes, senderAddress, CancellationToken.None, trashReceivedBeforeUtc, trashWithoutAttachmentsOnly);
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RunOrchestrator>().ExecuteAsync(runId, CancellationToken.None);
        }

        await using var db = Db.CreateDbContext();
        return await db.Runs.SingleAsync(r => r.Id == runId);
    }

    /// <summary>File presenti sotto la cartella allegati.</summary>
    public string[] FilesOnDisk() =>
        Directory.Exists(AttachmentsRoot) ? Directory.GetFiles(AttachmentsRoot, "*", SearchOption.AllDirectories) : [];

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        if (Directory.Exists(AttachmentsRoot))
        {
            Directory.Delete(AttachmentsRoot, recursive: true);
        }
    }
}
