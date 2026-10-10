using System.Net.Mail;

namespace dr_mailroom_core.Gmail;

/// <summary>Estrae indirizzo (minuscolo, D7) e nome visualizzato dall'header From.</summary>
public static class SenderParser
{
    /// <summary>Header "Nome &lt;indirizzo&gt;" → (indirizzo minuscolo, nome); header non parsabile → testo grezzo minuscolo, nome null.</summary>
    public static (string Address, string? DisplayName) Parse(string? fromHeader)
    {
        if (string.IsNullOrWhiteSpace(fromHeader))
        {
            return (string.Empty, null);
        }

        if (MailAddress.TryCreate(fromHeader.Trim(), out var mailAddress))
        {
            var displayName = string.IsNullOrWhiteSpace(mailAddress.DisplayName) ? null : mailAddress.DisplayName;
            return (mailAddress.Address.ToLowerInvariant(), displayName);
        }

        return (fromHeader.Trim().ToLowerInvariant(), null);
    }
}
