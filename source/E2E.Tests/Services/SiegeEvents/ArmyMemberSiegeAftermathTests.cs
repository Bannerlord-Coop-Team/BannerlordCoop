using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>Verifies the end of a won siege hands an army member to the aftermath route (#3903).</summary>
public class ArmyMemberSiegeAftermathTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public ArmyMemberSiegeAftermathTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void EndState_ArmyMemberOfWonSiege_IsRoutedToAftermathInsteadOfFinished()
    {
        var client = environment.Clients.First();
        var siegeEvents = new Mock<ISiegeEventInterface>();
        siegeEvents.Setup(siege => siege.TryRouteArmyMemberToAftermath()).Returns(true);

        client.Call(() =>
        {
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterState = PlayerEncounterState.End;
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                new PlayerEncounterInterface(siegeEvents.Object).UpdateInternalAfterBattle(encounter);

                // the route finishes the encounter itself; the plain Finish must not run first
                Assert.Same(encounter, Campaign.Current.PlayerEncounter);
                Assert.True(encounter._stateHandled);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });

        siegeEvents.Verify(siege => siege.TryRouteArmyMemberToAftermath(), Times.Once);
    }

    [Fact]
    public void EndState_AftermathAppliedBeforeMemberReachesEnd_StillRoutesToAftermath()
    {
        var client = environment.Clients.First();
        var mainPartyId = environment.CreateRegisteredObject<MobileParty>();
        var leaderPartyId = environment.CreateRegisteredObject<MobileParty>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();

        // record (and skip) the stale encounter teardown; the menu switch runs for real
        using var finishRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();
        MenuContext menuContext = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Campaign.Current.MainParty = mainParty;
            var army = ObjectHelper.SkipConstructor<Army>();
            using (new AllowedThread())
            {
                army.LeaderParty = leaderParty;
            }
            mainParty._army = army;

            if (Campaign.Current.GetCampaignBehavior<SiegeAftermathCampaignBehavior>() == null)
            {
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { new SiegeAftermathCampaignBehavior() }));
            }

            // what the client's OnMapEventEnded prefix leaves behind for a won siege's army member
            var aftermathBehavior = Campaign.Current.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
            aftermathBehavior._besiegerParty = leaderParty;
            aftermathBehavior._wasPlayerArmyMember = true;
            menuContext = SeedStaleEncounterMenu(aftermathBehavior);
            siegeEvents.SetLocalAftermathNarrationContext(settlement);

            // the leader picks before this member's encounter reaches End
            siegeEvents.SetLocalAftermathNarration(settlement, (int)SiegeAftermathAction.SiegeAftermath.ShowMercy);

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterState = PlayerEncounterState.End;
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                playerEncounters.UpdateInternalAfterBattle(encounter);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }

            Assert.Equal(SiegeAftermathAction.SiegeAftermath.ShowMercy, aftermathBehavior._playerEncounterAftermath);
            // vanilla menu_settlement_taken routes a member to the leader's decision narration
            Assert.Same(menuContext, Campaign.Current.CurrentMenuContext);
            Assert.Equal("menu_settlement_taken_player_army_member", menuContext.GameMenu.StringId);
        });

        // only the route's own Finish of the stale encounter, not the plain end-of-battle one as well
        Assert.Equal(1, finishRecorder.CountFor(client));
    }

    // Registers the vanilla aftermath menus on the live behavior and opens a map menu still showing the stale encounter.
    private static MenuContext SeedStaleEncounterMenu(SiegeAftermathCampaignBehavior aftermathBehavior)
    {
        var menus = Campaign.Current.GameMenuManager;
        if (menus.GetGameMenu("menu_settlement_taken") == null)
        {
            aftermathBehavior.AddGameMenus(new CampaignGameStarter(menus, Campaign.Current.ConversationManager));
        }

        // The member narration's text needs a fully built army and settlement; only where it lands is under test.
        menus.GetGameMenu("menu_settlement_taken_player_army_member").OnInit = _ => { };

        var context = Game.Current.ObjectManager.CreateObject<MenuContext>();
        context.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
        context.GameMenu.StringId = "encounter";
        var mapState = Game.Current.GameStateManager.CreateState<MapState>();
        mapState._menuContext = context;
        Game.Current.GameStateManager._gameStates.Add(mapState);
        return context;
    }
}