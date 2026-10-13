using System.Security.Cryptography;
using System.Text.Json;
using dr_mailroom_core.Enums;
using Microsoft.AspNetCore.DataProtection;

namespace dr_mailroom_core.Security;

/// <summary>
/// State del flusso OAuth firmato, cifrato e a scadenza (anti-CSRF): il callback è anonimo perché lo chiama il browser
/// di ritorno da Google, quindi accetta solo state emessi da questa istanza negli ultimi minuti.
/// </summary>
public sealed class OAuthStateProtector(IDataProtectionProvider provider)
{
    /// <summary>Scopo dedicato, separato da quello dei refresh token. v2: lo state porta un contenuto JSON.</summary>
    public const string Purpose = "dr-mailroom.oauth-state.v2";

    /// <summary>Validità dello state: il tempo per completare il consenso su Google.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const int NonceBytes = 16;

    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    /// <summary>State per il collegamento di una casella.</summary>
    public string CreateForConnect() => Protect(new OAuthState(OAuthPurpose.Connect, null, null, NewNonce()));

    /// <summary>State per l'autorizzazione del cestino di una casella, con il mittente da cui riprendere.</summary>
    public string CreateForTrash(int mailboxId, string? senderAddress) =>
        Protect(new OAuthState(OAuthPurpose.Trash, mailboxId, senderAddress, NewNonce()));

    /// <summary>Contenuto dello state se è integro, emesso da questa istanza e non scaduto; altrimenti null.</summary>
    public OAuthState? Read(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OAuthState>(_protector.Unprotect(state));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private string Protect(OAuthState state) => _protector.Protect(JsonSerializer.Serialize(state), Lifetime);

    private static string NewNonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(NonceBytes));
}
