using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(PartyBaseHelper))]
    public class TempHasFeatPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("HasFeat")]
        public static bool Prefix(PartyBase party, FeatObject feat, ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}