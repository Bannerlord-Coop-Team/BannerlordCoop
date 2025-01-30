using System;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.VillageTypes.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageTypes.Patches;

[HarmonyPatch]
internal class VillageTypeLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<VillageTypeLifetimePatches>();

    [HarmonyPatch(typeof(VillageType), MethodType.Constructor, new Type[] { typeof(string) })]
    [HarmonyPrefix]
    private static bool Prefix(string stringId, VillageType __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(VillageType), Environment.StackTrace);
            return true;
        }

        var message = new VillageTypeCreated(__instance);

        MessageBroker.Instance.Publish(null, message);

        return true;
    }
}