namespace dr_mailroom_core.Security;

/// <summary>Cifra e decifra i refresh token OAuth prima di salvarli in Mailbox.EncryptedRefreshToken (T6).</summary>
public interface ITokenProtector
{
    /// <summary>Restituisce il token cifrato, pronto per il database.</summary>
    string Protect(string refreshToken);

    /// <summary>Restituisce il token in chiaro; lancia CryptographicException se il dato è alterato o la chiave manca.</summary>
    string Unprotect(string encryptedRefreshToken);
}
