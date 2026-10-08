using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Posizione del key ring Data Protection che cifra i refresh token (T6, C11).</summary>
public sealed class TokenProtectionOptions
{
    public const string SectionName = "Mailroom:DataProtection";

    /// <summary>Cartella del key ring: in container va su un volume, mai dentro il database.</summary>
    [Required]
    public string KeyRingPath { get; init; } = string.Empty;
}
