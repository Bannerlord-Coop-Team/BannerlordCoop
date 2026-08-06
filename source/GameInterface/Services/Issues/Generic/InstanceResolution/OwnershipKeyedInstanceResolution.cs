using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.InstanceResolution;

public interface IOwnershipKeyedInstanceResolution
{
    TQuest Resolve<TQuest>() where TQuest : QuestBase;
}

internal sealed class OwnershipKeyedInstanceResolution : IOwnershipKeyedInstanceResolution
{
    public TQuest Resolve<TQuest>() where TQuest : QuestBase =>
        ResolveFrom(Campaign.Current.QuestManager.Quests.OfType<TQuest>());

    internal static TQuest ResolveFrom<TQuest>(IEnumerable<TQuest> candidates) where TQuest : QuestBase =>
        candidates.FirstOrDefault(q => q.IsOngoing && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(q.QuestGiver));
}
