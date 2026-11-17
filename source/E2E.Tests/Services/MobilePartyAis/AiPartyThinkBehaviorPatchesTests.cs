using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Tests.Utils;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// Verifies which parties the server lets run the hourly AI decision tick, and that a player clan changing kingdom
/// holds the nearby lords that chased its player parties, like vanilla does around the main party.
/// </summary>
public class AiPartyThinkBehaviorPatchesTests : MapEventTestBase
{
    public AiPartyThinkBehaviorPatchesTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Raid)]
    [InlineData(MapEvent.BattleTypes.Siege)]
    public void AttackerLeaderInAiOnlyMapEvent_NewTargetFinalizesTheEvent(MapEvent.BattleTypes battleType)
    {
        Assert.True(RunAttackerLeaderThinkTick(battleType, withPlayerParty: false));
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Raid)]
    [InlineData(MapEvent.BattleTypes.Siege)]
    public void AttackerLeaderInMapEventWithPlayerParty_DoesNotFinalizeTheEvent(MapEvent.BattleTypes battleType)
    {
        Assert.False(RunAttackerLeaderThinkTick(battleType, withPlayerParty: true));
    }

    [Fact]
    public void ClanChangedKingdom_HoldsOnlyLordsNoLongerAtWarThatChaseItsPlayerParties()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, clanmatePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var aiPartyId = CreateServerParty();
        var peacefulLordId = CreateServerParty();
        var clanmateChaserId = CreateServerParty();
        var warLordId = CreateServerParty();
        var aiPartyChaserId = CreateServerParty();

        Server.Call(() =>
        {
            EnsureEncounterModel();
            var playerParty = Server.GetRegisteredObject<MobileParty>(playerPartyId);
            var clanmateParty = Server.GetRegisteredObject<MobileParty>(clanmatePartyId);
            var aiParty = Server.GetRegisteredObject<MobileParty>(aiPartyId);
            var peacefulLord = Server.GetRegisteredObject<MobileParty>(peacefulLordId);
            var clanmateChaser = Server.GetRegisteredObject<MobileParty>(clanmateChaserId);
            var warLord = Server.GetRegisteredObject<MobileParty>(warLordId);
            var aiPartyChaser = Server.GetRegisteredObject<MobileParty>(aiPartyChaserId);
            var playerClan = playerParty.ActualClan;

            // The harness gives every party its own clan, so move the second player and an AI party into the
            // first player's clan, which then holds player and non-player war parties.
            clanmateParty.ActualClan = playerClan;
            aiParty.ActualClan = playerClan;
            Assert.Contains(playerParty.WarPartyComponent, playerClan.WarPartyComponents);
            Assert.Contains(clanmateParty.WarPartyComponent, playerClan.WarPartyComponents);
            Assert.Contains(aiParty.WarPartyComponent, playerClan.WarPartyComponents);

            PlaceNear(playerParty, clanmateParty, 1f);
            PlaceNear(playerParty, aiParty, 1.5f);
            PlaceNear(playerParty, peacefulLord, 2f);
            PlaceNear(playerParty, clanmateChaser, 2.5f);
            PlaceNear(playerParty, warLord, 3f);
            PlaceNear(playerParty, aiPartyChaser, 3.5f);
            Chase(peacefulLord, playerParty);
            Chase(clanmateChaser, clanmateParty);
            Chase(warLord, playerParty);
            Chase(aiPartyChaser, aiParty);
            FactionManager.DeclareWar(warLord.MapFaction, playerClan.MapFaction);

            new AiPartyThinkBehavior().OnClanChangedKingdom(
                playerClan,
                oldKingdom: null,
                newKingdom: null,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom,
                showNotification: false);

            Assert.Equal(AiBehavior.Hold, peacefulLord.DefaultBehavior);
            Assert.Equal(AiBehavior.Hold, clanmateChaser.DefaultBehavior);
            Assert.Equal(AiBehavior.EngageParty, warLord.DefaultBehavior);
            Assert.Equal(AiBehavior.EngageParty, aiPartyChaser.DefaultBehavior);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// Runs the real <c>AiPartyThinkBehavior.PartyHourlyAiTick</c> for the attacker leader of a raid or siege
    /// assault that scores another target, and returns whether the map event got finalized.
    /// </summary>
    private bool RunAttackerLeaderThinkTick(MapEvent.BattleTypes battleType, bool withPlayerParty)
    {
        var context = CreateServerMapEvent();
        string? playerPartyId = withPlayerParty ? CreatePlayerHeroParty("player").partyId : null;
        var finalized = false;

        // The new target's movement order needs a navigation mesh the test campaign doesn't have.
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(SetPartyAiAction), nameof(SetPartyAiAction.GetActionForVisitingSettlement)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.AttackerPartyId, out var attacker));

            mapEvent.SetBattleType(battleType);
            if (playerPartyId != null)
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
                playerParty.Party._mapEventSide = mapEvent.DefenderSide;
                mapEvent.DefenderSide._battleParties.Add(new MapEventParty(playerParty.Party));
            }

            var otherTarget = GameObjectCreator.CreateInitializedObject<Settlement>();
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, (party, think) =>
            {
                if (party != attacker) return;
                think.AddBehaviorScore((new AIBehaviorData(
                    otherTarget, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default,
                    willGatherArmy: false, isFromPort: false, isTargetingPort: false), 1f));
            });

            try
            {
                Assert.Same(attacker.Party, mapEvent.AttackerSide.LeaderParty);
                new AiPartyThinkBehavior().PartyHourlyAiTick(attacker);
                finalized = mapEvent.IsFinalized;
            }
            finally
            {
                CampaignEvents.AiHourlyTickEvent.ClearListeners(this);
            }
        }, disabledMethods);

        return finalized;
    }

    // Moves a party onto the map next to another party, inside the hold's search radius.
    private static void PlaceNear(MobileParty anchor, MobileParty party, float offset)
    {
        party.Position = new CampaignVec2(anchor.Position.ToVec2() + new Vec2(offset, 0f), true);
    }

    // Orders a lord to engage a party, with the short term target the hold reads.
    private static void Chase(MobileParty lord, MobileParty target)
    {
        lord.SetMoveEngageParty(target, MobileParty.NavigationType.Default);
        lord.RecalculateShortTermBehavior();
        Assert.Same(target, lord.ShortTermTargetParty);
        Assert.Equal(AiBehavior.EngageParty, lord.DefaultBehavior);
    }
}
