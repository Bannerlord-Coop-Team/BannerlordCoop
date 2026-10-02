using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues;

internal interface ISmugglersQuestOwners
{
    void Set(MBObjectBase quest, Hero player);
    bool TryGet(MBObjectBase quest, out Hero player);
    Dictionary<MBObjectBase, Hero> Snapshot();
    void Restore(Dictionary<MBObjectBase, Hero> entries);
    bool HasActiveCommitment(Hero player);
    void ReplacePlayer(Hero oldPlayer, Hero newPlayer);
}

internal sealed class SmugglersQuestOwners : ISmugglersQuestOwners
{
    private readonly Dictionary<MBObjectBase, Hero> owners = new();

    public void Set(MBObjectBase quest, Hero player)
    {
        if ((quest is SmugglersIssueBehavior.SmugglersIssueQuest || quest is SmugglersIssueBehavior.SmugglersIssue) && player != null)
            owners[quest] = player;
    }

    public bool TryGet(MBObjectBase quest, out Hero player) => owners.TryGetValue(quest, out player);

    public Dictionary<MBObjectBase, Hero> Snapshot() => new(owners);

    public void ReplacePlayer(Hero oldPlayer, Hero newPlayer)
    {
        if (oldPlayer == null || newPlayer == null || oldPlayer == newPlayer) return;
        foreach (var entry in Snapshot())
        {
            if (entry.Value == oldPlayer) owners[entry.Key] = newPlayer;
        }
    }

    public bool HasActiveCommitment(Hero player)
    {
        foreach (var entry in owners)
        {
            if (entry.Value != player) continue;
            if (entry.Key is QuestBase quest && quest.IsOngoing) return true;
            if (entry.Key is IssueBase issue && issue.IsSolvingWithAlternative && issue.IssueOwner?.Issue == issue) return true;
        }
        return false;
    }

    public void Restore(Dictionary<MBObjectBase, Hero> entries)
    {
        owners.Clear();
        if (entries == null) return;
        foreach (var entry in entries) Set(entry.Key, entry.Value);
    }
}
