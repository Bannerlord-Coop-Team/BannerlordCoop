using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Disables the diplomacy / lifecycle listeners of <see cref="KingdomDecisionProposalBehavior"/>
    /// that drain the unresolved queue via UpdateKingdomDecisions, which cancels invalid decisions
    /// and resolves due ones (StartElectionWithoutPlayer) exactly as the sweep in
    /// <see cref="CoopKingdomDecisionProposalBehaviorPatch"/> does. Leaving them enabled would give
    /// two resolvers for the same queue running re-entrantly: resolving a decision fires a war/peace
    /// event whose listener re-enters UpdateKingdomDecisions and resolves/removes further decisions
    /// mid-resolution. Disabling them makes the sweep the sole resolver. DailyTick (queue pruning)
    /// and OnKingdomDecisionAdded (dedup tracking) are intentionally left enabled.
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
