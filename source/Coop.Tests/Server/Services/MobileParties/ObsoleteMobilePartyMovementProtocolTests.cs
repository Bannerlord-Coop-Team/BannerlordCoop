using Coop.Core.Server.Services.MobileParties.PacketHandlers;
using Xunit;

namespace Coop.Tests.Server.Services.MobileParties;

/// <summary>
/// Verifies retired mobile-party movement protocol types stay removed.
/// </summary>
public class ObsoleteMobilePartyMovementProtocolTests
{
    [Theory]
    [InlineData("Coop.Core.Server.Services.MobileParties.Messages.NetworkUpdatePartyMovement")]
    [InlineData("Coop.Core.Server.Services.MobileParties.Messages.NetworkPartyMovementRequested")]
    [InlineData("Coop.Core.Common.Services.MobileParties.Data.MobilePartyMovementData")]
    [InlineData("Coop.Core.Common.Services.MobileParties.Data.MovementType")]
    public void ObsoleteMovementProtocolTypes_AreNotInAssembly(string fullName)
    {
        Assert.Null(typeof(RequestMobilePartyBehaviorPacketHandler).Assembly.GetType(fullName));
    }
}
