using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Towns.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch]
internal class TownLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<TownLifetimePatches>();

    [HarmonyPatch(typeof(Town), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool Prefix(Town __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Town), Environment.StackTrace);
            return true;
        }

        var message = new TownCreated(__instance);

        MessageBroker.Instance.Publish(null, message);

        return true;
    }
}