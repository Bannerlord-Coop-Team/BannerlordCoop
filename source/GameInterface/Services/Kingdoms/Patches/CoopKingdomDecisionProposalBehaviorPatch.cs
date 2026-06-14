using Common;
using Common.Logging;
using GameInterface.Extentions;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Re-enables <see cref="KingdomDecisionProposalBehavior"/> on the server with coop-safe
    /// behaviour. The vanilla behaviour both proposes AI kingdom decisions (in DailyTickClan)
    /// and drains the unresolved queue (in HourlyTick / the diplomacy listeners). Both are
    /// keyed off the player clan / main hero, which do not meaningfully exist on the dedicated
    /// host, so this patch:
    /// <list type="bullet">
    /// <item>allows RegisterEvents only on the server, so clients never propose (they receive
    /// decisions through the existing AddDecision sync);</item>
    /// <item>replaces DailyTickClan with a proposer that drops every player-clan/main-hero
    /// dependency and only proposes war/peace/policy decisions;</item>
    /// <item>replaces HourlyTick with a server authoritative sweep over every kingdom.</item>
    /// </list>
    /// The remaining player-keyed listeners are disabled in
    /// <see cref="DisableKingdomDecisionProposalDiplomacyTicks"/>.
    /// </summary>
    [HarmonyPatch(typeof(KingdomDecisionProposalBehavior))]
    internal class CoopKingdomDecisionProposalBehaviorPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopKingdomDecisionProposalBehaviorPatch>();

        /// <summary>
        /// Vanilla RegisterEvents subscribes both the proposer and the player-keyed sweep.
        /// Allow it only on the server; clients stay fully unsubscribed.
        /// </summary>
        [HarmonyPatch(nameof(KingdomDecisionProposalBehavior.RegisterEvents))]
        [HarmonyPrefix]
        public static bool RegisterEventsPrefix() => ModInformation.IsServer;

        /// <summary>
        /// Coop-safe replacement for the per-clan daily proposer. Mirrors the vanilla logic but
        /// drops the Clan.PlayerClan / Hero.MainHero dependencies and proposes only war/peace/policy.
        /// Proposed decisions flow through the live Kingdom.AddDecision funnel, which resolves
        /// player-free kingdoms immediately and queues the rest for the hourly sweep.
        /// </summary>
        [HarmonyPatch("DailyTickClan")]
        [HarmonyPrefix]
        public static bool DailyTickClanPrefix(KingdomDecisionProposalBehavior __instance, Clan clan)
        {
            if (ModInformation.IsClient) return false;

            // Co-op equivalent of vanilla's `clan == Clan.PlayerClan` skip: never auto-propose for
            // a connected player's own clan. Clan.PlayerClan is the vestigial launcher clan on the
            // dedicated host, so the player set is the replicated GetPlayerMobileParties registry.
            // Proposing here would spend the player's influence and author a decision in their name.
            if (Campaign.Current.CampaignObjectManager.GetPlayerMobileParties().Any(party => party.ActualClan == clan))
            {
                return false;
            }

            if ((int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow < 5
                || clan.IsEliminated
                || clan.CurrentTotalStrength <= 0f
                || clan.IsBanditFaction
                || clan.Kingdom == null
                || clan.Influence < 100f)
            {
                return false;
            }

            KingdomDecision kingdomDecision = null;
            float randomFloat = MBRandom.RandomFloat;
            int activeClanCount = ((Kingdom)clan.MapFaction).Clans.Count(x => x.Influence > 100f);
            // Vanilla scales this chance by a Hero.MainHero.MapFaction factor; dropped because the
            // dedicated host has no real player faction to down-weight.
            float chance = MathF.Min(0.33f, 1f / (activeClanCount + 2f));
            var diplomacyModel = Campaign.Current.Models.DiplomacyModel;

            // The GetRandom*Decision factories are private in vanilla; GameInterface publicizes
            // TaleWorlds.CampaignSystem, so they are called directly here.
            if (randomFloat < chance && clan.Influence > diplomacyModel.GetInfluenceCostOfProposingPeace(clan))
            {
                kingdomDecision = __instance.GetRandomPeaceDecision(clan);
            }
            else if (randomFloat < chance * 2f && clan.Influence > diplomacyModel.GetInfluenceCostOfProposingWar(clan))
            {
                kingdomDecision = __instance.GetRandomWarDecision(clan);
            }
            else if (randomFloat < chance * 2.5f)
            {
                // Trade-agreement / alliance band — not synced; preserved as a no-op so the
                // remaining decision types keep their vanilla probabilities.
                return false;
            }
            else if (randomFloat < chance * 2.75f
                && clan.Influence > diplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(clan) * 4)
            {
                kingdomDecision = __instance.GetRandomPolicyDecision(clan);
            }
            // Vanilla's annexation band (randomFloat < chance * 3) is also not synced and
            // intentionally omitted, so AI kingdoms do not propose settlement-annexation decisions.

            if (kingdomDecision == null) return false;

            // Cross-kingdom duplicate guard (vanilla, player-deref-free): skip if an equivalent
            // war/peace decision is already pending.
            foreach (KingdomDecision existing in __instance._kingdomDecisionsList)
            {
                if (existing is DeclareWarDecision existingWar && kingdomDecision is DeclareWarDecision newWar
                    && existingWar.FactionToDeclareWarOn == newWar.FactionToDeclareWarOn
                    && existingWar.ProposerClan.MapFaction == newWar.ProposerClan.MapFaction)
                {
                    return false;
                }
                if (existing is MakePeaceKingdomDecision existingPeace && kingdomDecision is MakePeaceKingdomDecision newPeace
                    && existingPeace.FactionToMakePeaceWith == newPeace.FactionToMakePeaceWith
                    && existingPeace.ProposerClan.MapFaction == newPeace.ProposerClan.MapFaction)
                {
                    return false;
                }
            }

            clan.Kingdom.AddDecision(kingdomDecision);
            return false;
        }

        /// <summary>
        /// Server authoritative replacement for the vanilla hourly sweep. Drains every kingdom's
        /// unresolved queue: cancels invalid decisions and AI-resolves due ones. Removals and
        /// outcomes replicate to clients through the existing patched paths.
        /// </summary>
        [HarmonyPatch("HourlyTick")]
        [HarmonyPrefix]
        public static bool HourlyTickPrefix()
        {
            if (ModInformation.IsClient) return false;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom?._unresolvedDecisions == null || kingdom._unresolvedDecisions.Count == 0) continue;

                // Snapshot: RemoveDecision / ApplyChosenOutcome mutate the list during iteration.
                foreach (KingdomDecision decision in kingdom.UnresolvedDecisions.ToList())
                {
                    try
                    {
                        if (decision.ShouldBeCancelled())
                        {
                            kingdom.RemoveDecision(decision);
                            bool isPlayerInvolved =
                                (decision.DetermineChooser()?.Leader?.IsHumanPlayerCharacter ?? false)
                                || decision.DetermineSupporters().Any(supporter => supporter.IsPlayer);
                            CampaignEventDispatcher.Instance.OnKingdomDecisionCancelled(decision, isPlayerInvolved);
                        }
                        else if (decision.TriggerTime.IsPast)
                        {
                            // Resolves AI-side; ApplyChosenOutcome calls Kingdom.RemoveDecision,
                            // which replicates the removal by index through the live patch.
                            new KingdomElection(decision).StartElectionWithoutPlayer();

                            // Guaranteed drain: an election that did not conclude (e.g.
                            // OnShowDecision returned false) leaves the decision queued. Force
                            // its removal so the queue cannot wedge.
                            if (kingdom._unresolvedDecisions.Contains(decision))
                            {
                                kingdom.RemoveDecision(decision);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Isolate one bad decision so it cannot fault the whole tick. Leave it queued
                        // for the next sweep rather than force-removing it here — a failure while
                        // merely classifying a not-yet-due decision must not drop it.
                        Logger.Error(ex, "Failed to resolve kingdom decision {Decision} for kingdom {Kingdom}; leaving it queued.",
                            decision?.GetType().Name, kingdom.StringId);
                    }
                }
            }

            return false;
        }
    }
}
