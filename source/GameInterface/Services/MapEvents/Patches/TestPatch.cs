using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEventParty))]
    public class TestPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEventParty.Update))]
        static bool PrefixUpdate()
        {
            return false; //Temporary fix to limit troop roster updates
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEventParty.CommitXpGain))]
        static bool PrefixXp()
        {
            return false; //Temporary fix to limit troop roster updates
        }
    }
}
