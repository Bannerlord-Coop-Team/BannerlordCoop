using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEngineConstructionProgresss.Messages;
using GameInterface.Services.SiegeEnginesContainers.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Patches;

[HarmonyPatch(typeof(SiegeEnginesContainer))]
[HarmonyPatch(MethodType.Constructor)]
[HarmonyPatch(new Type[] { typeof(BattleSideEnum), typeof(SiegeEngineConstructionProgress) })] // Constructor signature
internal class SiegeEnginesContainerCreationPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerCreationPatches>();

    static bool Prefix(ref SiegeEnginesContainer __instance, BattleSideEnum side, SiegeEngineConstructionProgress siegePreparations)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name} with side {side} and siege preparations status {status}\n"
                + "Callstack: {callstack}", typeof(SiegeEnginesContainer), side, siegePreparations.Progress, Environment.StackTrace);
        }

        var siegeEnginesCreateMessage = new SiegeEnginesContainerCreated(__instance, siegePreparations);
        MessageBroker.Instance.Publish(__instance, siegeEnginesCreateMessage);

        //var siegePreparationsCreationMessage = new SiegeEngineConstructionProgressCreated(siegePreparations);
        //MessageBroker.Instance.Publish(siegePreparations, siegePreparationsCreationMessage);

        return true; // Continue with the original constructor
    }
}


//[HarmonyPatch]
//internal class SiegeEnginesContainerCreationPatches
//{
//    static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerCreationPatches>();

//    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEnginesContainer));

//    static bool Prefix(ref SiegeEnginesContainer __instance)
//    {
//        // Call original if we call this function
//        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

//        if (ModInformation.IsClient)
//        {
//            Logger.Error("Client created unmanaged {name}\n"
//                + "Callstack: {callstack}", typeof(SiegeEnginesContainer), Environment.StackTrace);
//            return true;
//        }

//        var message = new SiegeEnginesContainerCreated(__instance);

//        MessageBroker.Instance.Publish(__instance, message);

//        return true;
//    }
//}
