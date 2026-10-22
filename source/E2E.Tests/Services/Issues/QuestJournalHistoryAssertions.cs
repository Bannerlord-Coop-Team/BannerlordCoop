using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.ObjectSystem;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Exercises the completed journal history a joining player downloads from the server.
/// </summary>
internal static class QuestJournalHistoryAssertions
{
    internal static void RegisterJournalBehavior(IEnumerable<EnvironmentInstance> instances)
    {
        foreach (var instance in instances)
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());
    }

    internal static void RestoreVanillaArchive(EnvironmentInstance server, QuestBase quest)
    {
        server.Call(() =>
        {
            var entry = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                log => log.IsRelatedTo(quest)));
            var legacy = new JournalLogEntry(entry.Title, entry.RelatedHero, entry.Antagonist, entry.IsSpecial,
                entry._relatedObjectIds.Select(id => new MBObjectBase(id)).ToArray());
            legacy.Id = entry.Id;
            legacy.GameTime = entry.GameTime;
            legacy._entries = entry._entries;
            legacy._lastIssueStatus = entry._lastIssueStatus;
            legacy._questCompletionDetail = entry._questCompletionDetail;
            var logs = Campaign.Current.LogEntryHistory._logs;
            logs[logs.IndexOf(entry)] = legacy;
        });
    }

    internal static void AssertLateJoinHistory(EnvironmentInstance server, EnvironmentInstance owner,
        EnvironmentInstance observer, QuestBase serverQuest, QuestBase ownerQuest, IssueOutcome outcome)
    {
        Assert.Equal(outcome, Assert.Single(server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        owner.Call(() => Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => entry.IsRelatedTo(ownerQuest) && entry.IsEnded())));
        observer.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => entry.IsEnded())));

        LogEntry[] saveHistory = null!;
        server.Call(() => saveHistory = Campaign.Current.LogEntryHistory.GameActionLogs.ToArray());
        observer.Call(() =>
        {
            LoadHistory(saveHistory);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(serverQuest) && entry.IsEnded()));
        });
        owner.Call(() =>
        {
            // Rejoining replaces the owner's local history with the same server save.
            LoadHistory(saveHistory);
            var entry = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(serverQuest) && entry.IsEnded()));
            Assert.Equal(ownerQuest.Title.ToString(), entry.Title.ToString());
            Assert.Equal(outcome == IssueOutcome.QuestFail, entry.IsEndedUnsuccessfully());
        });
    }

    private static void LoadHistory(LogEntry[] saveHistory)
    {
        Campaign.Current.LogEntryHistory._logs.Clear();
        Campaign.Current.LogEntryHistory._logs.AddRange(saveHistory);
    }
}
