using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Entity;
using GameInterface.Services.UI.Notifications;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Issues;

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

    internal static JournalLogEntry RestoreVanillaArchive(EnvironmentInstance server, QuestBase quest)
    {
        JournalLogEntry legacy = null!;
        server.Call(() =>
        {
            var issue = IssueManager.GetIssueOfQuest(quest);
            Assert.NotNull(issue);
            Assert.False(string.IsNullOrEmpty(issue.StringId));
            var entry = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                log => log.IsRelatedTo(issue)));
            legacy = new JournalLogEntry(entry.Title, entry.RelatedHero, entry.Antagonist, entry.IsSpecial, issue, quest);
            legacy.Id = entry.Id;
            legacy.GameTime = entry.GameTime;
            legacy._entries = entry._entries;
            legacy._lastIssueStatus = entry._lastIssueStatus;
            legacy._questCompletionDetail = entry._questCompletionDetail;
            var logs = Campaign.Current.LogEntryHistory._logs;
            logs[logs.IndexOf(entry)] = legacy;
            Assert.Contains(issue.StringId, legacy._relatedObjectIds);
        });
        return legacy;
    }

    internal static void AssertLateJoinHistory(EnvironmentInstance server, EnvironmentInstance owner,
        EnvironmentInstance observer, string issueId, QuestBase ownerQuest, IssueOutcome outcome,
        JournalLogEntry? legacy = null)
    {
        Assert.False(string.IsNullOrEmpty(issueId));
        Assert.Equal(outcome, Assert.Single(server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        var ownerControllerId = owner.Resolve<IControllerIdProvider>().ControllerId;
        server.Call(() =>
        {
            var entry = Assert.IsType<PlayerJournalLogEntry>(Assert.Single(
                Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    log => log._relatedObjectIds.Contains(issueId))));
            Assert.Equal(ownerControllerId, entry.OwnerControllerId);
            if (legacy != null)
            {
                Assert.Equal(legacy.Id, entry.Id);
                Assert.Equal(legacy.GameTime, entry.GameTime);
            }
        });
        owner.Call(() => Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => entry._relatedObjectIds.Contains(issueId) && entry.IsEnded())));
        observer.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => entry.IsEnded())));

        LogEntry[] saveHistory = null!;
        server.Call(() => saveHistory = Campaign.Current.LogEntryHistory.GameActionLogs.ToArray());
        observer.Call(() =>
        {
            LoadHistory(saveHistory);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(issueId) && entry.IsEnded()));
        });
        owner.Call(() =>
        {
            // Rejoining replaces the owner's local history with the same server save.
            LoadHistory(saveHistory);
            var entry = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(issueId) && entry.IsEnded()));
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
