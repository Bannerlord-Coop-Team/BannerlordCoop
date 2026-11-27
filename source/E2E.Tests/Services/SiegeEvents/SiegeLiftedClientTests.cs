using Common.Util;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// An AI siege leader can lift a siege a player joined. Vanilla empties the besieger camp before the siege finalizes,
/// so the client must still leave the siege it is camped at when the end arrives.
/// </summary>
public class SiegeLiftedClientTests : SiegeTestBase
{
    public SiegeLiftedClientTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void AiLiftsSiege_CampedClientLeavesTheSiegeMenu()
    {
        var client = Clients.First();
        var aiLeaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (_, memberPartyId) = CreatePlayerHeroParty("LiftedSiegeMember");
        var siege = SetupSiege(aiLeaderId, memberPartyId);
        ConfigureSiegeMenuClient(client, memberPartyId);
        client.Call(() => Assert.NotNull(MobileParty.MainParty.BesiegerCamp));

        using var menuExit = new MethodCallRecorder(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)));
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Campaign.Current.MainParty = null;
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiLeaderId, out var aiLeader));
            // The AI leader's battle at the siege just ended, so vanilla keeps it as camp leader once it leaves
            using (new AllowedThread())
            {
                aiLeader.Party._mapEventSide = CreateEndedBattleSide();
            }

            siegeEvent.BesiegerCamp.RemoveAllSiegeParties();
        });
        client.PumpGameThread();

        var prompt = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());
        Assert.Null(prompt.LeaderPartyId);
        Assert.Contains(memberPartyId, prompt.AttackerPartyIds ?? Array.Empty<string>());
        client.Call(() => Assert.Null(MobileParty.MainParty.BesiegerCamp));
        Assert.Equal(1, menuExit.CountFor(client));
    }

    private static MapEventSide CreateEndedBattleSide()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent._state = MapEventState.WaitingRemoval;
        // v1.5 reads the battle type, which the party's disorganized time checks, from the component
        mapEvent.Component = ObjectHelper.SkipConstructor<FieldBattleEventComponent>();
        var side = ObjectHelper.SkipConstructor<MapEventSide>();
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(side, mapEvent);
        return side;
    }
}
