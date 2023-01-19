using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(PartyAi))]
    static class DisablePartyDecisionMaking
    {
        static readonly AccessTools.FieldRef<PartyAi, MobileParty> m_MobilePartyField =
            AccessTools.FieldRefAccess<PartyAi, MobileParty>("_mobileParty");

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PartyAi.DoNotMakeNewDecisions), MethodType.Setter)]
        static bool PrefixDoNotMakeNewDecisionsSetter(PartyAi __instance, ref bool value)
        {
            //MobileParty party = m_MobilePartyField(__instance);
            //if (party != null && !Coop.IsController(party))
            //{
            //    // Disable decision making for parties our client doesn't control. Decisions are made remote.
            //    value = true;
            //}
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
        static bool Prefix(ref MobileParty owner)
        {
            //if (owner != null && !Coop.IsController(owner))
            //{
            //    return false;
            //}
            return true;
        }
    }
}