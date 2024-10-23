using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch]
internal class VillageLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<VillageLifetimePatches>();

    [HarmonyPatch(typeof(Village), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool Prefix(Village __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Village), Environment.StackTrace);
            return true;
        }

        var message = new VillageCreated(__instance);

        MessageBroker.Instance.Publish(null, message);

        return true;
    }
}