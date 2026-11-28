using E2E.Tests.Services.MapEvents;
using GameInterface.Tests.Utils;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// An AI siege capture finalized on the server enters its capturer once the battle lets go of it, so vanilla's
/// garrison logic runs for the conquest and clears <c>GarrisonTroopsCampaignBehavior._newlyConqueredFortification</c>.
/// </summary>
public class SiegeCaptureEntryTests : MapEventTestBase
{
    public SiegeCaptureEntryTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AiSiegeCapture_EntersCapturerAndClearsNewlyConqueredFortification()
    {
        var context = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var previousOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var client in Clients)
        {
            client.Call(() => client.GetRegisteredObject<Settlement>(settlementId)
                .SetSettlementComponent(client.GetRegisteredObject<Town>(townId)));
        }

        var disabledMethods = BattleResultDisabledMethods
            .Concat(SiegeCreationDisabledMethods)
            // Taking the defeated party's members prisoner needs a live campaign world
            .Append(AccessTools.Method(typeof(MapEvent), nameof(MapEvent.CaptureDefeatedPartyMembers)))
            // The camp leave cascade needs a live campaign world, the BesiegerCamp setter itself still runs and replicates
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyLeftSiegeInternal)))
            // The garrison transfer reads sizing models the harness doesn't have. The flag clear after it still runs.
            .Append(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), nameof(GarrisonTroopsCampaignBehavior.CalculateSettlementGarrisonPartySizeLimitWithFoodAndWage)))
            .Append(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), nameof(GarrisonTroopsCampaignBehavior.CalculateSettlementIdealPartySizeWithEffects)))
            .Append(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), nameof(GarrisonTroopsCampaignBehavior.CalculateMobilePartySizeLimitWithFoodAndWage)))
            .Append(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), nameof(GarrisonTroopsCampaignBehavior.TryToLeaveTroopsToGarrisonForParty)))
            .Append(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), nameof(GarrisonTroopsCampaignBehavior.TryToTakeTroopsFromGarrisonForParty)))
            .ToList();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            var capturer = Server.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            var town = Server.GetRegisteredObject<Town>(townId);
            var previousOwner = Server.GetRegisteredObject<Hero>(previousOwnerId);
            previousOwner.Clan.SetLeader(previousOwner);
            settlement.SetSettlementComponent(town);
            town.OwnerClan = previousOwner.Clan;

            CreateReplicableSiegeEvent(settlement, capturer);
            mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
            mapEvent.SetMapEventSettlement(settlement);
            mapEvent.BattleState = BattleState.AttackerVictory;

            var garrisons = new GarrisonTroopsCampaignBehavior();
            Settlement? conqueredAfterCapture = null;
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(garrisons, garrisons.OnSettlementOwnerChanged);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(garrisons, garrisons.OnSettlementEntered);
            // What KingdomManager.SiegeCompleted does for the capture, without the campaign's kingdom and army state
            CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, (Settlement captured, MobileParty party, bool won, MapEvent.BattleTypes _) =>
            {
                party.BesiegerCamp = null;
                ChangeOwnerOfSettlementAction.ApplyBySiege(party.LeaderHero, party.LeaderHero, captured);
                conqueredAfterCapture = garrisons._newlyConqueredFortification;
                EnterSettlementAction.ApplyForParty(party, captured);
            });
            bool gameStarted = Campaign.Current.GameStarted;
            // GarrisonTroopsCampaignBehavior.OnSettlementEntered returns early until the game has started
            Campaign.Current.GameStarted = true;
            try
            {
                // The path an AI-only battle takes on the server, from MapEvent.Update
                mapEvent.FinalizeEvent();

                Assert.True(mapEvent.IsFinalized);
                Assert.Same(settlement, conqueredAfterCapture);
                Assert.Same(settlement, capturer.CurrentSettlement);
                Assert.Null(garrisons._newlyConqueredFortification);
            }
            finally
            {
                Campaign.Current.GameStarted = gameStarted;
                CampaignEvents.OnSettlementOwnerChangedEvent.ClearListeners(garrisons);
                CampaignEvents.SettlementEntered.ClearListeners(garrisons);
                CampaignEvents.SiegeCompletedEvent.ClearListeners(this);
            }
        }, disabledMethods);

        foreach (var client in Clients)
        {
            client.Call(() => Assert.Same(
                client.GetRegisteredObject<Settlement>(settlementId),
                client.GetRegisteredObject<MobileParty>(context.AttackerPartyId).CurrentSettlement));
        }
    }
}
