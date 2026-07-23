using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Stances.Patches
{
    /// <summary>
    /// Vanilla's <c>CheckSettlementSuitabilityForParties</c> (run when a faction's stance changes)
    /// reads <see cref="MobileParty.MapFaction"/> for any party in a settlement, but a not-yet-synced
    /// party on a receiving machine can be in a settlement with a null MapFaction, throwing a
    /// NullReferenceException. Drop those entries (and any null party) before the loop, reading
    /// MapFaction only when CurrentSettlement is set so a party that is not in a settlement (which
    /// vanilla would skip anyway) is left untouched. Runs even under
    /// <see cref="Common.Util.AllowedThread"/> (prefixes always execute); dropped parties are logged
    /// at debug level.
    /// </summary>
    [HarmonyPatch(typeof(PartyDiplomaticHandlerCampaignBehavior), "CheckSettlementSuitabilityForParties")]
    internal class PartyDiplomaticHandlerCampaignBehaviorPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PartyDiplomaticHandlerCampaignBehaviorPatch>();

        private static void Prefix(ref IEnumerable<MobileParty> parties)
        {
            if (parties == null) return;

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

                processable.Add(party);
            }

            parties = processable;
        }
    }
}
