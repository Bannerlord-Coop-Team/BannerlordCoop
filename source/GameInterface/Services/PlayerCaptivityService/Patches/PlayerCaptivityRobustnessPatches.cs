using Common.Logging;
using HarmonyLib;
using SandBox.ViewModelCollection.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class PlayerCaptivityRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityRobustnessPatches>();

    [HarmonyPatch(typeof(PlayerCaptivity), nameof(PlayerCaptivity.Update))]
    [HarmonyFinalizer]
    private static Exception Finalizer_UpdateMapEventsAux(Exception __exception, MethodBase __originalMethod)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"{__originalMethod.DeclaringType}.{__originalMethod.Name}");
        }

        return null;
    }
}
