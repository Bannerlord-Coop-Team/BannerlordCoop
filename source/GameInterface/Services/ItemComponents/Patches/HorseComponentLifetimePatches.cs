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
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(HorseComponent), Environment.StackTrace);
            return true;
        }

        var message = new ItemComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}