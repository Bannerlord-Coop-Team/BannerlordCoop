using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Generic.InstanceResolution;

// Not yet wired into any existing patch.
public static class LocationKeyedInstanceResolution
{
    public static TQuest Resolve<TQuest>(
        Func<TQuest, Settlement> locationSelector, Settlement currentSettlement, bool preserveVanillaFallback)
        where TQuest : QuestBase =>
        ResolveFrom(Campaign.Current.QuestManager.Quests.OfType<TQuest>(), locationSelector, currentSettlement, preserveVanillaFallback);

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
