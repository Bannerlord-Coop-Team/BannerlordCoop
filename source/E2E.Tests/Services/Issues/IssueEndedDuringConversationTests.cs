using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
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
        TalkToTheNotable(scenario.ClientA, OfferToken, AddOfferLines);

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
        TalkToTheNotable(scenario.ClientA, OfferToken, AddOfferLines);
        scenario.Accept(scenario.ClientB);
        using var notices = new MethodCallRecorder(
            AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));

        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.ClientA.Call(ClickContinue);

        Assert.Equal(1, notices.CountFor(scenario.ClientA));
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
        TalkToTheNotable(scenario.ClientA, OfferToken, AddOfferLines);

        scenario.Server.Call(() => Campaign.Current.IssueManager.ChangeIssueOwner(
            Notable(scenario.Server).Issue, scenario.Server.GetRegisteredObject<Hero>(relativeId)));
        scenario.ClientA.Call(() => Assert.Null(Notable(scenario.ClientA).Issue));
        using var notices = new MethodCallRecorder(
            AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));

        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.ClientA.Call(ClickContinue);

        Assert.Equal(1, notices.CountFor(scenario.ClientA));
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
        TalkToTheNotable(scenario.ClientA, QuestToken, conversation =>
        {
            conversation.AddDialogLine(PlayerLine("test_quest_option", QuestToken, "test_quest_answer", relatedObject: strayQuest));
            conversation.AddDialogLine(NpcLine("test_quest_answer", "test_quest_answer", "close_window", relatedObject: strayQuest));
        });
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
    public void ANewIssueForTheGiverArrivesWhileTheirEndedOneWaitsForTheConversation()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    // Making the issue picks a counter offer hero from the giver's settlement
                    Notable(instance).StayingInSettlement = instance.GetRegisteredObject<Settlement>(settlementId);
                    Notable(instance).Occupation = Occupation.GangLeader;
                }
            });
        }

        TalkToTheNotable(scenario.ClientA, OfferToken, AddOfferLines);
        scenario.Server.Call(() => Notable(scenario.Server).Issue.CompleteIssueWithTimedOut());

        // The player accepts the ended issue and is still reading the answer when the giver gets a new one
        scenario.ClientA.Call(() => Conversation.DoOption(0));
        scenario.Server.Call(() =>
        {
            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new AllowedIssue(hero, null),
                typeof(AllowedIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, Notable(scenario.Server)));
        });

        var created = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        var strayQuest = Assert.Single(quests.CreatedFor(scenario.ClientA));
        scenario.ClientA.Call(() =>
        {
            Assert.True(Conversation.IsConversationInProgress);
            Assert.Equal(created.IssueId, Notable(scenario.ClientA).Issue?.StringId);
            Assert.True(strayQuest.IsFinalized);
        });

        scenario.ClientA.Call(ClickContinue);

        scenario.ClientA.Call(() =>
        {
            Assert.False(Conversation.IsConversationInProgress);
            Assert.Equal(created.IssueId, Notable(scenario.ClientA).Issue?.StringId);
        });
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

            addLines(Conversation);
            Conversation.SetupAndStartMapConversation(
                null, new MapConversationAgent(character), new MapConversationAgent(CharacterObject.PlayerCharacter));
            Conversation.ActiveToken = Conversation.GetStateIndex(token);
            Conversation.GetPlayerSentenceOptions();
        }, new[] { StartNewMethod });
    }

    private static void AddOfferLines(ConversationManager conversation)
    {
        // Vanilla's issue_offer_player_accept_quest
        conversation.AddDialogLine(PlayerLine("test_issue_accept", OfferToken, "issue_classic_quest_start",
            () => Campaign.Current.IssueManager.StartIssueQuest(Hero.OneToOneConversationHero)));
        // Stands in for the offer line vanilla's quest adds when the accept makes it
        conversation.AddDialogLine(NpcLine("test_quest_offer", "issue_classic_quest_start", "close_window",
            () => Hero.OneToOneConversationHero.Issue?.IssueQuest != null));
    }

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

    private static string CurrentLineId() => Conversation._sentences[Conversation._currentSentence].Id;

    private Hero Notable(EnvironmentInstance instance) => instance.GetRegisteredObject<Hero>(scenario.NotableId);
}
