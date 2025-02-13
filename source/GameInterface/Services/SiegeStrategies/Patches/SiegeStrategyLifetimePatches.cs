using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeStrategies.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeStrategies.Patches;

[HarmonyPatch]
internal class SiegeStrategyLifetimePatches
{
    static ILogger Logger = LogManager.GetLogger<SiegeStrategyLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeStrategy));

    [HarmonyPrefix]
    static bool Prefix(ref SiegeStrategy __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);

            return false;
        }

        var message = new SiegeStrategyCreated(__instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}