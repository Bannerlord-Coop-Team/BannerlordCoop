using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEnginesContainers.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Patches;

[HarmonyPatch()]
internal class SiegeEnginesContainerCreationPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerCreationPatches>();

    private static bool Prefix(ref SiegeEnginesContainer __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(SiegeEnginesContainer), Environment.StackTrace);
            return true;
        }

        var siegeEnginesCreateMessage = new SiegeEnginesContainerCreated(__instance);
        MessageBroker.Instance.Publish(__instance, siegeEnginesCreateMessage);

        return true; // Continue with the original constructor
    }

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEnginesContainer));
}