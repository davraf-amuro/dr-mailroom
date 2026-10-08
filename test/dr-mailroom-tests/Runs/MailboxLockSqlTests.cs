using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Runs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace dr_mailroom_tests.Runs;

/// <summary>Fact eseguito solo se MAILROOM_TEST_SQL contiene una connection string; altrimenti risulta saltato.</summary>
public sealed class SqlFactAttribute : FactAttribute
{
    /// <summary>Variabile d'ambiente con la connection string del DB di sviluppo (mai in file).</summary>
    public const string ConnectionStringVariable = "MAILROOM_TEST_SQL";

    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Imposta {ConnectionStringVariable} per eseguire i test su SQL Server";
        }
    }
}

/// <summary>Verifica sp_getapplock reale (T8): seconda richiesta sulla stessa casella rifiutata finché la prima non rilascia.</summary>
public class MailboxLockSqlTests
{
    private static MailboxLock NewLock()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{MailroomProviderExtensions.ConnectionStringName}"] =
                    Environment.GetEnvironmentVariable(SqlFactAttribute.ConnectionStringVariable),
            })
            .Build();
        return new MailboxLock(configuration, NullLogger<MailboxLock>.Instance);
    }

    [SqlFact]
    public async Task SecondAcquire_OnBusyMailbox_IsRejected_UntilReleaseAsync()
    {
        var mailboxId = Random.Shared.Next(1_000_000, 2_000_000);
        var mailboxLock = NewLock();

        var first = await mailboxLock.TryAcquireAsync(mailboxId, CancellationToken.None);
        var second = await mailboxLock.TryAcquireAsync(mailboxId, CancellationToken.None);
        var otherMailbox = await mailboxLock.TryAcquireAsync(mailboxId + 1, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.NotNull(otherMailbox);

        await first!.DisposeAsync();
        await otherMailbox!.DisposeAsync();

        var afterRelease = await mailboxLock.TryAcquireAsync(mailboxId, CancellationToken.None);
        Assert.NotNull(afterRelease);
        await afterRelease!.DisposeAsync();
    }
}
