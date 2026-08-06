using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Generic.InstanceResolution;

/// <summary>
/// "Location-keyed" instance resolution - real instances: LordNeedsGarrisonTroops (no fallback),
/// MerchantArmyOfPoachers (first-ongoing fallback). Purely additive, like
/// <see cref="OwnershipKeyedInstanceResolution"/>: not yet consumed by either type's existing patch.
/// </summary>
public static class LocationKeyedInstanceResolution
{
    /// <summary>LordNeedsGarrisonTroops (<paramref name="preserveVanillaFallback"/>: false) and
    /// MerchantArmyOfPoachers (<paramref name="preserveVanillaFallback"/>: true) are the SAME loop with one
    /// boolean toggle - not two primitives. The parameter is required, not defaulted: the two real call sites
    /// made OPPOSITE, deliberate choices about fallback behavior.</summary>
    public static TQuest Resolve<TQuest>(
        Func<TQuest, Settlement> locationSelector, Settlement currentSettlement, bool preserveVanillaFallback)
        where TQuest : QuestBase =>
        ResolveFrom(Campaign.Current.QuestManager.Quests.OfType<TQuest>(), locationSelector, currentSettlement, preserveVanillaFallback);

    /// <summary>The actual selection loop, factored out from <see cref="Resolve{TQuest}"/> so it can be unit
    /// tested against an explicit candidate list - see <see cref="OwnershipKeyedInstanceResolution.ResolveFrom{TQuest}"/>'s
    /// own doc comment for why.</summary>
    internal static TQuest ResolveFrom<TQuest>(
        IEnumerable<TQuest> candidates, Func<TQuest, Settlement> locationSelector, Settlement currentSettlement, bool preserveVanillaFallback)
        where TQuest : QuestBase
    {
        TQuest fallback = null;
        foreach (var quest in candidates)
        {
            if (!quest.IsOngoing) continue;
            fallback ??= quest;
            if (currentSettlement != null && locationSelector(quest) == currentSettlement) return quest;
        }
        return preserveVanillaFallback ? fallback : null;
    }
}
