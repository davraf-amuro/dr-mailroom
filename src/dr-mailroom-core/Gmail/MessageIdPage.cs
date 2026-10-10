namespace dr_mailroom_core.Gmail;

/// <summary>Pagina di id messaggio del backfill, con il token della pagina successiva (null = ultima).</summary>
public sealed record MessageIdPage(IReadOnlyList<string> MessageIds, string? NextPageToken);
