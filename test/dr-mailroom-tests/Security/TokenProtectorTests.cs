using System.Security.Cryptography;
using dr_mailroom_core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace dr_mailroom_tests.Security;

/// <summary>Verifica la cifratura dei refresh token (T6).</summary>
public class TokenProtectorTests
{
    private const string SampleToken = "1//0g-sample-refresh-token";

    [Fact]
    public void ProtectThenUnprotect_ReturnsOriginalToken()
    {
        var protector = new TokenProtector(new EphemeralDataProtectionProvider());

        var encrypted = protector.Protect(SampleToken);

        Assert.NotEqual(SampleToken, encrypted);
        Assert.DoesNotContain(SampleToken, encrypted);
        Assert.Equal(SampleToken, protector.Unprotect(encrypted));
    }

    [Fact]
    public void Unprotect_WithDifferentKeyRing_Throws()
    {
        var encrypted = new TokenProtector(new EphemeralDataProtectionProvider()).Protect(SampleToken);
        var otherKeyRing = new TokenProtector(new EphemeralDataProtectionProvider());

        Assert.ThrowsAny<CryptographicException>(() => otherKeyRing.Unprotect(encrypted));
    }

    [Fact]
    public void Protect_EmptyToken_Throws()
    {
        var protector = new TokenProtector(new EphemeralDataProtectionProvider());

        Assert.ThrowsAny<ArgumentException>(() => protector.Protect(" "));
    }
}
