using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ItemRosters.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch]
internal class ItemRosterLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<ItemRosterLifetimePatches>();

    [HarmonyPatch(typeof(ItemRoster), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool Prefix(ItemRoster __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(ItemRoster), Environment.StackTrace);
            return true;
        }

        var message = new ItemRosterCreated(__instance);

        MessageBroker.Instance.Publish(null, message);

        return true;
    }
}