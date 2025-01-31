using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.MapEventParties.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(MapEventParty), Environment.StackTrace);
                return false;
            }

            var message = new MapEventPartyCreated(__instance, party);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
