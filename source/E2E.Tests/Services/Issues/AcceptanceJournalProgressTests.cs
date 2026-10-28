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

    [Fact]
    public void AnUnrelatedDialogueLineKeepsTrackingTheLaterAcceptanceIntroduction()
    {
        Accept(ClientA, FirstGiverId);
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            var quest = giver.Issue.IssueQuest;
            var unrelated = new ConversationSentence("unrelated", new TextObject("continue"), "start", "next",
                null, null, () => { });
            unrelated.RunConsequence(Game.Current);
            Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
            var introduction = new ConversationSentence("introduction", new TextObject("agreed"), "next", "close_window",
                null, null, () => quest.AddLog(new TextObject("The introduction arrived on a later dialogue turn.")));
            introduction.RunConsequence(Game.Current);
            Campaign.Current.ConversationManager.EndConversation();
            quest.AddLog(new TextObject("Later gameplay text is outside the acceptance conversation."));
        });
        TestEnvironment.FlushCoalescer();

        var sent = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        Assert.Equal("The introduction arrived on a later dialogue turn.", sent.Text.ToString());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            AssertMetadata(sent, Assert.Single(giver.Issue.IssueQuest.JournalEntries));
        });
        Assert.Empty(ClientB.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CarriedWeaponsRetainFinalAcceptanceProgressIncludingAnEndingInsideTheCallback(bool endInsideCallback)
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
                Campaign.Current.ConversationManager.EndConversation();
            }
        });
        TestEnvironment.FlushCoalescer();

        var sent = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        output.WriteLine("Owner progress: {0}; captured progress: {1}", ownerLog.CurrentProgress, sent.Progress);
        Assert.Equal(ownerLog.CurrentProgress, sent.Progress);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            var serverLog = endInsideCallback
                ? Assert.Single(Archive().GetEntries())
                : Assert.Single(giver.Issue.IssueQuest.JournalEntries);
            AssertMetadata(ownerLog, serverLog);
            output.WriteLine("Server progress: {0}", serverLog.CurrentProgress);
        });

        if (!endInsideCallback)
        {
            ClientA.Call(() =>
            {
                ownerLog.UpdateCurrentProgress(7);
                ownerQuest.AddLog(new TextObject("Later progress before the next campaign tick"));
                ownerQuest.CompleteQuestWithCancel();
            });
            TestEnvironment.FlushCoalescer();
            Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        }
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() =>
        {
            var archive = Archive();
            Assert.True(archive.IsEnded());
            Assert.Equal(3, Assert.Single(archive.GetEntries()).CurrentProgress);
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
            AssertMetadata(sent, Assert.Single(loadedArchive.GetEntries()));
            output.WriteLine("Loaded server archive progress: {0}", Assert.Single(loadedArchive.GetEntries()).CurrentProgress);
        });
    }

    private static JournalLogEntry Archive() => Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
        entry => entry._relatedObjectIds.Contains(FirstIssueId)));

    private static void AssertMetadata(JournalLog expected, JournalLog actual) =>
        AssertMetadata(new RequestQuestAcceptanceJournal("", "", 0, expected), actual);

    private static void AssertMetadata(RequestQuestAcceptanceJournal expected, JournalLog actual)
    {
        Assert.Equal(expected.Progress, actual.CurrentProgress);
        Assert.Equal(expected.Text.ToString(), actual.LogText.ToString());
        Assert.Equal(expected.Time, actual.LogTime);
        Assert.Equal(expected.TaskName.ToString(), actual.TaskName.ToString());
        Assert.Equal(expected.Range, actual.Range);
        Assert.Equal(expected.Type, actual.Type);
    }
}
