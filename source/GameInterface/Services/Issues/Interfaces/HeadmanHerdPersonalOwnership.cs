using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

public interface IHeadmanHerdPersonalOwnership
{
    void RecordCurrentOwner(Issue issue, Quest quest = null);
    void ForgetUnacceptedIssue(Issue issue);
    void SetOwner(string objectId, string controllerId);
    bool IsLocalOwner(string objectId);
    bool IsVisible(MBObjectBase subject);
    bool IsVisible(JournalLogEntry history);
    bool IsVisibleToCurrentPlayer(IssueBase issue);
    IReadOnlyCollection<KeyValuePair<string, string>> Snapshot();
    void Restore(IEnumerable<KeyValuePair<string, string>> entries);
}

internal sealed class HeadmanHerdPersonalOwnership : IHeadmanHerdPersonalOwnership
{
    private readonly ConcurrentDictionary<string, string> owners = new();
    private readonly IControllerIdProvider localController;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public HeadmanHerdPersonalOwnership(IControllerIdProvider localController, IPlayerManager players, IObjectManager objects)
    {
        this.localController = localController;
        this.players = players;
        this.objects = objects;
    }

    public void RecordCurrentOwner(Issue issue, Quest quest = null)
    {
        var controllerId = CurrentControllerId();
        if (controllerId == null)
            throw new InvalidOperationException("Deliver the Herd accepting player is not registered");
        SetOwner(issue.StringId, controllerId);
        if (quest != null) SetOwner(quest.StringId, controllerId);
    }

    public void SetOwner(string objectId, string controllerId)
    {
        if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(controllerId))
            throw new ArgumentException("Quest identity and controller are required");
        if (!owners.TryAdd(objectId, controllerId) && owners[objectId] != controllerId)
            throw new InvalidOperationException("Quest personal ownership cannot change");
    }

    public void ForgetUnacceptedIssue(Issue issue)
    {
        if (issue.IsOngoingWithoutQuest && issue.IssueQuest == null
            && owners.TryGetValue(issue.StringId, out var owner) && owner == CurrentControllerId())
            owners.TryRemove(issue.StringId, out _);
    }

    public bool IsLocalOwner(string objectId)
        => objectId != null && owners.TryGetValue(objectId, out var owner) && owner == localController.ControllerId;

    public bool IsVisible(MBObjectBase subject)
        => subject is not Issue && subject is not Quest || IsLocalOwner(subject.StringId);

    public bool IsVisible(JournalLogEntry history)
    {
        foreach (var id in history._relatedObjectIds)
        {
            if (owners.TryGetValue(id, out var owner)) return owner == localController.ControllerId;
        }
        return true;
    }

    private string CurrentControllerId()
    {
        if (!objects.TryGetId(Hero.MainHero, out var heroId)) return null;
        return players.Players.FirstOrDefault(candidate => candidate.HeroId == heroId)?.ControllerId;
    }

    public bool IsVisibleToCurrentPlayer(IssueBase issue)
    {
        if (issue is not Issue || !owners.TryGetValue(issue.StringId, out var owner)) return true;
        var controllerId = CurrentControllerId();
        return controllerId == null || owner == controllerId;
    }

    public IReadOnlyCollection<KeyValuePair<string, string>> Snapshot() => owners.ToArray();

    public void Restore(IEnumerable<KeyValuePair<string, string>> entries)
    {
        owners.Clear();
        foreach (var entry in entries)
            SetOwner(entry.Key, entry.Value);
    }
}
