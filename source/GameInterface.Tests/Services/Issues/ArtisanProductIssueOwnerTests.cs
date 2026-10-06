using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public sealed class ArtisanProductIssueOwnerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Campaign previousCampaign = Campaign.Current;
    private readonly Harmony harmony = new($"{nameof(ArtisanProductIssueOwnerTests)}.{Guid.NewGuid():N}");
    private readonly Mock<IObjectManager> objects = new();
    private readonly Mock<INetwork> network = new();
    private readonly IssueGenerationRegistry generations = new();
    private readonly IssueOwnershipRegistry ownership = new();
    private readonly IssueConversationTracker conversations = new();
    private readonly Hero original = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero relative = ObjectHelper.SkipConstructor<Hero>();
    private readonly Issue issue = ObjectHelper.SkipConstructor<Issue>();
    private readonly Quest quest = ObjectHelper.SkipConstructor<Quest>();
    private readonly JournalLogEntry history = ObjectHelper.SkipConstructor<JournalLogEntry>();
    private readonly Dictionary<Hero, IssueBase> issues;
    private readonly List<NetworkArtisanProductIssueOwnerChanged> sent = new();
    private readonly ArtisanProductIssueOwnerHandler handler;

    static ArtisanProductIssueOwnerTests()
        => RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);

    public ArtisanProductIssueOwnerTests()
    {
        ModInformation.IsServer = false;
        var manager = new IssueManager();
        issues = manager._issues;
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.IssueManager = manager;
        campaign.MapTimeTracker = new MapTimeTracker(CampaignTime.Zero);
        campaign.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());
        campaign._campaignBehaviorManager = Mock.Of<ICampaignBehaviorManager>();
        campaign.EncyclopediaManager = new EncyclopediaManager();
        Campaign.Current = campaign;
        var heroPage = ObjectHelper.SkipConstructor<DefaultEncyclopediaHeroPage>();
        AccessTools.Field(typeof(EncyclopediaPage), "_identifiers").SetValue(heroPage,
            new Dictionary<Type, string> { [typeof(Hero)] = "Hero" });
        campaign.EncyclopediaManager._pages = new Dictionary<Type, EncyclopediaPage> { [typeof(Hero)] = heroPage };
        AccessTools.Field(typeof(IssueBase), "_journalEntries").SetValue(issue, new MBList<JournalLog>());
        issue.IssueOwner = original;
        original.OnIssueCreatedForHero(issue);
        issues.Add(original, issue);
        quest.QuestGiver = original;
        quest._questState = QuestBase.QuestStates.Ongoing;
        issue.IssueQuest = quest;
        issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
        generations.SetGeneration(original, 7);
        generations.SetGeneration(relative, 40);
        ownership.SetOwner(original, "player-a");
        ownership.SetJournalOwner(history, "player-a");
        conversations.Register("original", "player-a", 7);
        SetupHero("original", original);
        SetupHero("relative", relative);
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(message =>
        {
            if (message is NetworkArtisanProductIssueOwnerChanged changed) sent.Add(changed);
        });
        handler = new ArtisanProductIssueOwnerHandler(MessageBroker.Instance, network.Object, objects.Object,
            generations, ownership, conversations);
        harmony.CreateClassProcessor(typeof(ArtisanProductIssueOwnerPatches)).Patch();
    }

    [Theory]
    [InlineData(IssueBase.IssueState.Ongoing)]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithAlternativeSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution)]
    public void ServerTransferPreservesTheIssueAndMovesOnlyItsPlayerOwnership(IssueBase.IssueState state)
    {
        PrepareState(state);
        ModInformation.IsServer = true;

        Campaign.Current.IssueManager.ChangeIssueOwner(issue, relative);

        var message = Assert.Single(sent);
        Assert.Equal("original", message.PreviousOwnerId);
        Assert.Equal(7, message.PreviousGeneration);
        Assert.Equal("relative", message.OwnerId);
        Assert.Equal(41, message.Generation);
        Assert.Equal(state == IssueBase.IssueState.Ongoing ? null : "player-a", message.ControllerId);
        AssertTransferred(state);
    }

    [Theory]
    [InlineData(IssueBase.IssueState.Ongoing)]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithAlternativeSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution)]
    public void ReceivedTransferUsesNativeOwnerChangeAndRetainsQuestGiverAndJournal(IssueBase.IssueState state)
    {
        PrepareState(state);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, Message(controller: state == IssueBase.IssueState.Ongoing ? null : "player-a"));
        stream.Position = 0;

        MessageBroker.Instance.Publish(this, Serializer.Deserialize<NetworkArtisanProductIssueOwnerChanged>(stream));
        Drain();

        AssertTransferred(state);
        Assert.Empty(sent);
    }

    [Fact]
    public void ClientCannotChangeOwnerOutsideAReceivedMessage()
    {
        Campaign.Current.IssueManager.ChangeIssueOwner(issue, relative);

        Assert.Same(issue, original.Issue);
        Assert.Null(relative.Issue);
        Assert.True(ownership.TryGetOwnerControllerId(original, out _));
    }

    [Theory]
    [InlineData(6, 41)]
    [InlineData(8, 41)]
    [InlineData(7, 40)]
    [InlineData(7, 39)]
    public void StaleSourceOrDestinationGenerationDoesNotTransfer(int previous, int next)
    {
        MessageBroker.Instance.Publish(this, Message(previous, next));
        Drain();

        Assert.Same(issue, original.Issue);
        Assert.Null(relative.Issue);
        Assert.True(ownership.TryGetOwnerControllerId(original, out _));
        Assert.False(ownership.TryGetOwnerControllerId(relative, out _));
    }

    [Fact]
    public void OccupiedDestinationDoesNotReplaceAnotherPlayersIssue()
    {
        var other = ObjectHelper.SkipConstructor<Issue>();
        relative.OnIssueCreatedForHero(other);
        ownership.SetOwner(relative, "player-b");

        MessageBroker.Instance.Publish(this, Message());
        Drain();

        Assert.Same(issue, original.Issue);
        Assert.Same(other, relative.Issue);
        Assert.True(ownership.TryGetOwnerControllerId(relative, out var owner));
        Assert.Equal("player-b", owner);
    }

    [Fact]
    public void RepeatedTransferAndLateDuplicateLeaveNoOldOwnerAliases()
    {
        var heir = ObjectHelper.SkipConstructor<Hero>();
        SetupHero("heir", heir);
        MessageBroker.Instance.Publish(this, Message());
        MessageBroker.Instance.Publish(this, new NetworkArtisanProductIssueOwnerChanged("relative", 41, "heir", 1, "player-a"));
        MessageBroker.Instance.Publish(this, Message());
        Drain();

        Assert.Same(issue, heir.Issue);
        Assert.Same(original, quest.QuestGiver);
        Assert.Same(issue, IssueManager.GetIssueOfQuest(quest));
        Assert.False(ownership.TryGetOwnerControllerId(original, out _));
        Assert.False(ownership.TryGetOwnerControllerId(relative, out _));
        Assert.True(ownership.TryGetOwnerControllerId(heir, out var owner));
        Assert.Equal("player-a", owner);
        ownership.Clear(heir);
        Assert.Empty(ownership.Snapshot());
        Assert.True(ownership.TryGetJournalOwner(history, out var historicalOwner));
        Assert.Equal("player-a", historicalOwner);
    }

    [Fact]
    public void RegistryResolutionWaitsForEarlierGameThreadRegistration()
    {
        Hero missing = null!;
        bool? lookupOnGameThread = null;
        objects.Setup(o => o.TryGetObjectWithLogging("relative", out missing)).Returns(false);
        GameThread.RunSafe(() =>
        {
            SetupHero("relative", relative);
            var registered = relative;
            objects.Setup(o => o.TryGetObjectWithLogging("relative", out registered))
                .Callback(() => lookupOnGameThread = GameThread.Instance.IsGameThread)
                .Returns(true);
        });

        MessageBroker.Instance.Publish(this, Message());
        Drain();

        Assert.Same(issue, relative.Issue);
        Assert.True(lookupOnGameThread);
    }

    [Fact]
    public void DirectChoiceUsesCurrentIssueOwnerInsteadOfOriginalQuestGiver()
    {
        MessageBroker.Instance.Publish(this, Message());
        Drain();
        var requests = new List<ArtisanProductQuestActionRequested>();
        Action<MessagePayload<ArtisanProductQuestActionRequested>> capture = payload => requests.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            Assert.False(ArtisanProductQuestActionPatches.Request(quest, ArtisanProductQuestAction.DeliverFully));
            Assert.Same(relative, Assert.Single(requests).Giver);
            Assert.Same(original, quest.QuestGiver);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Theory]
    [InlineData(QuestBase.QuestCompleteDetails.Success, IssueFinalizeReason.QuestSuccess)]
    [InlineData(QuestBase.QuestCompleteDetails.Cancel, IssueFinalizeReason.QuestCancel)]
    [InlineData(QuestBase.QuestCompleteDetails.Fail, IssueFinalizeReason.QuestFail)]
    [InlineData(QuestBase.QuestCompleteDetails.Timeout, IssueFinalizeReason.QuestTimeout)]
    [InlineData(QuestBase.QuestCompleteDetails.FailWithBetrayal, IssueFinalizeReason.QuestBetrayal)]
    public void FinalizationReasonFollowsQuestIdentityAfterInheritance(QuestBase.QuestCompleteDetails detail,
        IssueFinalizeReason expected)
    {
        MessageBroker.Instance.Publish(this, Message());
        Drain();
        var laterIssue = ObjectHelper.SkipConstructor<Issue>();
        laterIssue.IssueOwner = original;
        original.OnIssueCreatedForHero(laterIssue);
        issues.Add(original, laterIssue);

        IssueManagerQuestCompletedReasonCapture.Prefix(quest, detail);

        Assert.Equal(expected, IssueManagerQuestCompletedReasonCapture.PendingReasons[relative]);
        Assert.False(IssueManagerQuestCompletedReasonCapture.PendingReasons.ContainsKey(original));
        IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(relative);
        issues.Remove(relative);
        IssueManagerQuestCompletedReasonCapture.Prefix(quest, detail);
        Assert.False(IssueManagerQuestCompletedReasonCapture.PendingReasons.ContainsKey(original));
        Assert.False(IssueManagerQuestCompletedReasonCapture.PendingReasons.ContainsKey(relative));
    }

    private void PrepareState(IssueBase.IssueState state)
    {
        issue._issueState = state;
        if (state != IssueBase.IssueState.SolvingWithQuestSolution) issue.IssueQuest = null;
        if (state == IssueBase.IssueState.Ongoing) ownership.Clear(original);
    }

    private void AssertTransferred(IssueBase.IssueState state)
    {
        Assert.Null(original.Issue);
        Assert.Same(issue, relative.Issue);
        Assert.Same(relative, issue.IssueOwner);
        Assert.Equal(state, issue._issueState);
        Assert.Equal(state == IssueBase.IssueState.SolvingWithAlternativeSolution ? 1 : 0, issue.JournalEntries.Count);
        Assert.False(issues.ContainsKey(original));
        Assert.Same(issue, issues[relative]);
        Assert.Same(original, quest.QuestGiver);
        Assert.False(ownership.TryGetOwnerControllerId(original, out _));
        Assert.Equal(state != IssueBase.IssueState.Ongoing, ownership.TryGetOwnerControllerId(relative, out var owner));
        if (state != IssueBase.IssueState.Ongoing) Assert.Equal("player-a", owner);
        Assert.True(generations.TryGetGeneration(relative, out var generation));
        Assert.Equal(41, generation);
        Assert.True(ownership.TryGetJournalOwner(history, out var historicalOwner));
        Assert.Equal("player-a", historicalOwner);
        Assert.False(conversations.TryGetTrackedRequester("original", "player-a", out _));
        if (issue.IssueQuest != null) Assert.Same(issue, IssueManager.GetIssueOfQuest(quest));
    }

    private void SetupHero(string id, Hero hero)
    {
        hero.StringId = id;
        hero.SetName(new TextObject(id), new TextObject(id));
        hero._characterObject = new CharacterObject { HeroObject = hero, StringId = id };
        objects.Setup(o => o.TryGetIdWithLogging(hero, out id)).Returns(true);
        objects.Setup(o => o.TryGetObjectWithLogging(id, out hero)).Returns(true);
    }

    private static NetworkArtisanProductIssueOwnerChanged Message(int previous = 7, int next = 41, string? controller = "player-a")
        => new("original", previous, "relative", next, controller);

    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        Drain();
        handler.Dispose();
        harmony.UnpatchAll(harmony.Id);
        IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(original);
        IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(relative);
        Campaign.Current = previousCampaign;
        ModInformation.IsServer = wasServer;
    }
}
