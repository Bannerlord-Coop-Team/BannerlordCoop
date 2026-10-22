using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;

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

    public static List<string> EntriesOn(EnvironmentInstance instance)
    {
        var entries = new List<string>();

        instance.Call(() => entries = Campaign.Current.LogEntryHistory
            .GetGameActionLogs<JournalLogEntry>(_ => true)
            .Select(entry => $"{entry.Title}: {string.Join(" | ", entry.GetEntries().Select(log => log.LogText))}")
            .ToList());

        return entries;
    }
}
