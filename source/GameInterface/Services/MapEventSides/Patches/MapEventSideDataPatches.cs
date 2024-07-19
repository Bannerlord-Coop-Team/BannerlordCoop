using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventSides.Patches;

[HarmonyPatch(typeof(MapEventSide))]
internal class MapEventSideDataPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDataPatches>();

    [HarmonyPatch(nameof(MapEventSide.LeaderParty), MethodType.Setter)]
    [HarmonyPrefix]
    static bool LeaderPartyPrefix(ref MapEventSide __instance, ref PartyBase value)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return true;
        }

        var message = new MapEventSideMobilePartyChanged(__instance, value.MobileParty);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
