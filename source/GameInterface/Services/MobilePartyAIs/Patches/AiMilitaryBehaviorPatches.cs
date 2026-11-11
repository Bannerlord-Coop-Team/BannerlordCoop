using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Makes the main party checks of <see cref="AiMilitaryBehavior"/> read co-op player parties. Its listeners
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
}
