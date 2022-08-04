using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    [HarmonyPatch()]
    static class DisablePartyDecisionMaking
    {
        static readonly AccessTools.FieldRef<PartyAi, MobileParty> m_MobilePartyField = 
            AccessTools.FieldRefAccess<PartyAi, MobileParty>("_mobileParty");
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PartyAi), nameof(PartyAi.DoNotMakeNewDecisions), MethodType.Setter)]
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