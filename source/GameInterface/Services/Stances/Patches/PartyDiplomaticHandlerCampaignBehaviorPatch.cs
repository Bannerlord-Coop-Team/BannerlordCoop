using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Stances.Patches
{
    /// <summary>
    /// Guards the vanilla diplomatic continuity sweep that runs whenever a faction's stance
    /// changes. Setting a stance fires <c>OnMapEventContinuityNeedsUpdate</c>, whose listener walks
    /// the faction's war parties in <c>CheckSettlementSuitabilityForParties</c>. For each party
    /// vanilla evaluates <c>item.CurrentSettlement == null || !item.MapFaction.IsAtWarWith(...)</c>:
    /// it short-circuits on a null <see cref="MobileParty.CurrentSettlement"/> but, for a party that
    /// IS in a settlement, assumes a non-null <see cref="MobileParty.MapFaction"/>. On a receiving
    /// machine a not-yet-fully-synced party can be sitting in a settlement while its clan, owner and
    /// leader are still unresolved, leaving MapFaction null and crashing the sweep with a
    /// NullReferenceException at <c>item.MapFaction.IsAtWarWith(...)</c>. This runs even on the
    /// receive path under <see cref="Common.Util.AllowedThread"/> because the prefix itself always
    /// executes. Drop only the entries vanilla cannot process — a null party, or a party in a
    /// settlement with no MapFaction — mirroring vanilla's access order so MapFaction is never read
    /// for a roaming party vanilla would skip untouched; every other party flows through unchanged,
    /// so the leave/menu side effects are preserved. A dropped party is logged at debug level so a
    /// faction that stays unresolved (a sync bug, rather than a transient gap) remains visible.
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
                    Logger.Debug("Skipping null party in diplomatic continuity sweep.");
                    continue;
                }

                // Only inspect MapFaction for a party that is actually in a settlement: that is the
                // only case where vanilla reads it, so a roaming party (whose MapFaction getter could
                // itself fault on unresolved data) passes through untouched, exactly as vanilla skips it.
                if (party.CurrentSettlement != null && party.MapFaction == null)
                {
                    Logger.Debug("Skipping party with unresolved MapFaction while in a settlement during diplomatic continuity sweep: {party}", party.StringId);
                    continue;
                }

                processable.Add(party);
            }

            parties = processable;
        }
    }
}
