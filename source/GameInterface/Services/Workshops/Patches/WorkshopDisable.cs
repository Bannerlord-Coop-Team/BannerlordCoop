using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Patches
{
    [HarmonyPatch(typeof(Workshop))]
    internal class WorkshopDisable
    {
        [HarmonyPatch(nameof(Workshop.AfterLoad))]
        static bool Prefix() => ModInformation.IsServer;
    }
}
