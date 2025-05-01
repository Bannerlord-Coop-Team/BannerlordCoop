using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class DisableWorkshopsCampaignBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableWorkshopsCampaignBehavior>();

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
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
