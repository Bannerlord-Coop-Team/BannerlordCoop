using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventSideCreationPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventSideCreationPatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(MapEventSide));

    static bool Prefix(MapEventSide __instance, MapEvent mapEvent, BattleSideEnum missionSide, PartyBase leaderParty)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return true;
        }

        var message = new MapEventSideCreated(__instance, mapEvent, missionSide, leaderParty);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
