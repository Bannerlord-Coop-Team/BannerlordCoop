using Common.Util;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

// Breaking through to a besieged settlement must satisfy the defender join gate: the
// break-in postfix issues the same DefendSettlement order the "Join the defense" button does.
public class SiegeBreakInDefendOrderTests
{
    [Fact]
    public void BreakInContinuationPostfix_TargetsVanillaMethod()
    {
        var method = AccessTools.Method(
            typeof(EncounterGameMenuBehavior),
            "break_in_debrief_continue_on_consequence");

        Assert.NotNull(method);

        var postfix = AccessTools.Method(
            typeof(SiegeEntryFlowPatches),
            nameof(SiegeEntryFlowPatches.BreakInContinuationPostfix));

        Assert.NotNull(postfix);
    }

    [Fact]
    public void BreakInDefendOrder_SatisfiesDefenderJoinGate()
    {
        var settlement = CreateSettlement();
        var siege = CreateSiege(settlement);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        AccessTools.Field(typeof(MobileParty), "_defaultBehavior").SetValue(party, AiBehavior.DefendSettlement);
        AccessTools.Field(typeof(MobileParty), "_targetSettlement").SetValue(party, settlement);

        Assert.True(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, siege));
    }

    private static Settlement CreateSettlement()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement._partiesCache = new MBList<MobileParty>();
        return settlement;
    }

    private static SiegeEvent CreateSiege(Settlement settlement)
    {
        var siege = ObjectHelper.SkipConstructor<SiegeEvent>();
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)).SetValue(siege, settlement);
        return siege;
    }
}
