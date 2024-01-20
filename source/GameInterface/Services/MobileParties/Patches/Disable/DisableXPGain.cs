using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MobileParties.Patches.Disable
{
    [HarmonyPatch(typeof(MapEventParty))]
    internal class DisableXPGain
    {
        [HarmonyPatch(nameof(MapEventParty.CommitXpGain))]
        static bool Prefix() => false;
    }
}
