using Microsoft.AspNetCore.DataProtection;

namespace dr_mailroom_core.Security;

/// <summary>ITokenProtector basato su ASP.NET Core Data Protection, con uno scopo dedicato ai refresh token.</summary>
public sealed class TokenProtector(IDataProtectionProvider provider) : ITokenProtector
{
    /// <summary>Scopo che isola questi dati cifrati da ogni altro uso del key ring.</summary>
    public const string Purpose = "dr-mailroom.gmail-refresh-token.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    /// <summary>Cifra il refresh token. Il valore non va mai loggato.</summary>
    public string Protect(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        return _protector.Protect(refreshToken);
    }

    /// <summary>Decifra il refresh token letto dal database.</summary>
    public string Unprotect(string encryptedRefreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedRefreshToken);
        return _protector.Unprotect(encryptedRefreshToken);
    }
}
