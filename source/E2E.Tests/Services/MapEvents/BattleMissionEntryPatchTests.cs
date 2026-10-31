using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Checks that <see cref="BattleMissionEntryPatch"/> opens the coop battle window on the entering client's own
/// battle mission registry before the native battle mission is built.
/// </summary>
public class BattleMissionEntryPatchTests : MapEventTestBase
{
    public BattleMissionEntryPatchTests(ITestOutputHelper output) : base(output)
    {
    }

    // The prefix opens the window for the main party's map event on the entering client and leaves other clients closed.
    [Fact]
    public void Prefix_OpensBattleWindowForMainPartyMapEventOnEnteringClientOnly()
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEvent.MapEventId, BattleSideEnum.Attacker);
        var client = Clients.Last();
        var otherClient = Clients.First();
        var missionRegistry = client.Resolve<ICoopBattleMissionRegistry>();

        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                var campaign = Campaign.Current;
                var previousMainParty = campaign.MainParty;
                var previousEncounter = campaign.PlayerEncounter;

                try
                {
                    campaign.MainParty = party;
                    campaign.PlayerEncounter = null;

                    BattleMissionEntryPatch.Prefix();

                    Assert.Equal(mapEvent.MapEventId, missionRegistry.ActiveMapEventId);
                }
                finally
                {
                    campaign.MainParty = previousMainParty;
                    campaign.PlayerEncounter = previousEncounter;
                }
            });

            Assert.False(otherClient.Resolve<ICoopBattleMissionRegistry>().IsCoopBattleActive);
        }
        finally
        {
            missionRegistry.EndBattle();
        }
    }
}
