using dr_mailroom_core.Enums;
using dr_mailroom_core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace dr_mailroom_tests.Security;

/// <summary>Verifica lo state OAuth: scopo e casella del cestino tornano integri, state falsi o di un'altra istanza no.</summary>
public class OAuthStateProtectorTests
{
    [Fact]
    public void TrashState_RoundTripsPurposeMailboxAndSender()
    {
        var protector = new OAuthStateProtector(new EphemeralDataProtectionProvider());

        var state = protector.Read(protector.CreateForTrash(7, "no-reply@spam.example"));

        Assert.NotNull(state);
        Assert.Equal(OAuthPurpose.Trash, state.Purpose);
        Assert.Equal(7, state.MailboxId);
        Assert.Equal("no-reply@spam.example", state.SenderAddress);
    }

    [Fact]
    public void ConnectState_HasNoMailboxAndDifferentNonces()
    {
        var protector = new OAuthStateProtector(new EphemeralDataProtectionProvider());

        var first = protector.CreateForConnect();
        var second = protector.CreateForConnect();

        Assert.NotEqual(first, second);
        var state = protector.Read(first);
        Assert.NotNull(state);
        Assert.Equal(OAuthPurpose.Connect, state.Purpose);
        Assert.Null(state.MailboxId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("falso")]
    public void Read_ForgedState_ReturnsNull(string? forged)
    {
        var protector = new OAuthStateProtector(new EphemeralDataProtectionProvider());

        Assert.Null(protector.Read(forged));
    }

    [Fact]
    public void Read_StateFromAnotherKeyRing_ReturnsNull()
    {
        var state = new OAuthStateProtector(new EphemeralDataProtectionProvider()).CreateForTrash(1, null);

        Assert.Null(new OAuthStateProtector(new EphemeralDataProtectionProvider()).Read(state));
    }
}
