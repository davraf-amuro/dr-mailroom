using dr_mailroom_core.Filtering;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_core.Processes;

/// <summary>Dati che un processo riceve per lavorare un messaggio: sessione Gmail aperta, messaggio, filtri corrispondenti.</summary>
public sealed record ProcessContext(IGmailSession Session, Message Message, FilterMatchResult Match);
