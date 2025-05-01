using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Hideouts.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Patches;

[HarmonyPatch]
internal class HideoutLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<HideoutLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(Hideout));

    private static bool Prefix(Hideout __instance)
    {
        // Run original if we called it
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        var message = new HideoutCreated(__instance);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, message);

        return true;
    }
}
