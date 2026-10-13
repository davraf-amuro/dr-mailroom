using System.Net;
using System.Threading.RateLimiting;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;

namespace dr_mailroom_core.Gmail;

/// <summary>
/// Esecuzione delle chiamate Gmail di una sessione: quota al minuto rispettata, retry su quota superata e 5xx (T9),
/// invalid_grant → TokenRevokedException. Condivisa da lettura e cestino, ognuno con la propria istanza.
/// </summary>
public sealed class GmailCallExecutor(GmailRetrySettings retry, int quotaUnitsPerMinute, ILogger logger) : IDisposable
{
    /// <summary>Gettoni = unità di quota, riforniti ogni secondo senza burst; le chiamate in eccesso attendono in coda.</summary>
    private readonly TokenBucketRateLimiter _quotaLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = quotaUnitsPerMinute / 60,
        TokensPerPeriod = quotaUnitsPerMinute / 60,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = int.MaxValue,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });

    /// <summary>Esegue una chiamata Gmail: attende le unità di quota; invalid_grant → TokenRevokedException; quota superata e 5xx → ritenta con backoff esponenziale.</summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> call, int cost, string operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Ogni tentativo consuma quota: attendo i gettoni prima di chiamare
            using var lease = await _quotaLimiter.AcquireAsync(cost, cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new InvalidOperationException($"Gmail {operation}: quota locale non acquisita");
            }

            try
            {
                return await call(cancellationToken);
            }
            catch (TokenResponseException ex) when (ex.Error?.Error == GmailConstants.InvalidGrant)
            {
                throw new TokenRevokedException(ex);
            }
            catch (GoogleApiException ex) when (IsRateLimited(ex) && attempt < retry.MaxAttempts)
            {
                var delay = retry.RateLimitBaseDelay * Math.Pow(2, attempt - 1);
                // Reason e messaggio di Google indicano quale quota è scattata (utente, progetto, al minuto o al secondo)
                var reasons = string.Join(',', (ex.Error?.Errors ?? []).Select(e => e.Reason));
                logger.LogWarning(
                    "Gmail {Operation}: quota superata (HTTP {StatusCode}, reason {Reasons}: {GoogleMessage}), tentativo {Attempt} di {MaxAttempts}, nuovo tentativo tra {DelayMs} ms",
                    operation, (int)ex.HttpStatusCode, reasons, ex.Error?.Message, attempt, retry.MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (GoogleApiException ex) when ((int)ex.HttpStatusCode >= 500 && attempt < retry.MaxAttempts)
            {
                var delay = retry.BaseDelay * Math.Pow(2, attempt - 1);
                logger.LogWarning(
                    "Gmail {Operation}: HTTP {StatusCode}, tentativo {Attempt} di {MaxAttempts}, nuovo tentativo tra {DelayMs} ms",
                    operation, (int)ex.HttpStatusCode, attempt, retry.MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>Rilascia il limitatore di quota.</summary>
    public void Dispose() => _quotaLimiter.Dispose();

    /// <summary>Quota superata: 429, oppure 403 con reason rateLimitExceeded/userRateLimitExceeded; gli altri 403 non migliorano ritentando.</summary>
    private static bool IsRateLimited(GoogleApiException ex) =>
        ex.HttpStatusCode == HttpStatusCode.TooManyRequests
        || (ex.HttpStatusCode == HttpStatusCode.Forbidden
            && (ex.Error?.Errors ?? []).Any(e => e.Reason is GmailConstants.RateLimitExceededReason or GmailConstants.UserRateLimitExceededReason));
}
