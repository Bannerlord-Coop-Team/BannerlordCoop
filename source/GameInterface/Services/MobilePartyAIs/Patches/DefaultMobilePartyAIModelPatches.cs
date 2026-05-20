using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(DefaultMobilePartyAIModel))]
internal class DefaultMobilePartyAIModelPatches
{
    public static readonly ConditionalWeakTable<MobilePartyAi, Dictionary<MobileParty, CampaignTime>> DisablePlayerAttackTimes = new ConditionalWeakTable<MobilePartyAi, Dictionary<MobileParty, CampaignTime>>();

    [HarmonyPatch(nameof(DefaultMobilePartyAIModel.ShouldConsiderAttacking))]
    [HarmonyPostfix]
    private static void ShouldConsiderAttacking_Postfix(
        MobileParty party,
        MobileParty targetParty,
        ref bool __result)
    {
        if (!__result)
            return;

        // TODO test with player parties
        if (targetParty.ShouldBeIgnored)
            __result = false;

        if (!CanAttackTargetParty(party, targetParty))
            __result = false;
    }

    private static bool CanAttackTargetParty(MobileParty party, MobileParty targetParty)
    {
        if (party?.Ai == null || targetParty == null)
            return true;

        if (!DisablePlayerAttackTimes.TryGetValue(party.Ai, out var disableTimes))
            return true;

        if (!disableTimes.TryGetValue(targetParty, out var disableTime))
            return true;

        if (disableTime.IsPast)
        {
            disableTimes.Remove(targetParty);
            return true;
        }

        return false;
    }
}
