using Common.Util;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Tests.Utils;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>Verifies the end of a won siege hands its leader or an army member to the aftermath route (#3903).</summary>
public class SiegeAftermathEndRouteTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public SiegeAftermathEndRouteTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void EndState_ArmyMemberOfWonSiege_IsRoutedToAftermathInsteadOfFinished()
    {
        var client = environment.Clients.First();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var siegeEvents = new Mock<ISiegeEventInterface>();
        siegeEvents.Setup(siegeEvent => siegeEvent.TryRouteToAftermath(It.IsAny<MapEvent>())).Returns(true);
        MapEvent? siege = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            siege = CreateMapEvent(settlement);
            var encounter = CreateEndingEncounter(siege);
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

        // the route is keyed off the ending encounter's own map event, like vanilla DoEnd
        siegeEvents.Verify(siegeEvent => siegeEvent.TryRouteToAftermath(siege), Times.Once);
    }

    [Fact]
    public void EndState_AftermathAppliedBeforeMemberReachesEnd_StillRoutesToAftermath()
    {
        var client = environment.Clients.First();
        var settlementId = environment.CreateRegisteredObject<Settlement>();

        // record (and skip) the stale encounter teardown; the menu switch runs for real
        using var finishRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();
        MenuContext menuContext = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            var wonSiege = CreateMapEvent(settlement);
            var aftermathBehavior = SetUpArmyMemberOfWonSiege(siegeEvents, settlement, wonSiege);
            menuContext = SeedStaleEncounterMenu(aftermathBehavior);

            // the leader picks before this member's encounter reaches End
            siegeEvents.SetLocalAftermathNarration(settlement, (int)SiegeAftermathAction.SiegeAftermath.ShowMercy);

            RunEnd(playerEncounters, CreateEndingEncounter(wonSiege));

            Assert.Equal(SiegeAftermathAction.SiegeAftermath.ShowMercy, aftermathBehavior._playerEncounterAftermath);
            // vanilla menu_settlement_taken routes a member to the leader's decision narration
            Assert.Same(menuContext, Campaign.Current.CurrentMenuContext);
            Assert.Equal("menu_settlement_taken_player_army_member", menuContext.GameMenu.StringId);
        });

        // only the route's own Finish of the stale encounter, not the plain end-of-battle one as well
        Assert.Equal(1, finishRecorder.CountFor(client));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndState_PromptedLeaderOfWonSiege_GetsTheChoiceAfterTheLootSteps(bool simulated)
    {
        var client = environment.Clients.First();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var mainPartyId = environment.CreateRegisteredObject<MobileParty>();
        var mapEventId = environment.CreateRegisteredObject<MapEvent>();
        var defeatedClanId = environment.CreateRegisteredObject<Clan>();

        // record (and skip) the engine-side encounter calls; the menu switch runs for real
        using var finishRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));
        using var settlementEntryRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty)),
            AccessTools.Method(typeof(EncounterManager), nameof(EncounterManager.StartSettlementEncounter)));

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            var lordComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
            lordComponent._leader = Hero.MainHero;
            mainParty._partyComponent = lordComponent;
            Campaign.Current.MainParty = mainParty;

            // a vassal's capture: the fief has gone to the ruler, so the defeated owner only comes with the prompt
            var wonSiege = client.GetRegisteredObject<MapEvent>(mapEventId);
            using (new AllowedThread())
            {
                wonSiege.SetBattleType(MapEvent.BattleTypes.Siege);
                wonSiege.SetMapEventSettlement(settlement);
                wonSiege.BattleState = BattleState.AttackerVictory;
            }
            var aftermathBehavior = GetAftermathBehavior();
            aftermathBehavior._besiegerParty = mainParty;
            aftermathBehavior._wasPlayerArmyMember = false;
            var menuContext = SeedStaleEncounterMenu(aftermathBehavior);

            // A played siege can dispatch OnMapEventEnded before the prompt. The simulation can be removed first.
            if (!simulated)
                siegeEvents.SetLocalAftermathNarrationContext(settlement, wonSiege);
            var encounter = CreateEndingEncounter(wonSiege);
            encounter.PlayerSide = BattleSideEnum.Attacker;
            encounter.EncounterState = PlayerEncounterState.LootParty;
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                client.SimulateMessage(environment.Server.NetPeer,
                    new NetworkPromptSiegeAftermathChoice(settlementId, mainPartyId, defeatedClanId));
                Assert.Equal("encounter", menuContext.GameMenu.StringId);

                if (simulated)
                {
                    // Replicated removal can arrive while the simulation scoreboard still owns the encounter.
                    var simulation = ObjectHelper.SkipConstructor<BattleSimulation>();
                    AccessTools.Field(typeof(BattleSimulation), nameof(BattleSimulation._mapEvent)).SetValue(simulation, wonSiege);
                    encounter.BattleSimulation = simulation;
                    var mapState = (MapState)Game.Current.GameStateManager.ActiveState;
                    mapState._battleSimulation = encounter.BattleSimulation;
                    client.SimulateMessage(environment.Server.NetPeer,
                        new NetworkDestroyInstance<MapEvent>(client.GetHandle<MapEvent>(mapEventId)));
                    mapState._battleSimulation = null;
                }
                else
                {
                    // A solo mission host can receive the fallback reply when the server finalized first.
                    client.SimulateMessage(environment.Server.NetPeer, new NetworkMapEventFinalized());
                }

                encounter.EncounterState = PlayerEncounterState.End;
                if (simulated)
                    client.Resolve<IMapEventInitializationBarrier>().CompleteDeferredEncounterCleanup();
                playerEncounters.UpdateInternalAfterBattle(encounter);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
                SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out _);
                SiegeCaptureMenuHoldPatch.Release(settlement);
            }

            // vanilla menu_settlement_taken routes the besieger to the Devastate/Pillage/Mercy choice
            Assert.Same(menuContext, Campaign.Current.CurrentMenuContext);
            Assert.Equal("menu_settlement_taken_player_leader", menuContext.GameMenu.StringId);
            Assert.Same(mainParty, aftermathBehavior._besiegerParty);
            Assert.Same(client.GetRegisteredObject<Clan>(defeatedClanId), aftermathBehavior._prevSettlementOwnerClan);
        });

        // only the route's own Finish of the ended encounter, not the plain end-of-battle one as well
        Assert.Equal(1, finishRecorder.CountFor(client));
        Assert.Equal(2, settlementEntryRecorder.CountFor(client));
    }

    [Fact]
    public void EndState_StalePendingAftermath_DoesNotRouteLaterBattle()
    {
        var client = environment.Clients.First();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        using var recorders = new EngineCallRecorders();

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            // the won siege's own End never ran (left via _leaveEncounter), so the member route stays pending
            SetUpArmyMemberOfWonSiege(siegeEvents, settlement, CreateMapEvent(settlement));

            // a field fight days later, then a new battle at the old siege's settlement
            RunEnd(playerEncounters, CreateEndingEncounter(CreateMapEvent(null)));
            RunEnd(playerEncounters, CreateEndingEncounter(CreateMapEvent(settlement)));
        });

        Assert.Empty(recorders.Menus.MenusFor(client));
        // both encounters take the plain end-of-battle Finish
        Assert.Equal(2, recorders.Finishes.CountFor(client));
    }

    [Fact]
    public void EndState_StalePendingAftermath_DoesNotRouteNextBattleAtSameSettlement()
    {
        var client = environment.Clients.First();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        using var recorders = new EngineCallRecorders();

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            // the won siege's own End never ran, so the member route stays pending
            SetUpArmyMemberOfWonSiege(siegeEvents, settlement, CreateMapEvent(settlement));

            // the very next End is a different battle at the same settlement, e.g. a later siege assault
            RunEnd(playerEncounters, CreateEndingEncounter(CreateMapEvent(settlement)));
        });

        Assert.Empty(recorders.Menus.MenusFor(client));
        Assert.Equal(1, recorders.Finishes.CountFor(client));
    }

    // Leaves what the client's OnMapEventEnded prefix sets for a won siege's army member, then the narration context.
    private SiegeAftermathCampaignBehavior SetUpArmyMemberOfWonSiege(ISiegeEventInterface siegeEvents, Settlement settlement, MapEvent wonSiege)
    {
        var client = environment.Clients.First();
        var mainPartyId = environment.CreateRegisteredObject<MobileParty>();
        var leaderPartyId = environment.CreateRegisteredObject<MobileParty>();
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));

        Campaign.Current.MainParty = mainParty;
        var army = ObjectHelper.SkipConstructor<Army>();
        using (new AllowedThread())
        {
            army.LeaderParty = leaderParty;
        }
        mainParty._army = army;

        var aftermathBehavior = GetAftermathBehavior();
        aftermathBehavior._besiegerParty = leaderParty;
        aftermathBehavior._wasPlayerArmyMember = true;
        siegeEvents.SetLocalAftermathNarrationContext(settlement, wonSiege);
        return aftermathBehavior;
    }

    // Returns the client's siege aftermath behavior, adding one when the harness campaign has none.
    private static SiegeAftermathCampaignBehavior GetAftermathBehavior()
    {
        if (Campaign.Current.GetCampaignBehavior<SiegeAftermathCampaignBehavior>() == null)
        {
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { new SiegeAftermathCampaignBehavior() }));
        }

        return Campaign.Current.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
    }

    // Builds a map event at the given settlement (null for a field fight).
    private static MapEvent CreateMapEvent(Settlement? mapEventSettlement)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        using (new AllowedThread())
        {
            mapEvent.SetMapEventSettlement(mapEventSettlement);
        }
        return mapEvent;
    }

    // Builds an after-battle encounter at End for the given map event.
    private static PlayerEncounter CreateEndingEncounter(MapEvent mapEvent)
    {
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._mapEvent = mapEvent;
        encounter.EncounterState = PlayerEncounterState.End;
        return encounter;
    }

    // Runs the after-battle update for one encounter while it is the current encounter.
    private static void RunEnd(IPlayerEncounterInterface playerEncounters, PlayerEncounter encounter)
    {
        Campaign.Current.PlayerEncounter = encounter;
        try
        {
            playerEncounters.UpdateInternalAfterBattle(encounter);
        }
        finally
        {
            Campaign.Current.PlayerEncounter = null;
        }
    }

    /// <summary>Records (and skips) the engine-side calls so only the routing decision is under test.</summary>
    private sealed class EngineCallRecorders : IDisposable
    {
        public MethodCallRecorder Finishes { get; } = new(Priority.First,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));

        public MethodCallRecorder Menus { get; } = new(Priority.First,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }));

        // Removes both recorders' patches.
        public void Dispose()
        {
            Finishes.Dispose();
            Menus.Dispose();
        }
    }

    // Registers the vanilla aftermath menus on the live behavior and opens a map menu still showing the stale encounter.
    private static MenuContext SeedStaleEncounterMenu(SiegeAftermathCampaignBehavior aftermathBehavior)
    {
        var menus = Campaign.Current.GameMenuManager;
        if (menus.GetGameMenu("menu_settlement_taken") == null)
        {
            aftermathBehavior.AddGameMenus(new CampaignGameStarter(menus, Campaign.Current.ConversationManager));
        }

        // The narrations' text needs a fully built army and settlement; only where they land is under test.
        menus.GetGameMenu("menu_settlement_taken_player_army_member").OnInit = _ => { };
        menus.GetGameMenu("menu_settlement_taken_player_leader").OnInit = _ => { };

        var starter = new CampaignGameStarter(menus, Campaign.Current.ConversationManager);
        if (menus.GetGameMenu("encounter") == null)
            starter.AddGameMenu("encounter", "", _ => { });

        var context = Game.Current.ObjectManager.CreateObject<MenuContext>();
        context.GameMenu = menus.GetGameMenu("encounter");
        var mapState = Game.Current.GameStateManager.CreateState<MapState>();
        mapState._menuContext = context;
        Game.Current.GameStateManager._gameStates.Add(mapState);
        return context;
    }
}
