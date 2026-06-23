using Common.Logging;
using HarmonyLib;
using SandBox.View.Missions;
using SandBox.ViewModelCollection.Nameplate;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.GauntletUI.Data;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class RobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<RobustnessPatches>();

    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(GauntletMovie), nameof(GauntletMovie.LoadMovie)),
        AccessTools.Method(typeof(MissionAudienceHandler), nameof(MissionAudienceHandler.SpawnAudienceAgents)),
        AccessTools.Method(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshDynamicProperties))
    };

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, MethodBase __originalMethod)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"{__originalMethod.DeclaringType}.{__originalMethod.Name}");
        }

        return null;
    }
}
