using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.PartyComponents.Patches.Lifetime;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventLifetimePatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(MapEventSide));

    static bool Prefix(MapEventSide __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return true;
        }

        var message = new MapEventSideCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
