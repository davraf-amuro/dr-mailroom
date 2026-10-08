using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Enums;
using dr_mailroom_core.Filtering;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;
using dr_mailroom_core.Processes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dr_mailroom_core.Runs;

/// <summary>
/// Coda FIFO di elaborazione: un produttore scrive gli id dei messaggi, MaxParallelMessages consumatori li elaborano
/// in parallelo (filtri e processi). Ogni consumatore ha il proprio scope DI, perché il DbContext non è thread-safe.
/// Errore del produttore → i consumatori finiscono i messaggi già in coda, poi l'errore risale.
/// Errore fatale di un consumatore (es. token revocato) → si annulla tutto e l'errore risale.
/// </summary>
public class MessageProcessingQueue(
    IServiceScopeFactory scopeFactory,
    FilterMatcher filterMatcher,
    IOptions<ProcessingOptions> processingOptions,
    ILogger<MessageProcessingQueue> logger)
{
    /// <summary>Posti in coda per consumatore: oltre, il produttore attende (contropressione).</summary>
    public const int CapacityPerConsumer = 4;

    /// <summary>Esegue il produttore e i consumatori; restituisce i contatori dei messaggi elaborati.</summary>
    public async Task<QueueOutcome> RunAsync(
        IGmailSession session,
        IReadOnlyList<Filter> filters,
        IReadOnlySet<ProcessType>? allowed,
        Func<ChannelWriter<long>, CancellationToken, Task> producer,
        CancellationToken cancellationToken)
    {
        var consumers = processingOptions.Value.MaxParallelMessages;
        var channel = Channel.CreateBounded<long>(new BoundedChannelOptions(consumers * CapacityPerConsumer)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = consumers == 1,
        });

        using var state = new QueueState(cancellationToken);
        var consumerTasks = Enumerable.Range(0, consumers)
            .Select(_ => ConsumeAsync(channel.Reader, session, filters, allowed, state))
            .ToArray();

        Exception? producerError = null;
        try
        {
            await producer(channel.Writer, state.Token);
        }
        catch (Exception ex)
        {
            producerError = ex;
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await Task.WhenAll(consumerTasks);

        // L'errore di un consumatore viene prima: il produttore in quel caso fallisce solo per l'annullamento
        if (state.Fatal is not null)
        {
            ExceptionDispatchInfo.Throw(state.Fatal);
        }

        if (producerError is not null)
        {
            ExceptionDispatchInfo.Throw(producerError);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new QueueOutcome(state.Processed, state.Errors);
    }

    /// <summary>Consumatore: legge gli id in ordine FIFO, valuta i filtri e applica i processi nel proprio scope.</summary>
    private async Task ConsumeAsync(
        ChannelReader<long> reader,
        IGmailSession session,
        IReadOnlyList<Filter> filters,
        IReadOnlySet<ProcessType>? allowed,
        QueueState state)
    {
        // Cedo subito il controllo: il produttore parte senza aspettare l'avvio dei consumatori
        await Task.Yield();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var messageProvider = scope.ServiceProvider.GetRequiredService<MessageProvider>();
            var mailboxProvider = scope.ServiceProvider.GetRequiredService<MailboxProvider>();
            var processesByType = scope.ServiceProvider.GetServices<IMessageProcess>().ToDictionary(p => p.Type);

            await foreach (var messageId in reader.ReadAllAsync(state.Token))
            {
                // Leggo da DB il messaggio da elaborare
                var message = (await messageProvider.GetMessagesByIdsAsync([messageId], state.Token)).SingleOrDefault();
                if (message is null)
                {
                    continue;
                }

                var match = filterMatcher.Match(message, filters);
                var toApply = match.Processes.Where(p => allowed is null || allowed.Contains(p)).Order().ToList();
                if (toApply.Count > 0)
                {
                    var failed = await ApplyProcessesAsync(processesByType, mailboxProvider, new ProcessContext(session, message, match), toApply, state.Token);
                    state.CountProcessed(failed);
                }

                // Ogni messaggio è chiuso e salvato: svuoto il tracker per non accumulare entità
                mailboxProvider.DiscardPendingChanges();
            }
        }
        catch (OperationCanceledException) when (state.Token.IsCancellationRequested)
        {
            // Annullamento del giro o errore fatale di un altro consumatore: esco senza altro
        }
        catch (Exception ex)
        {
            state.Fail(ex);
        }
    }

    /// <summary>Applica i processi al messaggio; true se almeno uno è fallito.</summary>
    private async Task<bool> ApplyProcessesAsync(
        Dictionary<ProcessType, IMessageProcess> processesByType,
        MailboxProvider mailboxProvider,
        ProcessContext context,
        IReadOnlyList<ProcessType> toApply,
        CancellationToken cancellationToken)
    {
        var failed = false;
        foreach (var processType in toApply)
        {
            try
            {
                var status = await processesByType[processType].ExecuteAsync(context, cancellationToken);
                failed |= status == ProcessExecutionStatus.Failed;
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TokenRevokedException)
            {
                // Errore imprevisto del singolo processo: scarto le modifiche pendenti e passo al successivo
                logger.LogError(ex, "Processo {ProcessType} sul messaggio {MessageId} interrotto", processType, context.Message.Id);
                mailboxProvider.DiscardPendingChanges();
                failed = true;
            }
        }

        return failed;
    }

    /// <summary>Stato condiviso dai consumatori: contatori, primo errore fatale, annullamento comune.</summary>
    private sealed class QueueState(CancellationToken cancellationToken) : IDisposable
    {
        private readonly CancellationTokenSource _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        private int _processed;
        private int _errors;
        private Exception? _fatal;

        public CancellationToken Token => _cts.Token;

        public int Processed => Volatile.Read(ref _processed);

        public int Errors => Volatile.Read(ref _errors);

        public Exception? Fatal => Volatile.Read(ref _fatal);

        /// <summary>Conta un messaggio elaborato, e un errore se almeno un processo è fallito.</summary>
        public void CountProcessed(bool failed)
        {
            Interlocked.Increment(ref _processed);
            if (failed)
            {
                Interlocked.Increment(ref _errors);
            }
        }

        /// <summary>Registra il primo errore fatale e ferma produttore e consumatori.</summary>
        public void Fail(Exception ex)
        {
            Interlocked.CompareExchange(ref _fatal, ex, null);
            _cts.Cancel();
        }

        public void Dispose() => _cts.Dispose();
    }
}

/// <summary>Esito della coda: messaggi elaborati (almeno un processo applicato) e messaggi con processi falliti.</summary>
public sealed record QueueOutcome(int Processed, int Errors);
