using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// The v1.5 stronger-party check stops nearby AI parties from attacking. Only a player party on the map can
/// step in, so a parked party (an offline player's) does not count.
/// </summary>
public class StrongerPlayerNearbyTests : MapEventTestBase
{
    public StrongerPlayerNearbyTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ActivePlayerParty_Counts_ParkedOrNonPlayerPartyDoesNot()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            var playerParty = Server.GetRegisteredObject<MobileParty>(playerPartyId);
            var otherParty = Server.GetRegisteredObject<MobileParty>(otherPartyId);
            playerParty.IsActive = true;
            otherParty.IsActive = true;

            Assert.True(DefaultMobilePartyAIModelPatches.IsActivePlayerParty(playerParty));
            Assert.False(DefaultMobilePartyAIModelPatches.IsActivePlayerParty(otherParty));

            playerParty.IsActive = false;

            Assert.False(DefaultMobilePartyAIModelPatches.IsActivePlayerParty(playerParty));
        });
    }
}
