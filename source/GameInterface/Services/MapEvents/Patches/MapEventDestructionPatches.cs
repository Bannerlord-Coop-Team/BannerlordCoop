//using Common;
//using Common.Logging;
//using Common.Messaging;
//using GameInterface.Policies;
//using GameInterface.Services.MapEvents.Messages;
//using HarmonyLib;
//using Serilog;
//using System;
//using TaleWorlds.CampaignSystem.MapEvents;

//namespace GameInterface.Services.MapEvents.Patches;

//[HarmonyPatch(typeof(MapEvent))]
//internal class MapEventDestructionPatches
//{
//    static readonly ILogger Logger = LogManager.GetLogger<MapEventDestructionPatches>();

//    [HarmonyPatch(nameof(MapEvent.FinalizeEventAux))]
//    [HarmonyPrefix]
//    static bool Prefix(MapEvent __instance)
//    {
//        // Call original if we called it
//        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

//        if (ModInformation.IsClient)
//        {
//            Logger.Error("Client destroyed managed {name}", typeof(MapEvent));
//            return false;
//        }

//        var message = new MapEventDestroyed(__instance);

//        MessageBroker.Instance.Publish(__instance, message);

//        return true;
//    }
//}
