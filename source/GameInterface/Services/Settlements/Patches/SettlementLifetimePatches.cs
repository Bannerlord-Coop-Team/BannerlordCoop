using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch]
internal class SettlementLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<SettlementLifetimePatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(Settlement));

    static bool Prefix(ref Settlement __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        var message = new SettlementCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
