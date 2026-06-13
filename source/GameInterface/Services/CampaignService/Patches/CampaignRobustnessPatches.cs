using Common.Logging;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class CampaignRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignRobustnessPatches>();

    [HarmonyPatch(nameof(Campaign.RealTick))]
    [HarmonyFinalizer]
    private static Exception Finalizer_RealTick(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"Campaign.RealTick");
        }

        return null;
    }
}
