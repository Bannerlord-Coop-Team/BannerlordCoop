using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerTickPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueManagerTickPatches>();

    [HarmonyPatch(nameof(IssueManager.DailyTick))]
    [HarmonyPrefix]
    private static bool DailyTickPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(IssueManager.DailyTick))]
    [HarmonyFinalizer]
    private static Exception DailyTickFinalizer(Exception __exception)
    {
        if (__exception != null)
        {
            if (Hero.MainHero == null)
            {
                Logger.Warning(__exception,
                    "IssueManager.DailyTick threw on the server, most likely the alternative-solution " +
                    "reward/return path's Hero.MainHero dependency on a dedicated host with no local player - " +
                    "swallowing to keep the server tick alive.");
            }
            else
            {
                Logger.Error(__exception,
                    "IssueManager.DailyTick threw on the server even though Hero.MainHero is not null - this " +
                    "is an unexpected failure and needs investigating. Swallowing to keep the server tick " +
                    "alive regardless.");
            }
        }
        return null;
    }

    [HarmonyPatch(nameof(IssueManager.HourlyTick))]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsClient;
    }
}
