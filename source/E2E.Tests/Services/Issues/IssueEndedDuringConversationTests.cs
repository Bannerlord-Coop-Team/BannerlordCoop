using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class IssueEndedDuringConversationTests : IDisposable
{
    private const string OfferToken = "test_issue_offer_player_response";
    private const string QuestToken = "test_quest_player_options";
    private const string MainOptionsToken = "hero_main_options";

    // StartNew reads character data the test campaign never loads, so each test puts the dialog at its step instead
    private static readonly MethodBase StartNewMethod =
        AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.StartNew));

    private readonly StubIssueQuestGenerator quests = new();
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public IssueEndedDuringConversationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        quests.Dispose();
        preconditions.Dispose();
    }

    private static ConversationManager Conversation => Campaign.Current.ConversationManager;

    [Fact]
    public void AnIssueTimedOutWhileThePlayerReadsItsOfferStaysUntilTheConversationEnds()
    {
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines());

        scenario.Server.Call(() => Notable(scenario.Server).Issue.CompleteIssueWithTimedOut());

        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        scenario.ClientB.Call(() => Assert.Null(Notable(scenario.ClientB).Issue));
        scenario.ClientA.Call(() => Assert.NotNull(Notable(scenario.ClientA).Issue));

        scenario.ClientA.Call(() => Conversation.DoOption(0));

        // The quest the accept made answers it, as in vanilla
        scenario.ClientA.Call(() => Assert.Equal("test_quest_offer", CurrentLineId()));

        scenario.ClientA.Call(ClickContinue);

        var strayQuest = Assert.Single(quests.CreatedFor(scenario.ClientA));
        scenario.ClientA.Call(() =>
        {
            Assert.False(Conversation.IsConversationInProgress);
            Assert.Null(Notable(scenario.ClientA).Issue);
            Assert.True(strayQuest.IsFinalized);
        });
    }

    [Fact]
    public void AcceptingAnIssueAnotherPlayerTookClosesTheConversation()
    {
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines());
        scenario.Accept(scenario.ClientB);

        var shown = QuickInformationShownBy(() => scenario.ClientA.Call(() => Conversation.DoOption(0)));
        scenario.ClientA.Call(ClickContinue);

        Assert.Single(shown);
        Assert.Empty(quests.CreatedFor(scenario.ClientA));
        scenario.ClientA.Call(() =>
        {
            Assert.False(Conversation.IsConversationInProgress);
            Assert.True(Notable(scenario.ClientA).Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void AcceptingAnIssueThatMovedToARelativeClosesTheConversation()
    {
        var relativeId = TestEnvironment.CreateRegisteredObject<Hero>();
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines());

        scenario.Server.Call(() => Campaign.Current.IssueManager.ChangeIssueOwner(
            Notable(scenario.Server).Issue, scenario.Server.GetRegisteredObject<Hero>(relativeId)));
        scenario.ClientA.Call(() => Assert.Null(Notable(scenario.ClientA).Issue));

        var shown = QuickInformationShownBy(() => scenario.ClientA.Call(() => Conversation.DoOption(0)));
        scenario.ClientA.Call(ClickContinue);

        Assert.Single(shown);
        Assert.Empty(quests.CreatedFor(scenario.ClientA));
        scenario.ClientA.Call(() => Assert.False(Conversation.IsConversationInProgress));
    }

    [Fact]
    public void AStrayQuestRefusedWhileThePlayerAnswersItsDialogIsCancelledWhenTheConversationEnds()
    {
        var router = scenario.Server.Resolve<TestNetworkRouter>();
        // Player A accepts as well before hearing that player B was first
        router.PauseLink(scenario.Server.NetPeer, scenario.ClientA.NetPeer);
        scenario.Accept(scenario.ClientB);
        scenario.Accept(scenario.ClientA);
        var strayQuest = Assert.Single(quests.CreatedFor(scenario.ClientA));

        // The refusal arrives while the quest's own lines are on screen
        TalkToTheNotable(scenario.ClientA, QuestToken, QuestLines(strayQuest));
        router.ResumeLink(scenario.Server.NetPeer, scenario.ClientA.NetPeer);
        TestEnvironment.FlushCoalescer();
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());

        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.ClientA.Call(() =>
        {
            Assert.Equal("test_quest_answer", CurrentLineId());
            Assert.False(strayQuest.IsFinalized);
        });

        scenario.ClientA.Call(ClickContinue);

        scenario.ClientA.Call(() =>
        {
            Assert.False(Conversation.IsConversationInProgress);
            Assert.True(strayQuest.IsFinalized);
            Assert.Null(Notable(scenario.ClientA).Issue.IssueQuest);
            Assert.True(Notable(scenario.ClientA).Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void ANewIssueForTheGiverWaitsBehindTheirEndedOneUntilTheConversationEnds()
    {
        PlaceTheNotableInASettlement();
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines(QuestToken));
        scenario.Server.Call(() => Notable(scenario.Server).Issue.CompleteIssueWithTimedOut());

        // The player accepts the ended issue, and the quest it made has its lines on screen when the giver gets a new one
        scenario.ClientA.Call(() =>
        {
            Conversation.DoOption(0);
            QuestLines(Notable(scenario.ClientA).Issue.IssueQuest)(Conversation);
            Conversation.GetPlayerSentenceOptions();
        });
        scenario.Server.Call(() => MakeNewIssue(Notable(scenario.Server)));
        var created = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());

        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.ClientA.Call(() =>
        {
            Assert.Equal("test_quest_answer", CurrentLineId());
            Assert.Equal(QuestScenario.IssueId, Notable(scenario.ClientA).Issue?.StringId);
        });

        scenario.ClientA.Call(ClickContinue);

        var strayQuest = Assert.Single(quests.CreatedFor(scenario.ClientA));
        scenario.ClientA.Call(() =>
        {
            Assert.False(Conversation.IsConversationInProgress);
            Assert.True(strayQuest.IsFinalized);
            Assert.Equal(created.IssueId, Notable(scenario.ClientA).Issue?.StringId);
        });
    }

    [Fact]
    public void AnIssueMovedToARelativeWaitsBehindTheStrayQuestTheConversationHolds()
    {
        var relativeId = TestEnvironment.CreateRegisteredObject<Hero>();
        var router = scenario.Server.Resolve<TestNetworkRouter>();
        // Player A accepts as well before hearing that player B was first
        router.PauseLink(scenario.Server.NetPeer, scenario.ClientA.NetPeer);
        scenario.Accept(scenario.ClientB);
        scenario.Accept(scenario.ClientA);
        var strayQuest = Assert.Single(quests.CreatedFor(scenario.ClientA));
        TalkToTheNotable(scenario.ClientA, QuestToken, QuestLines(strayQuest));

        // The server then hands the issue player B took to a relative, as when the giver dies
        scenario.Server.Call(() => Campaign.Current.IssueManager.ChangeIssueOwner(
            Notable(scenario.Server).Issue, scenario.Server.GetRegisteredObject<Hero>(relativeId)));
        router.ResumeLink(scenario.Server.NetPeer, scenario.ClientA.NetPeer);
        TestEnvironment.FlushCoalescer();

        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.ClientA.Call(() =>
        {
            Assert.Equal("test_quest_answer", CurrentLineId());
            Assert.Equal(QuestScenario.IssueId, Notable(scenario.ClientA).Issue?.StringId);
        });

        scenario.ClientA.Call(ClickContinue);

        scenario.ClientA.Call(() =>
        {
            var moved = scenario.ClientA.GetRegisteredObject<Hero>(relativeId).Issue;
            Assert.False(Conversation.IsConversationInProgress);
            Assert.True(strayQuest.IsFinalized);
            Assert.Null(Notable(scenario.ClientA).Issue);
            Assert.Equal(QuestScenario.IssueId, moved?.StringId);
            Assert.True(moved!.IsSolvingWithQuest);
            Assert.True(scenario.ClientA.Resolve<IIssueOwnershipRegistry>().TryGetOwner(relativeId, QuestScenario.IssueId, out var owner));
            Assert.Equal(QuestScenario.ControllerB, owner);
        });
    }

    [Fact]
    public void AnOwnersQuestEndedDuringTheirConversationWithTheGiverReplaysAtOnce()
    {
        scenario.Accept(scenario.ClientA);
        var ownerQuest = scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread())
            {
                ownerQuest.StartQuest();
            }
        });

        // The owner ends the quest from its dialog with the giver, the way a hand in does
        TalkToTheNotable(scenario.ClientA, QuestToken, QuestLines(ownerQuest));
        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this,
            new QuestOutcomeRequested(ownerQuest, IssueOutcome.QuestCancel)));

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
        scenario.ClientA.Call(() =>
        {
            Assert.True(Conversation.IsConversationInProgress);
            Assert.True(ownerQuest.IsFinalized);
            Assert.Null(Notable(scenario.ClientA).Issue);
        });
    }

    [Fact]
    public void AnotherGiversIssueEndedDuringTheConversationReplaysAtOnce()
    {
        const string otherIssueId = "issue_other_giver";
        var otherGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        scenario.AddIssue(otherGiverId, otherIssueId);
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines());

        scenario.Server.Call(() =>
        {
            Notable(scenario.Server).Issue.CompleteIssueWithTimedOut();
            scenario.Server.GetRegisteredObject<Hero>(otherGiverId).Issue.CompleteIssueWithTimedOut();
        });

        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>(),
            finalized => finalized.IssueId == otherIssueId);
        scenario.ClientA.Call(() =>
        {
            Assert.True(Conversation.IsConversationInProgress);
            Assert.Equal(QuestScenario.IssueId, Notable(scenario.ClientA).Issue?.StringId);
            Assert.Null(scenario.ClientA.GetRegisteredObject<Hero>(otherGiverId).Issue);
        });
    }

    [Fact]
    public void AnIssueAnotherPlayerSentTroopsForStaysUntilTheConversationEnds()
    {
        // Player B sent troops for the issue, as every machine records it
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Notable(instance).Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                }

                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                    scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerB));
            });
        }

        // Player A asks the giver about it while the server ends it
        TalkToTheNotable(scenario.ClientA, MainOptionsToken, TaskGivenLines());
        scenario.Server.Call(() => Notable(scenario.Server).Issue.CompleteIssueWithStayAliveConditionsFailed());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.ClientA.Call(() => Assert.Equal("test_alternative_solution_discuss", CurrentLineId()));

        scenario.ClientA.Call(ClickContinue);

        scenario.ClientA.Call(() =>
        {
            Assert.False(Conversation.IsConversationInProgress);
            Assert.Null(Notable(scenario.ClientA).Issue);
            Assert.False(scenario.ClientA.Resolve<IIssueOwnershipRegistry>().TryGetOwner(
                scenario.NotableId, QuestScenario.IssueId, out _));
        });
    }

    [Fact]
    public void ALordSolutionForTheGiversNewIssueWaitsBehindItsCreation()
    {
        PlaceTheNotableInASettlement();
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines());
        scenario.Server.Call(() =>
        {
            Notable(scenario.Server).Issue.CompleteIssueWithTimedOut();
            MakeNewIssue(Notable(scenario.Server));
        });
        var created = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());

        // Player B starts the new issue's lord solution while player A still holds the ended one
        scenario.ClientA.SimulateMessage(this, new NetworkLordSolutionStarted(
            scenario.NotableId, created.IssueId, QuestScenario.ControllerB, 1f));
        scenario.ClientA.Call(() => Assert.Equal(QuestScenario.IssueId, Notable(scenario.ClientA).Issue?.StringId));

        scenario.ClientA.Call(() => Conversation.EndConversation());

        scenario.ClientA.Call(() =>
        {
            var issue = Notable(scenario.ClientA).Issue;
            Assert.Equal(created.IssueId, issue?.StringId);
            Assert.True(issue!.IsSolvingWithLordSolution);
            Assert.True(scenario.ClientA.Resolve<IIssueOwnershipRegistry>().TryGetOwner(
                scenario.NotableId, created.IssueId, out var owner));
            Assert.Equal(QuestScenario.ControllerB, owner);
        });
    }

    [Fact]
    public void WorkForARelativeBehindAWaitingMoveWaitsWhileTheGiverStillTalks()
    {
        var relativeId = TestEnvironment.CreateRegisteredObject<Hero>();
        TalkToTheNotable(scenario.ClientA, OfferToken, OfferLines());
        var ran = new List<string>();

        scenario.ClientA.Call(() =>
        {
            var conversationGuard = scenario.ClientA.Resolve<IIssueConversationGuard>();
            conversationGuard.RunWhenNotTalkingTo(() => ran.Add("giver"), scenario.NotableId);
            conversationGuard.RunInOrder(() => ran.Add("move"), scenario.NotableId, relativeId);
            conversationGuard.RunInOrder(() => ran.Add("relative"), relativeId);

            // The relative's work only shares a hero with the move, which waits for the giver still being talked to
            conversationGuard.OnConversationEnded();
            Assert.Empty(ran);

            Conversation.EndConversation();
            Assert.Equal(new[] { "giver", "move", "relative" }, ran);
        });
    }

    private void PlaceTheNotableInASettlement()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    // Making an issue picks a counter offer hero from the giver's settlement
                    Notable(instance).StayingInSettlement = instance.GetRegisteredObject<Settlement>(settlementId);
                    Notable(instance).Occupation = Occupation.GangLeader;
                }
            });
        }
    }

    private static void MakeNewIssue(Hero giver)
    {
        var potentialIssue = new PotentialIssueData(
            (in PotentialIssueData _, Hero hero) => new AllowedIssue(hero, null),
            typeof(AllowedIssue),
            IssueBase.IssueFrequency.Common);

        Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, giver));
    }

    private void TalkToTheNotable(EnvironmentInstance client, string token, Action<ConversationManager> addLines)
    {
        client.Call(() =>
        {
            // The dialog's text variables read encyclopedia links
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

            var character = ObjectHelper.SkipConstructor<CharacterObject>();
            character.HeroObject = Notable(client);

            Conversation.SetupAndStartMapConversation(
                null, new MapConversationAgent(character), new MapConversationAgent(CharacterObject.PlayerCharacter));
            addLines(Conversation);
            Conversation.ActiveToken = Conversation.GetStateIndex(token);
            Conversation.GetPlayerSentenceOptions();
        }, new[] { StartNewMethod });
    }

    private static Action<ConversationManager> OfferLines(string answerOutput = "close_window") => conversation =>
    {
        // Vanilla's issue_offer_player_accept_quest
        conversation.AddDialogLine(PlayerLine("test_issue_accept", OfferToken, "issue_classic_quest_start",
            () => Campaign.Current.IssueManager.StartIssueQuest(Hero.OneToOneConversationHero)));
        // Stands in for the offer line vanilla's quest adds when the accept makes it, gone with the issue
        conversation.AddDialogLine(NpcLine("test_quest_offer", "issue_classic_quest_start", answerOutput,
            () => Hero.OneToOneConversationHero.Issue?.IssueQuest != null, Hero.OneToOneConversationHero.Issue));
    };

    // A player option and its answer that belong to the quest, like the steps of its own dialog
    private static Action<ConversationManager> QuestLines(QuestBase quest) => conversation =>
    {
        conversation.AddDialogLine(PlayerLine("test_quest_option", QuestToken, "test_quest_answer", relatedObject: quest));
        conversation.AddDialogLine(NpcLine("test_quest_answer", "test_quest_answer", "close_window", relatedObject: quest));
    };

    // Vanilla's hero_task_given_alternative and issue_alternative_solution_discuss, whose answer reads the giver's issue
    private static Action<ConversationManager> TaskGivenLines() => conversation =>
    {
        conversation.AddDialogLine(PlayerLine("test_task_given_alternative", MainOptionsToken, "issue_discuss_alternative_solution"));
        conversation.AddDialogLine(NpcLine("test_alternative_solution_discuss", "issue_discuss_alternative_solution", "close_window",
            () => Hero.OneToOneConversationHero.Issue is IssueBase issue && issue.IsThereAlternativeSolution && issue.IsSolvingWithAlternative));
    };

    private static ConversationSentence PlayerLine(string id, string input, string output,
        ConversationSentence.OnConsequenceDelegate? consequence = null, object? relatedObject = null)
    {
        return new ConversationSentence(id, new TextObject(id), input, output, null, null, consequence,
            flags: 1u, relatedObject: relatedObject);
    }

    private static ConversationSentence NpcLine(string id, string input, string output,
        ConversationSentence.OnConditionDelegate? condition = null, object? relatedObject = null)
    {
        return new ConversationSentence(id, new TextObject(id), input, output, condition, null, null,
            relatedObject: relatedObject);
    }

    // What the conversation screen does when the player clicks to continue
    private static void ClickContinue()
    {
        if (!Conversation.IsConversationInProgress) return;

        Conversation.GetPlayerSentenceOptions();
        Conversation.ContinueConversation();
    }

    private static List<string> QuickInformationShownBy(Action action)
    {
        var shown = new List<string>();
        Action<string, int, BasicCharacterObject, Equipment, string> capture = (message, _, _, _, _) => shown.Add(message);
        // The publicized backing field shares the event's name, so the event is found by name
        var quickInformation = typeof(MBInformationManager).GetEvent("FiringQuickInformation", BindingFlags.Public | BindingFlags.Static)!;

        quickInformation.AddEventHandler(null, capture);
        try
        {
            action();
        }
        finally
        {
            quickInformation.RemoveEventHandler(null, capture);
        }

        return shown;
    }

    private static string CurrentLineId() => Conversation._sentences[Conversation._currentSentence].Id;

    private Hero Notable(EnvironmentInstance instance) => instance.GetRegisteredObject<Hero>(scenario.NotableId);
}
