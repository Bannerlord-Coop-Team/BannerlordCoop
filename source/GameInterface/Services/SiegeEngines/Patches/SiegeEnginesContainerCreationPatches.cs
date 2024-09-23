using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEnginesContainers.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Patches;

[HarmonyPatch]
internal class SiegeEnginesContainerCreationPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerCreationPatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEnginesContainer));

    static bool Prefix(ref SiegeEnginesContainer __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(SiegeEnginesContainer), Environment.StackTrace);
            return true;
        }

        var message = new SiegeEnginesContainerCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
