using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Used to sync number of enemies and allies spotted around.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class EntitiesSpottedSettlementPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<EntitiesSpottedSettlementPatch>();

    [HarmonyPatch(nameof(Settlement.NumberOfEnemiesSpottedAround), MethodType.Setter)]
    [HarmonyPrefix]
    static bool NumberEnemiesSpottedPrefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }

    [HarmonyPatch(nameof(Settlement.NumberOfAlliesSpottedAround), MethodType.Setter)]
    [HarmonyPrefix]
    static bool NumberAlliesSpottedPrefix()
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
