using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignPeriodicEventManager))]
internal class PartyTickPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyTickPatch>();

    [HarmonyPatch(nameof(CampaignPeriodicEventManager.TickPeriodicEvents))]
    [HarmonyPrefix]
    static bool TickPeriodicEventsPrefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }

    [HarmonyPatch(nameof(CampaignPeriodicEventManager.MobilePartyHourlyTick))]
    [HarmonyPrefix]
    static bool MobilePartyHourlyTickPrefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}
