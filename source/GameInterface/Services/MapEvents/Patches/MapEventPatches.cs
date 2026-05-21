using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Patches;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventPatches>();

    [HarmonyPatch(nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPrefix]
    private static void PrefixAddInvolvedPartyInternal(MapEvent __instance, MapEventParty mapEventParty)
    {
        // Parties not controlled by the server are player parties
        if (mapEventParty.Party.MobileParty.IsPlayerParty())
        {
            var partiesAdded = new List<MapEventParty>();

            __instance.TroopUpgradeTracker = new TroopUpgradeTracker();
            MapEventSide[] sides = __instance._sides;
            for (int i = 0; i < sides.Length; i++)
            {
                foreach (var existingParty in sides[i].Parties)
                {
                    __instance.TroopUpgradeTracker.AddParty(existingParty);
                    partiesAdded.Add(existingParty);
                }
            }

            var message = new MapEventInvolvedPartiesAdded(__instance, partiesAdded);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
