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
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        var siegeEnginesCreateMessage = new SiegeEnginesContainerCreated(__instance);
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, siegeEnginesCreateMessage);

        return true; // Continue with the original constructor
    }

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEnginesContainer));
}