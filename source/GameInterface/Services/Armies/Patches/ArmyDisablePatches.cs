using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Patches
{
    [HarmonyPatch(typeof(Army))]
    class ArmyDisablePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ArmyDisablePatches>();

        [HarmonyPatch(nameof(Army.Tick))]
        [HarmonyPrefix]
        private static bool DisableArmyTick()
        {
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            return true;
        }
    }
}
