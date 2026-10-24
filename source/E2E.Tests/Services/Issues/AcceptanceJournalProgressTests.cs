using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class AcceptanceJournalProgressTests : GangLeaderNeedsWeaponsAcceptScenario
{
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));
    private readonly ITestOutputHelper output;

    public AcceptanceJournalProgressTests(ITestOutputHelper output) : base(output)
    {
        this.output = output;
        foreach (var instance in Instances)
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());
    }

    public override void Dispose()
    {
        try { base.Dispose(); }
        finally { preconditions.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CarriedWeaponsRetainFinalAcceptanceProgressIncludingAnEndingInsideTheCallback(bool endInsideCallback)
    {
        var (ownerQuest, ownerLog) = AcceptCarriedWeapons(endInsideCallback);
        var sent = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        Assert.Equal(FirstGiverId, sent.IssueOwnerId);
        Assert.Equal(FirstIssueId, sent.IssueId);
        Assert.Equal(0, sent.Index);
        Assert.Equal(3, sent.Progress);
        AssertMetadata(sent, ownerLog);
        output.WriteLine("Owner progress: {0}; captured progress: {1}", ownerLog.CurrentProgress, sent.Progress);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            var serverLog = endInsideCallback
                ? Assert.Single(Archive().GetEntries())
                : Assert.Single(giver.Issue.IssueQuest.JournalEntries);
            AssertMetadata(sent, serverLog);
            output.WriteLine("Server progress: {0}", serverLog.CurrentProgress);
        });

        if (!endInsideCallback)
        {
            ClientA.Call(() => ownerQuest.CompleteQuestWithCancel());
            TestEnvironment.FlushCoalescer();
        }
        Assert.Equal(sent, Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>()));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        AssertSavedHistory(new[] { ownerLog });
    }

    [Fact]
    public void LaterProgressMirrorsWithoutRecapturingAcceptanceAndSurvivesSaveAndLoad()
    {
        var (ownerQuest, ownerLog) = AcceptCarriedWeapons(false);
        var acceptance = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        Assert.Equal(3, acceptance.Progress);
        AssertMetadata(acceptance, ownerLog);
        var mirrorCount = ClientA.NetworkSentMessages.GetMessages<RequestQuestJournal>().Count();
        JournalLog[] expected = null!;
        ClientA.Call(() =>
        {
            ownerLog.UpdateCurrentProgress(7);
            ownerQuest.AddLog(new TextObject("Later progress before the next campaign tick"));
            expected = ownerQuest.JournalEntries.ToArray();
            Assert.Equal(2, expected.Length);
            Assert.Equal(7, expected[0].CurrentProgress);
            Assert.Equal("Later progress before the next campaign tick", expected[1].LogText.ToString());
        });
        TestEnvironment.FlushCoalescer();
        Assert.Equal(acceptance, Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>()));
        var mirrors = ClientA.NetworkSentMessages.GetMessages<RequestQuestJournal>().Skip(mirrorCount).ToArray();
        Assert.Equal(2, mirrors.Length);
        Assert.All(mirrors, mirror =>
        {
            Assert.Equal(FirstGiverId, mirror.IssueOwnerId);
            Assert.Equal(FirstIssueId, mirror.IssueId);
        });
        AssertMetadata(expected[0], Assert.Single(mirrors[0].Entries).ToJournalLog());
        AssertEntries(expected, mirrors[1].Entries.Select(entry => entry.ToJournalLog()));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            AssertEntries(expected, giver.Issue.IssueQuest.JournalEntries);
        });
        ClientA.Call(() => ownerQuest.CompleteQuestWithCancel());
        TestEnvironment.FlushCoalescer();
        Assert.Equal(acceptance, Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>()));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        ClientA.Call(() =>
        {
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog(
                (JournalLogEntry entry) => entry.IsRelatedTo(ownerQuest));
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            AssertEntries(expected, archive.GetEntries());
        });
        ClientB.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => entry._relatedObjectIds.Contains(FirstIssueId))));
        AssertSavedHistory(expected);
    }

    private (Quest Quest, JournalLog Log) AcceptCarriedWeapons(bool endInsideCallback)
    {
        Quest ownerQuest = null!;
        JournalLog ownerLog = null!;
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));
            using (new AllowedThread())
                party.ItemRoster.AddToCounts(GangLeaderNeedsWeaponsTests.CreateAxe(), 3);
        });
        Accept(ClientA, FirstGiverId);
        TestEnvironment.FlushCoalescer();
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var hero));
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));
            ownerQuest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            using (new MainHeroSubstitutionScope(hero, party))
            {
                var sentence = new ConversationSentence("accept_weapons", new TextObject("accept"), "start", "close_window",
                    null, null, () =>
                    {
                        ownerQuest.QuestAcceptedConsequences();
                        ownerLog = Assert.Single(ownerQuest.JournalEntries);
                        Assert.Equal(3, ownerLog.CurrentProgress);
                        if (endInsideCallback) ownerQuest.CompleteQuestWithCancel();
                    });
                sentence.RunConsequence(Game.Current);
            }
        });
        TestEnvironment.FlushCoalescer();

        return (ownerQuest, ownerLog);
    }

    private void AssertSavedHistory(JournalLog[] expected)
    {
        Server.Call(() =>
        {
            var archive = Archive();
            Assert.True(archive.IsEnded());
            AssertEntries(expected, archive.GetEntries());
            var definitions = new DefinitionContext();
            definitions.FillWithCurrentTypes();
            Assert.False(definitions.GotError, string.Join("\n", definitions.Errors));
            var save = new SaveContext(definitions);
            var metadata = new MetaData();
            Assert.True(save.Save(Campaign.Current.LogEntryHistory, metadata, out var error), error);
            var driver = new InMemDriver();
            Assert.Equal(SaveResult.Success, driver.Save("weapons_journal", 1, metadata, save.SaveData).GetAwaiter().GetResult().SaveResult);
            var load = new LoadContext(definitions, driver);
            Assert.True(load.Load(driver.Load("weapons_journal"), loadAsLateInitialize: true));
            var loaded = Assert.IsType<LogEntryHistory>(load.RootObject);
            var loadedArchive = Assert.Single(loaded.GetGameActionLogs<JournalLogEntry>(entry => entry._relatedObjectIds.Contains(FirstIssueId)));
            Assert.NotSame(archive, loadedArchive);
            Assert.True(loadedArchive.IsEnded());
            AssertEntries(expected, loadedArchive.GetEntries());
            output.WriteLine("Loaded server archive progress: {0}", loadedArchive.GetEntries().First().CurrentProgress);
        });
    }

    private static void AssertEntries(JournalLog[] expected, IEnumerable<JournalLog> entries)
    {
        var actual = entries.ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
            AssertMetadata(expected[index], actual[index]);
    }

    private static JournalLogEntry Archive() => Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
        entry => entry._relatedObjectIds.Contains(FirstIssueId)));

    private static void AssertMetadata(JournalLog expected, JournalLog actual) =>
        AssertMetadata(new RequestQuestAcceptanceJournal("", "", 0, expected), actual);

    private static void AssertMetadata(RequestQuestAcceptanceJournal expected, JournalLog actual)
    {
        Assert.Equal(expected.Progress, actual.CurrentProgress);
        Assert.Equal(expected.Text?.ToString(), actual.LogText?.ToString());
        Assert.Equal(expected.Time.NumTicks, actual.LogTime.NumTicks);
        Assert.Equal(expected.TaskName?.ToString(), actual.TaskName?.ToString());
        Assert.Equal(expected.Range, actual.Range);
        Assert.Equal(expected.Type, actual.Type);
    }
}
