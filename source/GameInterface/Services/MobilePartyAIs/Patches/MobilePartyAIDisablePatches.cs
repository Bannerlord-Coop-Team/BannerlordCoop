using Common;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
internal class MobilePartyAIDisablePatches
{
    [HarmonyPatch(nameof(MobilePartyAi.Tick))]
    [HarmonyPrefix]
    private static bool ClientDisableTickPrefix(MobilePartyAi __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return ModInformation.IsServer || __instance._mobileParty.IsControlledByThisInstance();
    }
}
