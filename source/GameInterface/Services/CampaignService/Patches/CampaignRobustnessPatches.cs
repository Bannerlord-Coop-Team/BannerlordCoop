using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

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
