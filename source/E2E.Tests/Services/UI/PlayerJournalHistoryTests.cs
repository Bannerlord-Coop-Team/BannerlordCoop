using E2E.Tests.Environment;
using GameInterface.Services.Entity;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.UI;

/// <summary>
/// Keeps completed history from saves that predate player journal ownership visible to both clients.
/// </summary>
public class PlayerJournalHistoryTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public PlayerJournalHistoryTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletedEntriesFromOlderSavesRemainVisibleToBothPlayers(bool success)
    {
        LogEntry[] saveHistory = null!;
        environment.Server.Call(() =>
        {
            var entry = new JournalLogEntry(new TextObject("Legacy completed quest"), null, null, false);
            entry.Update(new MBList<JournalLog>
            {
                new JournalLog(CampaignTime.Zero, new TextObject("Completed before ownership was recorded")),
            }, success ? QuestBase.QuestCompleteDetails.Success : QuestBase.QuestCompleteDetails.Fail);
            Campaign.Current.LogEntryHistory._logs.Add(entry);
            saveHistory = Campaign.Current.LogEntryHistory.GameActionLogs.ToArray();
        });

        var player = 0;
        foreach (var client in environment.Clients)
        {
            var controllerId = $"player-{++player}";
            client.Call(() =>
            {
                client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
                Campaign.Current.LogEntryHistory._logs.Clear();
                Campaign.Current.LogEntryHistory._logs.AddRange(saveHistory);
                var entry = Assert.IsType<JournalLogEntry>(Assert.Single(
                    Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(log => log.IsEnded())));
                Assert.Equal("Legacy completed quest", entry.Title.ToString());
                Assert.Equal(!success, entry.IsEndedUnsuccessfully());
                Assert.Equal("Completed before ownership was recorded", Assert.Single(entry.GetEntries()).LogText.ToString());
            });
        }
        Assert.Equal(2, player);
    }
}
