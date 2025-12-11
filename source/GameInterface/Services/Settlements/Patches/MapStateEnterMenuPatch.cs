using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(MapState))]
    public class MapStateEnterMenuPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("EnterMenuMode")]
        public static bool Prefix()
        {
            return true;
        }
    }
}
