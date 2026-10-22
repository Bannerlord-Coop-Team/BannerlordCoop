using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.UI.Notifications;

internal interface IQuestJournalOwnerResolver
{
    JournalLogEntry CreateOwnedEntry(JournalLogEntry entry, params MBObjectBase[] relatedObjects);
    JournalLogEntry RestoreOwner(JournalLogEntry entry, IssueBase issue);
    bool IsVisible(PlayerJournalLogEntry entry);
}

/// <summary>
/// Uses the substituted player while the server runs an outcome, and the local controller when reading its archive.
/// </summary>
internal sealed class QuestJournalOwnerResolver : IQuestJournalOwnerResolver
{
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controller;

    public QuestJournalOwnerResolver(IPlayerManager playerManager, IControllerIdProvider controller)
    {
        this.playerManager = playerManager;
        this.controller = controller;
    }

    public JournalLogEntry CreateOwnedEntry(JournalLogEntry entry, params MBObjectBase[] relatedObjects)
    {
        var owner = playerManager.TryGetPlayer(Hero.MainHero, out var player) ? player.ControllerId : null;
        return new PlayerJournalLogEntry(entry, owner, relatedObjects);
    }

    public bool IsVisible(PlayerJournalLogEntry entry)
    {
        return !string.IsNullOrEmpty(entry.OwnerControllerId) && entry.OwnerControllerId == controller.ControllerId;
    }

    public JournalLogEntry RestoreOwner(JournalLogEntry entry, IssueBase issue)
    {
        if (entry == null || entry is PlayerJournalLogEntry || !playerManager.TryGetPlayer(Hero.MainHero, out var player))
            return entry;

        var restored = issue.IssueQuest == null
            ? new PlayerJournalLogEntry(entry, player.ControllerId, issue)
            : new PlayerJournalLogEntry(entry, player.ControllerId, issue, issue.IssueQuest);
        var logs = Campaign.Current.LogEntryHistory._logs;
        logs[logs.IndexOf(entry)] = restored;
        return restored;
    }
}
