using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventParties.Patches
{
    /// <summary>
    /// Lifetime Patches for MapEventParties
    /// </summary>
    [HarmonyPatch]
    internal class MapEventPartyLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<MapEventPartyLifetimePatches>();

        [HarmonyPatch(typeof(MapEventParty), MethodType.Constructor, typeof(PartyBase))]
        [HarmonyPrefix]
        private static bool CreateMapEventPartyPrefix(ref MapEventParty __instance, PartyBase party)
        {
            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new MapEventPartyCreated(__instance, party);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
