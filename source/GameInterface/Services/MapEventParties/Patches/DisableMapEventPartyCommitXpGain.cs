using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Patches
{
    [HarmonyPatch(typeof(MapEventParty))]
    internal class DisableMapEventPartyCommitXpGain
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEventParty.CommitXpGain))]
        private static bool Prefix()
        {
            return false;
        }
    }
}
