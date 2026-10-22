using Common.Util;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// The quest screen's Old Quests list is the <see cref="JournalLogEntry"/> history that vanilla's
/// <see cref="JournalLogsCampaignBehavior"/> writes, and a joining player downloads the server's copy of it.
/// </summary>
internal static class JournalHistory
{
    public static void Track(params EnvironmentInstance[] instances)
    {
        foreach (var instance in instances)
        {
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());
        }
    }

    /// <summary>The Old Quests list of <c>QuestsVM</c>: every ended <see cref="JournalLogEntry"/> in the history.</summary>
    public static List<string> OldQuestsOn(EnvironmentInstance instance)
    {
        var entries = new List<string>();

        instance.Call(() => entries = Campaign.Current.LogEntryHistory
            .GetGameActionLogs<JournalLogEntry>(_ => true)
            .Where(entry => entry.IsEnded())
            .Select(Describe)
            .ToList());

        return entries;
    }

    /// <summary>
    /// A player who joins or rejoins replaces the history they had with the one in the server's save. The entries are
    /// copied field by field, the way the save system restores them.
    /// </summary>
    public static void DownloadSaveOf(EnvironmentInstance server, EnvironmentInstance joiner)
    {
        var saved = new List<JournalLogEntry>();
        server.Call(() => saved = Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(_ => true).ToList());

        joiner.Call(() =>
        {
            var history = Campaign.Current.LogEntryHistory;
            history._logs.Clear();

            foreach (var entry in saved)
            {
                JournalLogEntry loaded;
                using (new AllowedThread())
                {
                    var relatedObjects = entry._relatedObjectIds.Select(id => new MBObjectBase(id)).ToArray();
                    loaded = new JournalLogEntry(entry.Title, entry.RelatedHero, entry.Antagonist, entry.IsSpecial, relatedObjects)
                    {
                        _lastIssueStatus = entry._lastIssueStatus,
                        _questCompletionDetail = entry._questCompletionDetail,
                        GameTime = entry.GameTime,
                        Id = entry.Id,
                    };
                }

                loaded.Update(entry._entries.ToMBList());

                history._logs.Add(loaded);
            }
        });
    }

    private static string Describe(JournalLogEntry entry) =>
        $"{entry.Title}: {string.Join(" | ", entry.GetEntries().Select(log => log.LogText))}";
}
