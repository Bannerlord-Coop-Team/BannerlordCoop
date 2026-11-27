using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Stances.Patches
{
    /// <summary>
    /// Filters the parties vanilla's <c>CheckSettlementSuitabilityForParties</c> checks after a stance change: a null
    /// party, a party in a settlement whose MapFaction isn't synced yet (vanilla would throw), and a player's party
    /// anywhere but its own client, where it is the main party.
    /// </summary>
    [HarmonyPatch(typeof(PartyDiplomaticHandlerCampaignBehavior), "CheckSettlementSuitabilityForParties")]
    internal class PartyDiplomaticHandlerCampaignBehaviorPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PartyDiplomaticHandlerCampaignBehaviorPatch>();

        private static void Prefix(ref IEnumerable<MobileParty> parties)
        {
            if (parties == null) return;

            bool hasPlayerManager = ContainerProvider.TryResolve<IPlayerManager>(out var playerManager);
            var processable = new List<MobileParty>();
            foreach (var party in parties)
            {
                if (party == null)
                {
                    Logger.Debug("Skipping null party while checking settlement suitability after a stance change.");
                    continue;
                }

                // Only read MapFaction for a party that is actually in a settlement: that is the
                // only case where vanilla reads it, so a party that is not in a settlement (whose
                // MapFaction getter could itself fault on unresolved data) passes through untouched,
                // exactly as vanilla skips it.
                if (party.CurrentSettlement != null && party.MapFaction == null)
                {
                    Logger.Debug("Skipping party with unresolved MapFaction while in a settlement after a stance change: {party}", party.StringId);
                    continue;
                }

                // Vanilla keeps the main party in a hostile town and opens the slink away menu, but the server
                // and other clients would eject it like an AI party, even out of a tournament or town mission.
                // An offline player has no client to open that menu, so the server still ejects its party.
                if (party.IsPlayerParty() && !party.IsControlledByThisInstance() &&
                    !(ModInformation.IsServer && hasPlayerManager && playerManager.IsOwnerOfPartyDisconnected(party)))
                {
                    Logger.Debug("Leaving player party {party} to its own client after a stance change.", party.StringId);
                    continue;
                }

                processable.Add(party);
            }

            parties = processable;
        }
    }
}
