namespace dr_mailroom_core.Gmail;

/// <summary>Ritentativi con backoff esponenziale su 5xx (BaseDelay) e su quota superata, 429 o 403 rateLimitExceeded (RateLimitBaseDelay) (T9).</summary>
public sealed record GmailRetrySettings(int MaxAttempts, TimeSpan BaseDelay, TimeSpan RateLimitBaseDelay)
{
    /// <summary>Valori di produzione: 5 tentativi; 5xx attese 1s, 2s, 4s, 8s; quota attese 10s, 20s, 40s, 80s (copre la finestra di un minuto).</summary>
    public static GmailRetrySettings Default { get; } = new(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
}
