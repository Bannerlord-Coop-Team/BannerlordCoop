using GameInterface.Services.MobileParties.Data;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Verifies retired mobile-party movement protocol types stay removed.
/// </summary>
public class ObsoleteMobilePartyMovementProtocolTests
{
    [Theory]
    [InlineData("GameInterface.Services.MobilePartyAIs.Messages.AiBehaviorInteractablePointUpdated")]
    [InlineData("GameInterface.Services.MobilePartyAIs.Messages.UpdateAiBehaviorInteractablePoint")]
    public void ObsoleteInteractableProtocolTypes_AreNotInAssembly(string fullName)
    {
        Assert.Null(typeof(MobilePartyBehaviorSnapshot).Assembly.GetType(fullName));
    }
}
