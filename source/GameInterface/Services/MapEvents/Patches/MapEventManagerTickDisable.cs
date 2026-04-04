//using Common;
//using Common.Logging;
//using GameInterface.Policies;
//using HarmonyLib;
//using Serilog;
//using System;
//using TaleWorlds.CampaignSystem.MapEvents;

//namespace GameInterface.Services.MapEvents.Patches;

//[HarmonyPatch(typeof(MapEventManager))]
//internal class MapEventManagerTickDisable
//{
//    private static readonly ILogger Logger = LogManager.GetLogger<MapEventCollectionPatches>();

//    [HarmonyPatch(nameof(MapEventManager.Tick))]
//    [HarmonyPrefix]
//    static bool Prefix() => ModInformation.IsServer;
//}