using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEventManager))]
    public class DisableSiegeMapEvent
    {
        private static readonly ILogger Logger = LogManager.GetLogger<DisableSiegeMapEvent>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEventManager.StartSiegeMapEvent))]
        static bool Prefix() //TODO Enable Siege
        {
            return false;
        }
    }
}