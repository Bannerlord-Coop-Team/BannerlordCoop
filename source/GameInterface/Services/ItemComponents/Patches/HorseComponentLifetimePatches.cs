using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ItemComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemComponents.Patches;

/// <summary>
/// Harmony patches for the lifetime of a <see cref="HorseComponent"/> object
/// </summary>
[HarmonyPatch]
internal class HorseComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HorseComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(HorseComponent));

    private static bool Prefix(HorseComponent __instance)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        var message = new ItemComponentCreated(__instance);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);

        return true;
    }
}