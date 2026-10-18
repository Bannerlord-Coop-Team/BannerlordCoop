using Autofac;
using Common.Network;
using Common.Util;
using E2E.Tests.Util;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Collections.Generic;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace E2E.Tests.Services.Issues;

public sealed class LesserNobleRevoltTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private readonly string giverId;

    public LesserNobleRevoltTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        giverId = testEnvironment.CreateRegisteredObject<Hero>();
    }

    public void Dispose() => testEnvironment.Dispose();

    private void RegisterPlayer(string controllerId)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        testEnvironment.Server.Call(() => Assert.True(testEnvironment.Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));
        testEnvironment.ConnectRegisteredPlayer(testEnvironment.Clients.First(), controllerId);
        testEnvironment.Clients.First().Resolve<IControllerIdProvider>().SetControllerId(controllerId);
    }

    private void AddIssueWithQuest(EnvironmentInstance instance, string issueId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            using (new AllowedThread())
            {
                var issue = ObjectHelper.SkipConstructor<Issue>();
                issue.IssueOwner = giver;
                issue.StringId = issueId;
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                var quest = ObjectHelper.SkipConstructor<Quest>();
                quest.QuestGiver = giver;
                quest._suitableVillagesToVisitList = new List<Settlement>();
                quest._visitedVillagesList = new List<Settlement>();
                issue.IssueQuest = quest;
                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
        });
    }

    [Fact]
    public void OwnerPartyCreated_AttachesTheServerPartyToTheOwningQuest()
    {
        const string issueId = "issue_lesser_noble";
        const string controllerId = "player-A";
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();

        RegisterPlayer(controllerId);
        AddIssueWithQuest(testEnvironment.Server, issueId);
        AddIssueWithQuest(testEnvironment.Clients.First(), issueId);

        testEnvironment.Server.Call(() =>
        {
            Assert.True(testEnvironment.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, controllerId));
            testEnvironment.Server.Resolve<INetwork>().SendAll(
                new NetworkLesserNobleRevoltPartyCreated(giverId, issueId, controllerId, partyId));
        });

        testEnvironment.Clients.First().Call(() =>
        {
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(party, Assert.IsType<Quest>(giver.Issue.IssueQuest)._lesserNobleParty);
        });
    }
    [Fact]
    public void FrameworkRegistersTheQuestAndReplicatesItsCreationCapture()
    {
        var server = testEnvironment.Server;
        var client = testEnvironment.Clients.First();

        server.Call(() =>
        {
            Assert.True(server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
            Assert.IsType<LesserNobleRevoltCreationCaptureStrategy>(descriptor.CreationCaptureStrategy);
            Assert.IsType<LesserNobleRevoltQuestSolutionAcceptStrategy>(descriptor.QuestSolutionAcceptStrategy);
            Assert.NotNull(descriptor.FinalizationProofStrategy);
        });

        byte[] captured = Array.Empty<byte>();
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var issue = new Issue(giver);
            issue._lesserNobleName = new TextObject("Noble A");
            issue.IssueCreationTime = CampaignTime.Days(6f);
            issue.IssueDueTime = CampaignTime.Days(26f);
            captured = server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor)
                ? descriptor.CreationCaptureStrategy.Capture(issue)
                : null;
        });

        client.Call(() =>
        {
            Assert.True(client.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
            Assert.NotEmpty(captured);
            Assert.NotEmpty(captured);
        });
    }
}
