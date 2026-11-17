using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Adapts <see cref="AiMilitaryBehavior"/> settlement targeting to co-op player parties and battles. Its listeners
/// are only registered on the server (<see cref="AiEngagePartyBehaviorPatches"/>).
/// </summary>
[HarmonyPatch(typeof(AiMilitaryBehavior))]
internal class AiMilitaryBehaviorPatches
{
    // Vanilla skips a settlement last attacked by the main party while DoNotAttackMainPartyUntil runs. Co-op skips it
    // while any attack protection in DefaultMobilePartyAIModelPatches runs against the last attacker, per party or faction.
    [HarmonyPatch(nameof(AiMilitaryBehavior.CheckIfSettlementIsSuitableForMilitaryAction))]
    [HarmonyPostfix]
    private static void CheckIfSettlementIsSuitableForMilitaryActionPostfix(Settlement settlement, MobileParty mobileParty, ref bool __result)
    {
        if (!__result) return;

        __result = !DefaultMobilePartyAIModelPatches.IsAttackPrevented(mobileParty, settlement.LastAttackerParty);
    }

    // Drops hourly besiege and defend targets whose siege battle is closed to AI joins, otherwise the party walks up
    // and gets held beside it. Parties already in that siege's camp keep their score so they aren't ejected.
    [HarmonyPatch(nameof(AiMilitaryBehavior.AiHourlyTick))]
    [HarmonyPostfix]
    private static void AiHourlyTickPostfix(MobileParty mobileParty, PartyThinkParams p)
    {
        p._aiBehaviorScores.RemoveAll(score =>
            (score.Item1.AiBehavior == AiBehavior.BesiegeSettlement || score.Item1.AiBehavior == AiBehavior.DefendSettlement) &&
            score.Item1.Party is Settlement target &&
            mobileParty.BesiegedSettlement != target &&
            InteractionPatches.IsSiegeClosedToAiJoins(target));
    }
}
