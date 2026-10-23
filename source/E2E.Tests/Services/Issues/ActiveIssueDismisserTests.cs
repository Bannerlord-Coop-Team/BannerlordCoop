using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Registry.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using UnregisteredIssue = E2E.Tests.Services.Issues.NeverRegisteredIssue;

namespace E2E.Tests.Services.Issues;

public class ActiveIssueDismisserTests : IDisposable
{
    private const string IssueId = "issue_dismiss";
    private const string ControllerId = "player-A";

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();

    public ActiveIssueDismisserTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static IssueBase AddIssue(EnvironmentInstance instance, string notableId, IssueBase.IssueState state, bool registeredType = true)
    {
        IssueBase issue = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                issue = registeredType ? new AllowedIssue(notable, null) : new UnregisteredIssue(notable);
                issue.StringId = IssueId;
                issue._issueState = state;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });

        return issue;
    }

    private static bool HasIssue(EnvironmentInstance instance, string notableId)
    {
        var hasIssue = false;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            hasIssue = notable.Issue != null;
        });

        return hasIssue;
    }

    private string RegisterPlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));

        return heroId;
    }

    private void SetOwner(string notableId, string controllerId)
    {
        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(notableId, IssueId, controllerId)));
    }

    private bool HasOwner(string notableId)
    {
        var hasOwner = false;

        Server.Call(() => hasOwner = Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _));

        return hasOwner;
    }

    private void PublishServerStarted(EnvironmentInstance instance)
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered()));
    }

    private void PublishDisconnected(EnvironmentInstance instance, EnvironmentInstance leaver)
    {
        instance.Call(() => instance.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(leaver.NetPeer, default(DisconnectInfo))));
    }

    [Theory]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution)]
    public void ServerStart_DismissesAnIssueNobodyOwns(IssueBase.IssueState state)
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Server, notableId, state);

        PublishServerStarted(Server);

        Assert.False(HasIssue(Server, notableId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void ServerStart_DismissesATroopsIssueNobodyOwnsAndFreesTheCompanion()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var issue = AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithAlternativeSolution);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));

            using (new AllowedThread())
            {
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                companion.ChangeState(Hero.CharacterStates.Disabled);
            }

            Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
        });

        PublishServerStarted(Server);

        Assert.False(HasIssue(Server, notableId));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
        });
    }

    [Fact]
    public void ServerStart_PutsTheCompanionOfATroopsIssueInTheNotablesSettlement()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var issue = AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithAlternativeSolution);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                notable.StayingInSettlement = settlement;
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                companion.ChangeState(Hero.CharacterStates.Disabled);
            }

            Assert.Null(companion.CurrentSettlement);
        });

        PublishServerStarted(Server);

        Assert.False(HasIssue(Server, notableId));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Same(settlement, companion.CurrentSettlement);
        });
    }

    [Fact]
    public void ServerStart_KeepsIdleIssuesAndIssuesOfUnregisteredTypes()
    {
        var idleNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var unregisteredNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Server, idleNotableId, IssueBase.IssueState.Ongoing);
        AddIssue(Server, unregisteredNotableId, IssueBase.IssueState.SolvingWithQuestSolution, registeredType: false);

        PublishServerStarted(Server);

        Assert.True(HasIssue(Server, idleNotableId));
        Assert.True(HasIssue(Server, unregisteredNotableId));
    }

    [Fact]
    public void ServerStart_KeepsAQuestIssueWhoseOwnerIsConnected()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        SetOwner(notableId, ControllerId);

        PublishServerStarted(Server);

        Assert.True(HasIssue(Server, notableId));
        Assert.True(HasOwner(notableId));
    }

    [Theory]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution)]
    public void PlayerDisconnected_DismissesTheIssueOfAnOwnerThatLeftAndEveryClientEndsIt(IssueBase.IssueState state)
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        AddIssue(Server, notableId, state);
        AddIssue(Client, notableId, state);
        AddIssue(OtherClient, notableId, state);
        SetOwner(notableId, ControllerId);

        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(Client.NetPeer));
        PublishDisconnected(Server, Client);

        Assert.False(HasIssue(Server, notableId));
        Assert.False(HasIssue(Client, notableId));
        Assert.False(HasIssue(OtherClient, notableId));
        Assert.False(HasOwner(notableId));

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(notableId, finalized.IssueOwnerId);
        Assert.Equal(IssueId, finalized.IssueId);
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dismissal_EndsTheServerArchiveBeforeItIsDownloadedOnRejoin(bool serverStart)
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerHeroId = RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        var issue = AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithQuestSolution);
        SetOwner(notableId, ControllerId);
        JournalLogEntry archive = null!;
        JournalLogEntry unrelated = null!;

        Server.Call(() =>
        {
            var journals = new JournalLogsCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { journals }));
            journals.RegisterEvents();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            using (new AllowedThread())
                issue.IssueQuest = new UnstartedQuest("quest_dismiss_archive", issue.IssueOwner);
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                issue.IssueQuest.AddLog(new TextObject("Follow the caravan"));
            archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
            unrelated = new JournalLogEntry(new TextObject("Unrelated active quest"), null, null, false);
            Campaign.Current.LogEntryHistory._logs.Add(unrelated);
            Assert.False(archive.IsEnded());
            Assert.DoesNotContain(issue.IssueQuest, Campaign.Current.QuestManager.Quests);
            Server.Resolve<IPlayerManager>().ClearPeer(Client.NetPeer);
        });

        if (serverStart)
            PublishServerStarted(Server);
        else
            PublishDisconnected(Server, Client);

        Server.Call(() =>
        {
            var ended = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
            Assert.Same(archive, ended);
            Assert.True(ended.IsEnded());
            Assert.True(ended.IsEndedUnsuccessfully());
            Assert.Equal(QuestBase.QuestCompleteDetails.Cancel, ended._questCompletionDetail);
            Assert.Equal("Follow the caravan", Assert.Single(ended.GetEntries()).LogText.ToString());
            Assert.False(unrelated.IsEnded());
            Assert.Null(issue.IssueOwner.Issue);
        });
        if (serverStart)
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        else
            Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Theory]
    [InlineData(IssueBase.IssueState.SolvingWithQuestSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution)]
    public void PlayerDisconnected_KeepsTheIssueOfAnOwnerThatIsStillConnected(IssueBase.IssueState state)
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        AddIssue(Server, notableId, state);
        SetOwner(notableId, ControllerId);

        PublishDisconnected(Server, OtherClient);

        Assert.True(HasIssue(Server, notableId));
        Assert.True(HasOwner(notableId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void PlayerDisconnected_KeepsATroopsIssueOfAnOwnerThatLeft()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterPlayer(ControllerId);
        AddIssue(Server, notableId, IssueBase.IssueState.SolvingWithAlternativeSolution);
        SetOwner(notableId, ControllerId);

        PublishDisconnected(Server, Client);

        Assert.True(HasIssue(Server, notableId));
        Assert.True(HasOwner(notableId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void Clients_IgnoreBothEvents()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Client, notableId, IssueBase.IssueState.SolvingWithQuestSolution);

        PublishServerStarted(Client);
        PublishDisconnected(Client, OtherClient);

        Assert.True(HasIssue(Client, notableId));
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
