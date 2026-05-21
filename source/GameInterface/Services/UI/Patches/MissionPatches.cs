using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(Mission))]
internal class MissionPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MissionPatches>();

    [HarmonyPatch(nameof(Mission.OnTick))]
    [HarmonyPostfix]
    static void Postfix()
    {
        if (LoadingWindow.IsLoadingWindowActive)
            LoadingWindow.DisableGlobalLoadingWindow();
    }
}