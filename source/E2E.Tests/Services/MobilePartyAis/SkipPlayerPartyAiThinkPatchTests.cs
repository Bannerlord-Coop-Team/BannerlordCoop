using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// Verifies which parties the server lets run the vanilla hourly AI decision tick.
/// </summary>
public class SkipPlayerPartyAiThinkPatchTests : MapEventTestBase
{
    public SkipPlayerPartyAiThinkPatchTests(ITestOutputHelper output) : base(output) { }

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

            mapEvent._mapEventType = battleType;
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
}
