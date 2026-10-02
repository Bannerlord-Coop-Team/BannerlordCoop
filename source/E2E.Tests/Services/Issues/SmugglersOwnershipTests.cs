using Common.Util;
using E2E.Tests.Util;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.Smugglers;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class SmugglersOwnershipTests : SyncTestBase
{
    public SmugglersOwnershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AlternativeAcceptUsesServerJournalAndOwnerWithoutRunningFallbackMirror()
    {
        var giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var playerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var originId = TestEnvironment.CreateRegisteredObject<Settlement>();
        long started = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(originId, out var origin));
            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new SmugglersIssueBehavior.SmugglersIssue(hero,
                    new KeyValuePair<Settlement, Settlement>(target, origin)),
                typeof(SmugglersIssueBehavior.SmugglersIssue), IssueBase.IssueFrequency.Rare);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
            var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(8), CampaignTime.DaysFromNow(7), 0.2f, 2, 1100, null);
            var log = new JournalLog(CampaignTime.Now - CampaignTime.Days(1), new TextObject("server companion journal"),
                new TextObject("Return Days"), 0, 8);
            started = log.LogTime.NumTicks;
            var fields = new SmugglersAlternativeAcceptFields(0.2f, state, playerId, new SmugglersJournalEntry(log));
            Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeAlternativeAccepted(giverId, "smugglers-owner", state,
                GenericAcceptFieldsSerializer.Serialize(fields), new TroopRosterData(Array.Empty<TroopRosterElementData>())));
        });
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(playerId, out var player));
                var issue = Assert.IsType<SmugglersIssueBehavior.SmugglersIssue>(giver.Issue);
                Assert.True(issue.IsSolvingWithAlternative);
                Assert.True(client.Resolve<ISmugglersQuestOwners>().TryGet(issue, out var owner));
                Assert.Same(player, owner);
                var log = Assert.Single(issue.JournalEntries);
                Assert.Equal(started, log.LogTime.NumTicks);
                Assert.Equal("server companion journal", log.LogText.ToString());
                Assert.Equal(0.2f, issue.IssueDifficultyMultiplier);
            });
        }
    }

    [Fact]
    public void AnotherPlayersQuestDoesNotRegisterAVisibleMapMarker()
    {
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
                var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
                quest.IsTrackEnabled = true;
                quest.AddTrackedObject(target);
                Assert.False(quest.IsTracked(target));
                Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(target));
                client.Resolve<ISmugglersQuestOwners>().Set(quest, Hero.MainHero);
                quest.AddTrackedObject(target);
                Assert.True(quest.IsTracked(target));
                Assert.True(Campaign.Current.VisualTrackerManager.CheckTracked(target));
            });
        }
    }

    [Fact]
    public void ConversationChecksOwnershipBeforeInvokingAnyQuestLineCondition()
    {
        var giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var owner = Clients.First();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                client.Resolve<IControllerIdProvider>().SetControllerId(client == owner ? "smugglers-owner" : "other-player");
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                client.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, "smugglers-owner");
                var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
                quest.QuestGiver = giver;
                var calls = 0;
                var sentence = new ConversationSentence("smugglers_persuasion_attempt", TextObject.GetEmpty(),
                    "smugglers_persuasion_start_reservation", "smugglers_persuasion_select_option",
                    () => { calls++; return true; }, null, null, relatedObject: quest);

                Assert.Equal(client == owner, sentence.RunCondition());
                Assert.Equal(client == owner ? 1 : 0, calls);
            });
        }
    }

    [Fact]
    public void ReceivingWorldChangesDoesNotRunLocalQuestAcceptanceOrCancellation()
    {
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
                var issue = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssue>();
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                using (new AllowedThread())
                {
                    quest.QuestAcceptedConsequences();
                    quest.CompleteQuestWithCancel(new TextObject("world callback"));
                    issue.CompleteIssueWithCancel();
                }
                Assert.True(quest.IsOngoing);
                Assert.Null(quest._smugglerParty);
                Assert.True(issue.IsSolvingWithAlternative);
            });
        }
    }
}
