using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch]
internal class KingdomLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<KingdomLifetimePatches>();

    [HarmonyPatch(typeof(Kingdom), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool CreateKingdomPrefix(ref Kingdom __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Kingdom), Environment.StackTrace);
            return true;
        }

        var message = new KingdomCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
