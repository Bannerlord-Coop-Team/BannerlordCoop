using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.InstanceResolution;

// Not yet wired into any existing patch.
public static class OwnershipKeyedInstanceResolution
{
    public static TQuest Resolve<TQuest>() where TQuest : QuestBase =>
        ResolveFrom(Campaign.Current.QuestManager.Quests.OfType<TQuest>());

    internal static TQuest ResolveFrom<TQuest>(IEnumerable<TQuest> candidates) where TQuest : QuestBase =>
        candidates.FirstOrDefault(q => q.IsOngoing && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(q.QuestGiver));
}
