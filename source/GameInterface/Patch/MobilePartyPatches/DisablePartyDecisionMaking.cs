using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    [HarmonyPatch()]
    static class DisablePartyDecisionMaking
    {
        static readonly AccessTools.FieldRef<MobilePartyAi, MobileParty> m_MobilePartyField = 
            AccessTools.FieldRefAccess<MobilePartyAi, MobileParty>("_mobileParty");
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MobilePartyAi), nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Setter)]
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