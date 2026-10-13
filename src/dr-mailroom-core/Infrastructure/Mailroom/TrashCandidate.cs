namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Mail da spostare nel cestino: Id interno (per marcarla) e id Gmail (per la chiamata a Gmail).</summary>
public sealed record TrashCandidate(long Id, string GmailMessageId);
