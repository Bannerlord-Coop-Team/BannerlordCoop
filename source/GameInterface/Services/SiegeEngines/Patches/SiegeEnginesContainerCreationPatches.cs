using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEnginesContainers.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Patches;

[HarmonyPatch(typeof(SiegeEnginesContainer))]
[HarmonyPatch(MethodType.Constructor)]
[HarmonyPatch(new Type[] { typeof(BattleSideEnum), typeof(SiegeEngineConstructionProgress) })] // Constructor signature
internal class SiegeEnginesContainerCreationPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerCreationPatches>();

    private static bool Prefix(ref SiegeEnginesContainer __instance, BattleSideEnum side, SiegeEngineConstructionProgress siegePreparations)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name} with side {side} and siege preparations status {status}\n"
                + "Callstack: {callstack}", typeof(SiegeEnginesContainer), side, siegePreparations.Progress, Environment.StackTrace);
        }

        var siegeEnginesCreateMessage = new SiegeEnginesContainerCreated(__instance, siegePreparations);
        MessageBroker.Instance.Publish(__instance, siegeEnginesCreateMessage);

        return true; // Continue with the original constructor
    }
}