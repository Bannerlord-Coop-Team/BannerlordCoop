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
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var siegeEvents = new Mock<ISiegeEventInterface>();
        siegeEvents.Setup(siege => siege.TryRouteArmyMemberToAftermath(It.IsAny<Settlement>())).Returns(true);
        Settlement? siegeSettlement = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject(settlementId, out siegeSettlement));
            var encounter = CreateEndingEncounter(siegeSettlement);
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

        // the route is keyed off the ending encounter's own map event settlement, like vanilla DoEnd
        siegeEvents.Verify(siege => siege.TryRouteArmyMemberToAftermath(siegeSettlement), Times.Once);
    }

    [Fact]
    public void EndState_AftermathAppliedBeforeMemberReachesEnd_StillRoutesToAftermath()
    {
        var client = environment.Clients.First();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        using var recorders = new EngineCallRecorders();

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            var aftermathBehavior = SetUpArmyMemberOfWonSiege(siegeEvents, settlement);

            // the leader picks before this member's encounter reaches End
            siegeEvents.SetLocalAftermathNarration(settlement, (int)SiegeAftermathAction.SiegeAftermath.ShowMercy);

            RunEnd(playerEncounters, CreateEndingEncounter(settlement));

            Assert.Equal(SiegeAftermathAction.SiegeAftermath.ShowMercy, aftermathBehavior._playerEncounterAftermath);
        });

        Assert.Equal(1, recorders.SettlementEncounters.CountFor(client));
        Assert.Equal(new[] { "menu_settlement_taken" }, recorders.Menus.MenusFor(client));
        // only the route's own Finish of the stale encounter, not the plain end-of-battle one as well
        Assert.Equal(1, recorders.Finishes.CountFor(client));
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
            SetUpArmyMemberOfWonSiege(siegeEvents, settlement);

            // a field fight days later, then the old siege's settlement again
            RunEnd(playerEncounters, CreateEndingEncounter(null));
            RunEnd(playerEncounters, CreateEndingEncounter(settlement));
        });

        Assert.Equal(0, recorders.SettlementEncounters.CountFor(client));
        Assert.Empty(recorders.Menus.MenusFor(client));
        // both encounters take the plain end-of-battle Finish
        Assert.Equal(2, recorders.Finishes.CountFor(client));
    }

    // Leaves what the client's OnMapEventEnded prefix sets for a won siege's army member, then the narration context.
    private SiegeAftermathCampaignBehavior SetUpArmyMemberOfWonSiege(ISiegeEventInterface siegeEvents, Settlement settlement)
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

        if (Campaign.Current.GetCampaignBehavior<SiegeAftermathCampaignBehavior>() == null)
        {
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { new SiegeAftermathCampaignBehavior() }));
        }

        var aftermathBehavior = Campaign.Current.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
        aftermathBehavior._besiegerParty = leaderParty;
        aftermathBehavior._wasPlayerArmyMember = true;
        siegeEvents.SetLocalAftermathNarrationContext(settlement);
        return aftermathBehavior;
    }

    // Builds an after-battle encounter at End whose map event is at the given settlement (null for a field fight).
    private static PlayerEncounter CreateEndingEncounter(Settlement? mapEventSettlement)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        using (new AllowedThread())
        {
            mapEvent.MapEventSettlement = mapEventSettlement;
        }

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

        public MethodCallRecorder SettlementEncounters { get; } = new(Priority.First,
            AccessTools.Method(typeof(EncounterManager), nameof(EncounterManager.StartSettlementEncounter)));

        public MethodCallRecorder Menus { get; } = new(Priority.First,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }));

        // Removes all three recorders' patches.
        public void Dispose()
        {
            Finishes.Dispose();
            SettlementEncounters.Dispose();
            Menus.Dispose();
        }
    }
}
