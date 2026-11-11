using Common.Util;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
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
        ConfigureCampedClient(client, memberPartyId);

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

    private static void ConfigureCampedClient(EnvironmentInstance client, string mainPartyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            Assert.NotNull(mainParty.BesiegerCamp);
            Campaign.Current.MainParty = mainParty;
            Campaign.Current.PlayerEncounter = null;

            var gameStateManager = Game.Current.GameStateManager;
            var mapState = gameStateManager.CreateState<MapState>();
            var menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            menuContext.GameMenu = new GameMenu("menu_siege_strategies");
            mapState._menuContext = menuContext;
            gameStateManager._gameStates.Add(mapState);
        });
    }
}
