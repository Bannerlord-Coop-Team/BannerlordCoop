using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.InstanceResolution;

/// <summary>
/// "Ownership-keyed" instance resolution - real instances: BettingFraud, LandLordCompanyOfTrouble,
/// RivalGangMovingIn. Each has its own <c>*InstanceResolutionPatch.cs</c> file that Harmony-prefixes a private
/// static <c>Instance</c> getter (vanilla's own "first found in <c>QuestManager.Quests</c>" fallback) and
/// replaces it with a locally-scoped, unambiguous resolution.
///
/// Purely additive - not consumed by any existing patch; each type keeps its own hand-rolled loop unchanged for
/// now. Rewiring them to call this primitive instead is a separate, lower-risk follow-up.
/// </summary>
public static class OwnershipKeyedInstanceResolution
{
    public static TQuest Resolve<TQuest>() where TQuest : QuestBase =>
        ResolveFrom(Campaign.Current.QuestManager.Quests.OfType<TQuest>());

    /// <summary>The actual selection logic, factored out from <see cref="Resolve{TQuest}"/> so it can be unit
    /// tested against an explicit candidate list instead of requiring a fully bootstrapped
    /// <see cref="Campaign.Current"/>/<c>QuestManager</c> (which real <c>Quest</c> instances can't cheaply be
    /// inserted into outside a real campaign).</summary>
    internal static TQuest ResolveFrom<TQuest>(IEnumerable<TQuest> candidates) where TQuest : QuestBase =>
        candidates.FirstOrDefault(q => q.IsOngoing && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(q.QuestGiver));
}
