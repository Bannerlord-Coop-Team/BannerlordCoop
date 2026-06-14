using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Disables the diplomacy / lifecycle listeners of <see cref="KingdomDecisionProposalBehavior"/>
    /// that drain a kingdom's decision queue via UpdateKingdomDecisions. Vanilla's drain keeps a
    /// decision a player in the kingdom must vote on queued (with a deadline) and only AI-resolves
    /// the rest. That player-vote path is out of scope here (restored in #1379), so every decision
    /// instead flows through the no-player resolve path: the sweep in
    /// <see cref="CoopKingdomDecisionProposalBehaviorPatch"/>, which resolves via StartElectionWithoutPlayer.
    /// Disabling these also keeps that sweep the sole resolver; leaving them enabled would double-resolve
    /// re-entrantly through the war/peace events the sweep fires. DailyTick (queue pruning) and
    /// OnKingdomDecisionAdded (dedup tracking) are intentionally left enabled.
    /// </summary>
    [HarmonyPatch]
    internal class DisableKingdomDecisionProposalDiplomacyTicks
    {
        private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
        {
            AccessTools.Method(typeof(KingdomDecisionProposalBehavior), "OnPeaceMade"),
            AccessTools.Method(typeof(KingdomDecisionProposalBehavior), "OnWarDeclared"),
            AccessTools.Method(typeof(KingdomDecisionProposalBehavior), "OnKingdomDestroyed"),
            AccessTools.Method(typeof(KingdomDecisionProposalBehavior), "OnClanChangedKingdom"),
        };

        static bool Prefix() => false;
    }
}
