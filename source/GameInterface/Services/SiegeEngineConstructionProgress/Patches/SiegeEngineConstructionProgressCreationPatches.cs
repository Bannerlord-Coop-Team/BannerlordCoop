using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEngineConstructionProgresss.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Patches;

[HarmonyPatch]
internal class SiegeEngineConstructionProgressCreationPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineConstructionProgressCreationPatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEngineConstructionProgress));

    private static bool Prefix(ref SiegeEngineConstructionProgress __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(SiegeEngineConstructionProgress), Environment.StackTrace);
            return true;
        }

        var message = new SiegeEngineConstructionProgressCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}