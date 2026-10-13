using dr_mailroom_core.Enums;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Runs;
using dr_mailroom_tests.Runs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Api;

/// <summary>Dispatcher finto: registra i giri affidati senza eseguirli.</summary>
public sealed class RecordingRunDispatcher : IRunDispatcher
{
    public List<long> Dispatched { get; } = [];

    public Task DispatchAsync(long runId, CancellationToken cancellationToken)
    {
        lock (Dispatched)
        {
            Dispatched.Add(runId);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Autorizzazione OAuth finta: URL fisso con lo scopo, nessuna chiamata a Google.</summary>
public sealed class FakeGmailAuthorizer : IGmailAuthorizer
{
    /// <summary>Indirizzo dell'account "autorizzato" restituito dallo scambio del code.</summary>
    public string ConnectedEmail { get; set; } = "nuova@example.com";

    /// <summary>Scopi degli scambi ricevuti, in ordine.</summary>
    public List<OAuthPurpose> ExchangedPurposes { get; } = [];

    public string BuildAuthorizationUrl(string state, OAuthPurpose purpose) =>
        $"https://accounts.example/consent?purpose={purpose}&state=" + Uri.EscapeDataString(state);

    public Task<ConnectedMailbox> ExchangeCodeAsync(string code, OAuthPurpose purpose, CancellationToken cancellationToken)
    {
        ExchangedPurposes.Add(purpose);
        return Task.FromResult(new ConnectedMailbox(ConnectedEmail, $"refresh-{purpose}-{code}"));
    }
}

/// <summary>Host di test dell'API: ambiente Testing, impostazioni fittizie, DB InMemory, nessun servizio in background.</summary>
public sealed class MailroomApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key";
    public const string ApiKeyHeader = "x-api-key";

    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "dr-mailroom-api-tests", Guid.NewGuid().ToString("N"));
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public RecordingRunDispatcher Dispatcher { get; } = new();

    public FakeMailboxLock Lock { get; } = new();

    public FakeGmailAuthorizer Authorizer { get; } = new();

    /// <summary>Nuovo contesto sul DB InMemory dell'host, per preparare o verificare i dati.</summary>
    public MailroomDbContext NewContext() => new(
        new DbContextOptionsBuilder<MailroomDbContext>().UseInMemoryDatabase(_databaseName).Options,
        NullLoggerFactory.Instance);

    /// <summary>Client HTTP con la chiave API già impostata.</summary>
    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyHeader, ApiKey);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MailroomDb", "Server=non-usato;Database=non-usato");
        builder.UseSetting("Mailroom:Gmail:ClientId", "test-client");
        builder.UseSetting("Mailroom:Gmail:ClientSecret", "test-secret");
        builder.UseSetting("Mailroom:Attachments:RootPath", Path.Combine(_dataRoot, "attachments"));
        builder.UseSetting("Mailroom:DataProtection:KeyRingPath", Path.Combine(_dataRoot, "keys"));
        builder.UseSetting("Authentication:ApiKey:ApiKeyValue", ApiKey);

        builder.ConfigureServices(services =>
        {
            // Sostituisco SQL Server con InMemory: tolgo opzioni, configurazione del provider e factory
            services.RemoveAll<DbContextOptions<MailroomDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MailroomDbContext>>();
            services.RemoveAll<IDbContextFactory<MailroomDbContext>>();
            services.RemoveAll<MailroomDbContext>();
            services.AddDbContextFactory<MailroomDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IHostedService>();
            services.AddSingleton<IMailboxLock>(Lock);
            services.AddSingleton<IRunDispatcher>(Dispatcher);
            services.AddSingleton<IGmailAuthorizer>(Authorizer);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }
}
