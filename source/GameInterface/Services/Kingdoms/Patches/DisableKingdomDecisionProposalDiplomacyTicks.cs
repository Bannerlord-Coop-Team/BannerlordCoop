using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Disables the diplomacy / lifecycle listeners of <see cref="KingdomDecisionProposalBehavior"/>
    /// that drain the unresolved queue via the vanilla, player-keyed UpdateKingdomDecisions
    /// (which uses the player-driven election path). The sweep in
    /// <see cref="CoopKingdomDecisionProposalBehaviorPatch"/> is the sole authority for draining.
    /// DailyTick (queue pruning) and OnKingdomDecisionAdded (dedup tracking) are intentionally
    /// left enabled.
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
