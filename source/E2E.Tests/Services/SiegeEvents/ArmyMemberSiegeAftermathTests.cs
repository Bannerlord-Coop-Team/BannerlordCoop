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

        // record (and skip) the engine-side calls so only the routing decision is under test
        using var finishRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));
        using var settlementEncounterRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(EncounterManager), nameof(EncounterManager.StartSettlementEncounter)));
        using var menuRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }));

        var siegeEvents = client.Resolve<ISiegeEventInterface>();
        var playerEncounters = client.Resolve<IPlayerEncounterInterface>();

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
        });

        Assert.Equal(1, settlementEncounterRecorder.CountFor(client));
        Assert.Equal(new[] { "menu_settlement_taken" }, menuRecorder.MenusFor(client));
        // only the route's own Finish of the stale encounter, not the plain end-of-battle one as well
        Assert.Equal(1, finishRecorder.CountFor(client));
    }
}