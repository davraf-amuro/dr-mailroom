using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace dr_mailroom_core.Security;

/// <summary>
/// State del flusso OAuth firmato e a scadenza (anti-CSRF): il callback è anonimo perché lo chiama il browser
/// di ritorno da Google, quindi accetta solo state emessi da questa istanza negli ultimi minuti.
/// </summary>
public sealed class OAuthStateProtector(IDataProtectionProvider provider)
{
    /// <summary>Scopo dedicato, separato da quello dei refresh token.</summary>
    public const string Purpose = "dr-mailroom.oauth-state.v1";

    /// <summary>Validità dello state: il tempo per completare il consenso su Google.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>Nuovo state casuale, firmato e con scadenza.</summary>
    public string Create() => _protector.Protect(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), Lifetime);

    /// <summary>True se lo state è integro, emesso da questa istanza e non scaduto.</summary>
    public bool IsValid(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        try
        {
            _protector.Unprotect(state);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
