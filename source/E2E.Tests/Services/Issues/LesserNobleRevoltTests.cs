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
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;
using GameInterface.Registry.Messages;
using Common.Messaging;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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

    private void RegisterPlayer(string controllerId, EnvironmentInstance client)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        testEnvironment.Server.Call(() => Assert.True(testEnvironment.Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));
        testEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
    }



    // A constructed issue can be finalized, but only the server has the culture names its constructor needs.
    private void AddIssueWithQuest(EnvironmentInstance instance, string issueId, string revoltPartyId = null, bool constructed = false)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            using (new AllowedThread())
            {
                var issue = constructed ? new Issue(giver) : ObjectHelper.SkipConstructor<Issue>();
                issue.IssueOwner = giver;
                issue.StringId = issueId;
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                var quest = ObjectHelper.SkipConstructor<Quest>();
                quest.QuestGiver = giver;
                quest._suitableVillagesToVisitList = new List<Settlement>();
                quest._visitedVillagesList = new List<Settlement>();
                if (revoltPartyId != null)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(revoltPartyId, out var revoltParty));
                    quest._lesserNobleParty = revoltParty;
                }

                issue.IssueQuest = quest;
                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
        });
    }

    private Quest QuestOf(EnvironmentInstance instance)
    {
        Quest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
        });

        return quest;
    }

    private void EnterSettlement(string partyId, string settlementId)
    {
        var server = testEnvironment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            AccessTools.Method(typeof(LesserNobleRevoltWorldEventPatches), nameof(LesserNobleRevoltWorldEventPatches.SettlementEntered))
                .Invoke(null, new object[] { party, settlement, null });
        });
    }

    private void SendVisit(string issueId, string controllerId, string settlementId, int visitCount)
    {
        testEnvironment.Server.Call(() => testEnvironment.Server.Resolve<INetwork>().SendAll(
            new NetworkLesserNobleRevoltJournalUpdate(giverId, issueId, controllerId, settlementId, visitCount, false)));
    }

    // The quest runs on the server as player A's.
    private void RegisterOwner(string issueId, string controllerId)
    {
        var server = testEnvironment.Server;
        RegisterPlayer(controllerId);
        AddIssueWithQuest(server, issueId);
        server.Call(() => Assert.True(server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, controllerId)));
    }

    [Fact]
    public void UnrelatedSettlementVisit_DoesNotAdvanceTheOwnersSupportLog()
    {
        const string issueId = "issue_lesser_noble";
        const string controllerId = "player-A";
        var revoltPartyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        var client = testEnvironment.Clients.First();

        RegisterPlayer(controllerId);
        AddIssueWithQuest(testEnvironment.Server, issueId, revoltPartyId);
        AddIssueWithQuest(client, issueId, revoltPartyId);
        testEnvironment.Server.Call(() =>
        {
            Assert.True(testEnvironment.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, controllerId));
            // Vanilla marks the party it spawns as used by the quest.
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<MobileParty>(revoltPartyId, out var revoltParty));
            revoltParty.SetPartyUsedByQuest(true);
        });

        EnterSettlement(otherPartyId, settlementId);

        Assert.Empty(testEnvironment.Server.NetworkSentMessages.GetMessages<NetworkLesserNobleRevoltJournalUpdate>());
        Assert.Empty(QuestOf(client)._visitedVillagesList);

        EnterSettlement(revoltPartyId, settlementId);

        var update = Assert.Single(testEnvironment.Server.NetworkSentMessages.GetMessages<NetworkLesserNobleRevoltJournalUpdate>());
        Assert.Equal(1, update.VisitCount);
        Assert.Single(QuestOf(client)._visitedVillagesList);
    }

    [Fact]
    public void RevisitCountsAgainButARepeatedVisitPacketDoesNot()
    {
        const string issueId = "issue_lesser_noble";
        const string controllerId = "player-A";
        var revoltPartyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        var client = testEnvironment.Clients.First();

        RegisterPlayer(controllerId);
        AddIssueWithQuest(client, issueId, revoltPartyId);

        SendVisit(issueId, controllerId, settlementId, 1);
        SendVisit(issueId, controllerId, settlementId, 2);
        SendVisit(issueId, controllerId, settlementId, 2);

        // Vanilla counts a second visit to the same village, but the same visit only once.
        Assert.Equal(2, QuestOf(client)._visitedVillagesList.Count);
        Assert.Equal(9, QuestOf(client)._giveNotificationAfterHoursCounter);
    }

    [Fact]
    public void AWarAndAKingdomChangeReachTheOwnersQuestOnceOnTheServer()
    {
        const string issueId = "issue_lesser_noble";
        const string controllerId = "player-A";
        var server = testEnvironment.Server;
        var otherClanId = testEnvironment.CreateRegisteredObject<Clan>();

        RegisterOwner(issueId, controllerId);
        using var wars = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared)));
        using var kingdoms = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom)));

        // The framework relay runs the quest's own listeners as its owner, the quest adds no copy of its own.
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            CampaignEventDispatcher.Instance.OnWarDeclared(giver.MapFaction, giver.MapFaction, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
            // Another clan's change, so vanilla's listener runs through without needing the old kingdom.
            Assert.True(server.ObjectManager.TryGetObject<Clan>(otherClanId, out var otherClan));
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(otherClan, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        });

        Assert.Equal(1, wars.CountFor(server));
        Assert.Equal(1, kingdoms.CountFor(server));
    }

    [Fact]
    public void KingdomCancellation_DoesNotChangeAnObserverRelation()
    {
        const string issueId = "issue_lesser_noble_observer";
        RegisterPlayer("player-A", testEnvironment.Clients.First());
        RegisterPlayer("player-B", testEnvironment.Clients.Last());
        AddIssueWithQuest(testEnvironment.Server, issueId);

        testEnvironment.Server.Call(() =>
        {
            Assert.True(testEnvironment.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, "player-A"));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(testEnvironment.Server.Resolve<IPlayerManager>().TryGetPlayer("player-A", out var ownerPlayer));
            Assert.True(testEnvironment.Server.Resolve<IPlayerManager>().TryGetPlayer("player-B", out var observerPlayer));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<Hero>(ownerPlayer.HeroId, out var owner));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<Hero>(observerPlayer.HeroId, out var observer));
            var ownerBefore = owner.GetRelation(giver);
            var observerBefore = observer.GetRelation(giver);
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(null, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
            Assert.Equal(ownerBefore, owner.GetRelation(giver));
            Assert.Equal(observerBefore, observer.GetRelation(giver));
        });
    }



    [Fact]
    public void OwnerInARevoltBattle_IsNeitherCapturedNorParked()
    {
        // The guard must run before coop's capture prefix whatever order the patches were applied in.
        var prefixes = Harmony.GetPatchInfo(
            AccessTools.Method(typeof(TakePrisonerAction), nameof(TakePrisonerAction.ApplyInternal))).Prefixes;
        var guard = Assert.Single(prefixes, prefix => prefix.PatchMethod == AccessTools.Method(typeof(LesserNobleRevoltAcceptPatches), nameof(LesserNobleRevoltAcceptPatches.ProtectOwnerFromCapture)));
        Assert.All(prefixes.Where(prefix => prefix != guard), prefix => Assert.True(guard.priority > prefix.priority));

        var server = testEnvironment.Server;
        AddIssueWithQuest(server, "issue_lesser_noble");
        var quest = QuestOf(server);
        string ownerPartyId = null;
        string captorPartyId = null;

        server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var captorParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(server.ObjectManager.TryGetId(captorParty, out captorPartyId));
        });
        testEnvironment.FlushCoalescer();

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));
            var owner = ownerParty.LeaderHero;
            Assert.True(server.ObjectManager.TryGetId(owner, out var heroId));
            Assert.True(server.ObjectManager.TryGetId(ownerParty.ActualClan, out var clanId));
            Assert.True(server.ObjectManager.TryGetId(owner.CharacterObject, out var characterId));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, ownerPartyId, clanId, characterId)));

            LesserNobleRevoltWorldEventPatches.RecordOwnerBattle(quest, owner, null);
            try
            {
                // What coop's CaptureDefeatedPlayerHeroes does to a player who lost a battle.
                TakePrisonerAction.Apply(captorParty.Party, owner);

                Assert.False(owner.IsPrisoner);
                Assert.Same(ownerParty, owner.PartyBelongedTo);
                Assert.True(ownerParty.IsActive);
            }
            finally
            {
                LesserNobleRevoltWorldEventPatches.ForgetOwnerBattle(quest);
            }
        });
    }

    [Fact]
    public void OwnRevoltParty_NeverCapturesItsOwner()
    {
        const string issueId = "issue_lesser_noble";
        var server = testEnvironment.Server;
        string ownerPartyId = null;
        string revoltPartyId = null;

        server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var revoltParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(server.ObjectManager.TryGetId(revoltParty, out revoltPartyId));
        });
        testEnvironment.FlushCoalescer();
        AddIssueWithQuest(server, issueId, revoltPartyId);

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(revoltPartyId, out var revoltParty));
            var owner = ownerParty.LeaderHero;
            Assert.True(server.ObjectManager.TryGetId(owner, out var heroId));
            Assert.True(server.ObjectManager.TryGetId(ownerParty.ActualClan, out var clanId));
            Assert.True(server.ObjectManager.TryGetId(owner.CharacterObject, out var characterId));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, ownerPartyId, clanId, characterId)));
            Assert.True(server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, "player-A"));

            // No battle of the owner's own is recorded: another player started it, and the owner joined and lost.
            TakePrisonerAction.Apply(revoltParty.Party, owner);

            Assert.False(owner.IsPrisoner);
            Assert.Same(ownerParty, owner.PartyBelongedTo);
        });
    }

    [Fact]
    public void BattleEnds_RunTheMatchingBranchOnTheServer()
    {
        var server = testEnvironment.Server;
        AddIssueWithQuest(server, "issue_lesser_noble");
        var quest = QuestOf(server);
        using var wins = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.QuestSuccessWithPlayerDefeatedLesserNobleParty)));
        using var losses = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.QuestFailWithPlayerDefeatedAgainstLesserNobleParty)));

        server.Call(() =>
        {
            LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.Ignored);
            Assert.False(quest._firstTalkIsDone);

            LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.NoWinner);
            Assert.True(quest._firstTalkIsDone);

            LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.OwnerLost);
            Assert.Equal(0, wins.CountFor(server));
            Assert.Equal(1, losses.CountFor(server));

            LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.OwnerWon);
            LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.ThirdPartyWon);
        });

        Assert.Equal(2, wins.CountFor(server));
        Assert.Equal(1, losses.CountFor(server));
    }

    [Fact]
    public void ABranchOnTheOwnersClient_IsOnlyRequestedFromTheServer()
    {
        const string issueId = "issue_lesser_noble";
        var client = testEnvironment.Clients.First();
        RegisterPlayer("player-A");
        AddIssueWithQuest(client, issueId);

        // Vanilla would pay the reward here, the client only asks the server for the branch.
        client.Call(() => QuestOf(client).QuestSuccessWithPlayerDefeatedLesserNobleParty());

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal((byte)LesserNobleRevoltBranch.SuccessByBattle, request.Proof);
        Assert.Equal(issueId, request.IssueId);
    }

    [Fact]
    public void DueTime_TimesTheQuestOutOnTheServerOnce()
    {
        const string issueId = "issue_lesser_noble";
        var server = testEnvironment.Server;
        RegisterOwner(issueId, "player-A");
        var quest = QuestOf(server);
        using var timeouts = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut)));
        var hourlyTick = AccessTools.Method(typeof(LesserNobleRevoltWorldEventPatches), nameof(LesserNobleRevoltWorldEventPatches.HourlyTick));

        server.Call(() =>
        {
            quest.QuestDueTime = CampaignTime.Never;
            hourlyTick.Invoke(null, null);
            Assert.Equal(0, timeouts.CountFor(server));

            // The test campaign has no time model, so the due time is set from raw ticks.
            quest.QuestDueTime = new CampaignTime(CampaignTime.Now.NumTicks - 1);
            hourlyTick.Invoke(null, null);
        });

        Assert.Equal(1, timeouts.CountFor(server));
    }

    [Fact]
    public void DismissedQuest_ReleasesItsOwnersCaptureProtectionAndItsParty()
    {
        var server = testEnvironment.Server;
        var ownerId = testEnvironment.CreateRegisteredObject<Hero>();
        var revoltPartyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        AddIssueWithQuest(server, "issue_lesser_noble", revoltPartyId, constructed: true);
        var quest = QuestOf(server);
        using var destroyed = new MethodCallRecorder(AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            using (new AllowedThread())
            {
                quest._lesserNobleParty.IsActive = true;
            }

            LesserNobleRevoltWorldEventPatches.RecordOwnerBattle(quest, owner, null);
            Assert.True(LesserNobleRevoltWorldEventPatches.IsProtectedHero(owner));

            // Nobody owns the quest, so the server-start sweep dismisses it.
            server.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered());

            Assert.Null(giver.Issue);
            Assert.False(LesserNobleRevoltWorldEventPatches.IsProtectedHero(owner));
        });

        Assert.Equal(1, destroyed.CountFor(server));
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
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            Assert.Same(party, quest._lesserNobleParty);
        });
    }

    [Fact]
    public void FrameworkRegistersTheQuestAndRoundTripsItsCreationCapture()
    {
        var server = testEnvironment.Server;

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

        // The issue is rebuilt on the server, the only instance with the culture names the constructor needs.
        server.Call(() =>
        {
            Assert.True(server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Issue created = null;
            using (new AllowedThread())
            {
                created = Assert.IsType<Issue>(descriptor.CreationCaptureStrategy.CreateIssue(giver, captured));
            }

            Assert.Equal("Noble A", created._lesserNobleName.ToString());
            Assert.Equal(CampaignTime.Days(6f), created.IssueCreationTime);
            Assert.Equal(CampaignTime.Days(26f), created.IssueDueTime);
        });
    }
}
