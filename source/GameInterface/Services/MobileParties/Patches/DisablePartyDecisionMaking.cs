using Coop.Mod.Extentions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
static class DisablePartyDecisionMaking
{
    static readonly AccessTools.FieldRef<MobilePartyAi, MobileParty> m_MobilePartyField =
        AccessTools.FieldRefAccess<MobilePartyAi, MobileParty>("_mobileParty");

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Setter)]
    static bool PrefixDoNotMakeNewDecisionsSetter(MobilePartyAi __instance, ref bool value)
    {
        //MobileParty party = m_MobilePartyField(__instance);
        //if (party != null && !Coop.IsController(party))
        //{
        //    // Disable decision making for parties our client doesn't control. Decisions are made remote.
        //    value = true;
        //}
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Getter)]
    static void PostfixDoNotMakeNewDecisionsGetter(MobilePartyAi __instance, ref bool __result)
    {
        // TODO allow decision making for controlled parties and sync
        __result = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
    static bool Prefix(ref MobileParty owner)
    {
        //if (owner != null && !Coop.IsController(owner))
        //{
        //    return false;
        //}
        return false;
    }
}