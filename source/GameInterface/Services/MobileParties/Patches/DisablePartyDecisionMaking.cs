using GameInterface.Extentions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
static class DisablePartyDecisionMaking
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Getter)]
    static void PostfixDoNotMakeNewDecisionsGetter(MobilePartyAi __instance, ref bool __result)
    {
        MobileParty party = __instance.GetMobileParty();
        if (party != null && !party.IsControlled())
        {
            // Disable decision making for parties our client doesn't control. Decisions are made remote.
            __result = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
    static bool Prefix(ref MobileParty owner)
    {
        if (owner != null && !owner.IsControlled())
        {
            return false;
        }

        return true;
    }

    
}