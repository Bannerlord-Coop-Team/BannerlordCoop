using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
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
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

namespace E2E.Tests.Services.Issues;

/// <summary>Checks authoritative acceptance history before and after the real campaign serializer.</summary>
public class TimeoutJournalHistoryTests : IDisposable
{
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly ITestOutputHelper output;
    private readonly string mountId;
    private Quest ownerQuest = null!;
    private Quest serverQuest = null!;
    private string introduction = null!;

    public TimeoutJournalHistoryTests(ITestOutputHelper output)
    {
        this.output = output;
        environment = new E2ETestEnvironment(output);
        mountId = environment.CreateRegisteredObject<ItemObject>();
        scenario = new QuestScenario(environment, CreateIssue);
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                    Object<Hero>(instance, scenario.OwnerHeroId)._partyBelongedTo = Object<MobileParty>(instance, scenario.OwnerPartyId);
                new JournalLogsCampaignBehavior().RegisterEvents();
            });
        }
    }

    public void Dispose()
    {
        try { environment.Dispose(); }
        finally { preconditions.Dispose(); }
    }

    [Fact]
    public void AcceptanceRetainsTheOwnersIntroductionOnTheServerBeforeSaving()
    {
        Accept();
        scenario.Server.Call(() =>
        {
            Assert.Contains(serverQuest.JournalEntries, log => log.LogText.ToString() == introduction);
            Assert.DoesNotContain(serverQuest, Campaign.Current.QuestManager.Quests);
        });
        scenario.ClientB.Call(() =>
        {
            Assert.Null(Object<Hero>(scenario.ClientB, scenario.NotableId).Issue.IssueQuest);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
        });
        var sent = Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        scenario.Server.Call(() =>
        {
            var log = Assert.Single(serverQuest.JournalEntries);
            Assert.Equal(sent.Time, log.LogTime);
            Assert.Equal(sent.TaskName.ToString(), log.TaskName.ToString());
            Assert.Equal(sent.Type, log.Type);
            Assert.Equal(sent.Progress, log.CurrentProgress);
            Assert.Equal(sent.Range, log.Range);
        });
    }

    [Fact]
    public void RepeatedAndForeignAcceptanceLogsCannotChangeTheOwnersHistory()
    {
        Accept();
        var sent = Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        scenario.Server.SimulateMessage(scenario.ClientA.NetPeer, sent);
        scenario.Server.SimulateMessage(scenario.ClientB.NetPeer,
            new RequestQuestAcceptanceJournal(scenario.NotableId, QuestScenario.IssueId, 1,
                new JournalLog(CampaignTime.Zero, new TextObject("Other player's introduction"))));
        scenario.Server.SimulateMessage(scenario.ClientA.NetPeer,
            new RequestQuestAcceptanceJournal(scenario.NotableId, "previous_issue", 1,
                new JournalLog(CampaignTime.Zero, new TextObject("Previous issue's introduction"))));
        scenario.Server.SimulateMessage(scenario.ClientA.NetPeer,
            new RequestQuestAcceptanceJournal(scenario.NotableId, QuestScenario.IssueId, 2,
                new JournalLog(CampaignTime.Zero, new TextObject("Stage with a missing predecessor"))));
        scenario.Server.Call(() => Assert.Equal(introduction, Assert.Single(serverQuest.JournalEntries).LogText.ToString()));
    }

    [Fact]
    public void CapturesEveryAcceptanceStageButStopsBeforeLaterProgressLogs()
    {
        Accept(addSecondStage: true);
        Assert.Equal(2, scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>().Count());
        scenario.Server.Call(() => Assert.Equal(new[] { introduction, "Second acceptance stage" },
            serverQuest.JournalEntries.Select(log => log.LogText.ToString())));
        Assert.Equal(0, scenario.ClientA.PendingGameThreadActionCount);
        scenario.ClientA.SimulateMessage(this, new CampaignTick());
        scenario.ClientA.Call(() =>
        {
            ownerQuest.AddLog(new TextObject("Later quest progress"));
        });
        environment.FlushCoalescer();
        Assert.Equal(2, scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>().Count());
        scenario.Server.Call(() => Assert.Equal(2, serverQuest.JournalEntries.Count));
    }

    [Fact]
    public void RealGiverDeathEndsTheOwnerQuestAndClearsTheBystanderIssueOnce()
    {
        Accept();
        scenario.Server.Call(() =>
        {
            var giver = Object<Hero>(scenario.Server, scenario.NotableId);
            Assert.Contains(Campaign.Current.IssueManager, CampaignEventDispatcher.Instance._eventReceivers);
            output.WriteLine("Death receivers: {0}", string.Join(", ", CampaignEventDispatcher.Instance._eventReceivers.Select(receiver => receiver.GetType().FullName)));
            var changeState = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
            var bootstrap = Assert.Single(Harmony.GetPatchInfo(changeState).Prefixes, patch => patch.owner == "Coop.Testing");
            var harmony = new Harmony(bootstrap.owner);
            harmony.Unpatch(changeState, bootstrap.PatchMethod);
            try
            {
                giver.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.Murdered);
                KillCharacterAction.ApplyByDeathMarkForced(giver, false);
            }
            finally
            {
                harmony.Patch(changeState, prefix: new HarmonyMethod(bootstrap.PatchMethod)
                {
                    priority = bootstrap.priority,
                    before = bootstrap.before,
                    after = bootstrap.after,
                });
            }
            Assert.False(giver.IsAlive);
            Assert.Null(giver.Issue);
        }, new[] { AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.CreateObituary)) });
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.QuestCancel,
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
            Assert.True(Archive(Campaign.Current.LogEntryHistory).IsEnded());
        });
        scenario.ClientB.Call(() => Assert.Null(Object<Hero>(scenario.ClientB, scenario.NotableId).Issue));
    }

    [Fact]
    public void HourlyTimeoutRetainsTheFullOwnerHistoryThroughSaveContextAndLoadContext()
    {
        Accept();
        scenario.ClientA.Call(() =>
        {
            ownerQuest.ChangeQuestDueTime(CampaignTime.DaysFromNow(-1));
            Campaign.Current.QuestManager.HourlyTick();
        });
        environment.FlushCoalescer();

        string[] expected = null!;
        scenario.ClientA.Call(() =>
        {
            var ownerArchive = Archive(Campaign.Current.LogEntryHistory);
            Assert.True(ownerArchive.IsEnded());
            Assert.Equal(QuestBase.QuestCompleteDetails.Timeout, ownerArchive._questCompletionDetail);
            expected = Texts(ownerArchive);
            Assert.Equal(2, expected.Length);
            Assert.Equal(introduction, expected[0]);
            Assert.Contains("failed", expected[1]);
        });

        LogEntryHistory loaded = null!;
        JournalLogEntry originalArchive = null!;
        scenario.Server.Call(() =>
        {
            var history = Campaign.Current.LogEntryHistory;
            originalArchive = Archive(history);
            output.WriteLine("Server archive before save: {0}", string.Join(" | ", Texts(originalArchive)));
            var definitions = new DefinitionContext();
            definitions.FillWithCurrentTypes();
            Assert.False(definitions.GotError, string.Join("\n", definitions.Errors));
            var save = new SaveContext(definitions);
            var metadata = new MetaData();
            Assert.True(save.Save(history, metadata, out var error), error);
            var driver = new InMemDriver();
            Assert.Equal(SaveResult.Success, driver.Save("timeout_journal", 1, metadata, save.SaveData).GetAwaiter().GetResult().SaveResult);
            var load = new LoadContext(definitions, driver);
            Assert.True(load.Load(driver.Load("timeout_journal"), loadAsLateInitialize: true));
            loaded = Assert.IsType<LogEntryHistory>(load.RootObject);
            Assert.NotSame(history, loaded);
        });

        scenario.ClientA.Call(() =>
        {
            // Campaign exposes this saved field through a getter only.
            AccessTools.Field(typeof(Campaign), nameof(Campaign._logEntryHistory)).SetValue(Campaign.Current, loaded);
            var archive = Archive(Campaign.Current.LogEntryHistory);
            Assert.NotSame(originalArchive, archive);
            Assert.All(archive.GetEntries(), entry => Assert.DoesNotContain(originalArchive.GetEntries(), original => ReferenceEquals(original, entry)));
            output.WriteLine("Loaded archive: {0}", string.Join(" | ", Texts(archive)));
            Assert.True(archive.IsEnded());
            Assert.Equal(QuestBase.QuestCompleteDetails.Timeout, archive._questCompletionDetail);
            Assert.Equal(expected, Texts(archive));
        });
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    private void Accept(bool addSecondStage = false)
    {
        scenario.ClientA.Call(() =>
        {
            using (new MainHeroSubstitutionScope(Object<Hero>(scenario.ClientA, scenario.OwnerHeroId),
                Object<MobileParty>(scenario.ClientA, scenario.OwnerPartyId)))
            {
                var issue = Object<Hero>(scenario.ClientA, scenario.NotableId).Issue;
                issue.StartIssueWithQuest();
                ownerQuest = Assert.IsType<Quest>(issue.IssueQuest);
            }
        });
        environment.FlushCoalescer();
        Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestSolutionAccept>());
        scenario.Server.Call(() => Assert.Empty(Object<Hero>(scenario.Server, scenario.NotableId).Issue.IssueQuest.JournalEntries));
        // The quest's offer dialogue can run its consequence after the original accept request was already applied.
        scenario.ClientA.Call(() =>
        {
            GameThread.Instance.Update(TimeSpan.Zero);
            using (new MainHeroSubstitutionScope(Object<Hero>(scenario.ClientA, scenario.OwnerHeroId),
                Object<MobileParty>(scenario.ClientA, scenario.OwnerPartyId)))
            {
                var sentence = new ConversationSentence("accept_horses", new TextObject("accept"), "start", "close_window",
                    null, null, () =>
                    {
                        ownerQuest.OnQuestAccepted();
                        introduction = Assert.Single(ownerQuest.JournalEntries).LogText.ToString();
                        if (addSecondStage) ownerQuest.AddLog(new TextObject("Second acceptance stage"));
                    });
                sentence.RunConsequence(Game.Current);
            }
        });
        environment.FlushCoalescer();
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(QuestScenario.ControllerA, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>()).ControllerId);
        scenario.Server.Call(() => serverQuest = Assert.IsType<Quest>(Object<Hero>(scenario.Server, scenario.NotableId).Issue.IssueQuest));
    }

    private IssueBase CreateIssue(EnvironmentInstance instance, Hero giver)
    {
        Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
        Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
        var mount = Object<ItemObject>(instance, mountId);
        mount.Name = new TextObject("sumpter horse");
        var issue = new Issue(giver);
        // The native field is readonly; the fixture supplies the XML-selected mount.
        AccessTools.Field(typeof(Issue), nameof(Issue._mountObjectToBeDelivered)).SetValue(issue, mount);
        return issue;
    }

    private static JournalLogEntry Archive(LogEntryHistory history) => Assert.Single(history.GetGameActionLogs<JournalLogEntry>(
        entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));

    private static string[] Texts(JournalLogEntry entry) => entry.GetEntries().Select(log => log.LogText.ToString()).ToArray();

    private static T Object<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
