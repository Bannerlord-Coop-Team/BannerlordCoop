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
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventCreationPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventCreationPatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(MapEvent));

    static bool Prefix(MapEvent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return true;
        }

        var message = new MapEventCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
