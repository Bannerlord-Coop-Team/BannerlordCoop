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
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
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

    // MapEvent.Initialize runs the battle visual's Initialize, which needs a render context the test doesnt have.
    private static readonly MethodBase[] MapEventVisualMethods =
    {
        AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
    };

    // Player A owns the quest with a real party on the first client, player B on the last client only watches.
    private (string OwnerPartyId, string RevoltPartyId) RegisterOwnerWithRevoltParty(string issueId)
    {
        var server = testEnvironment.Server;
        var ownerClient = testEnvironment.Clients.First();
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

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var owner = ownerParty.LeaderHero;
            Assert.True(server.ObjectManager.TryGetId(owner, out var heroId));
            Assert.True(server.ObjectManager.TryGetId(ownerParty.ActualClan, out var clanId));
            Assert.True(server.ObjectManager.TryGetId(owner.CharacterObject, out var characterId));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, ownerPartyId, clanId, characterId)));
        });
        testEnvironment.ConnectRegisteredPlayer(ownerClient, "player-A");
        ownerClient.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        RegisterPlayer("player-B", testEnvironment.Clients.Last());

        foreach (var instance in testEnvironment.Clients.Append(server))
        {
            AddIssueWithQuest(instance, issueId, revoltPartyId);
        }

        server.Call(() => Assert.True(server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, "player-A")));
        return (ownerPartyId, revoltPartyId);
    }

    // Patches stay live, so Initialize fires CampaignEventDispatcher.OnMapEventStarted like MapEventBattleFactory does.
    private string StartServerBattle(string attackerPartyId, string defenderPartyId)
    {
        var server = testEnvironment.Server;
        string mapEventId = null;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attacker));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(defenderPartyId, out var defender));
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(attacker.Party, defender.Party, new FieldBattleEventComponent(mapEvent), MapEvent.BattleTypes.FieldBattle);
            mapEvent.MapEventVisual = null;
            if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
            {
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);
            }

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventVisualMethods);

        foreach (var instance in testEnvironment.Clients.Append(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                mapEvent.Position = new CampaignVec2(default, isOnLand: true);
            });
        }

        return mapEventId;
    }

    // The encounter menu's Leave ends in FinalizeEvent on the owner's client, which asks the server to finalize.
    private void LeaveBattleOnOwnersClient(string mapEventId)
    {
        var ownerClient = testEnvironment.Clients.First();
        ownerClient.Call(() =>
        {
            Assert.True(ownerClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            mapEvent.FinalizeEvent();
        }, MapEventVisualMethods);

        Assert.False(testEnvironment.Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
    }

    public enum RevoltEnding
    {
        BattleWon,
        BattleLost,
        Agreement,
        Release,
        TimedOut,
        CanceledByWar,
    }

    private const string StartLog = "Chaghan is stirring up unrest";

    // Player A runs the quest on the first client and player B only mirrors the issue, like after A's accept.
    private (EnvironmentInstance Owner, EnvironmentInstance Observer) RegisterOwnerAndObserver(string issueId)
    {
        var server = testEnvironment.Server;
        var owner = testEnvironment.Clients.First();
        var observer = testEnvironment.Clients.Last();
        RegisterPlayer("player-A", owner);
        RegisterPlayer("player-B", observer);
        AddIssueWithQuest(server, issueId, constructed: true);
        AddIssueWithQuest(owner, issueId);
        AddIssueWithQuest(observer, issueId);

        foreach (var instance in new[] { server, owner, observer })
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, "player-A"));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                var issue = giver.Issue;
                var quest = Assert.IsType<Quest>(issue.IssueQuest);

                // A skipped constructor leaves the stores an ending writes to null.
                if (issue.JournalEntries == null)
                {
                    AccessTools.Field(typeof(IssueBase), nameof(IssueBase._journalEntries)).SetValue(issue, new MBList<JournalLog>());
                    AccessTools.Field(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionSentTroops)).SetValue(issue, TroopRoster.CreateDummyTroopRoster());
                }

                AccessTools.Field(typeof(QuestBase), nameof(QuestBase._journalEntries)).SetValue(quest, new MBList<JournalLog>());
                AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold)).SetValue(quest, 5700);
                quest._taskList = new MBList<QuestTaskBase>();
                quest._lesserNobleName = new TextObject("Chaghan");
                if (instance != server)
                {
                    new JournalLogsCampaignBehavior().RegisterEvents();
                }

                if (instance == observer)
                {
                    using (new AllowedThread())
                    {
                        issue.IssueQuest = null;
                    }

                    return;
                }

                // The server and the owner both start with the support log.
                quest._journalEntries.Add(new JournalLog(CampaignTime.Now, new TextObject(StartLog)));
                if (instance == owner)
                {
                    using (new AllowedThread())
                    {
                        quest.StartQuest();
                    }

                    Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
                }
            });
        }

        return (owner, observer);
    }

    private static TextObject EndingLog(Quest quest, RevoltEnding ending) => ending switch
    {
        RevoltEnding.BattleWon => quest.QuestSuccessWithPlayerDefeatedNobleParty,
        RevoltEnding.BattleLost => quest.QuestFailWithPlayerDefeatedAgainstNobleParty,
        RevoltEnding.Agreement => quest.QuestSuccessPlayerComesToAnAgreementWithLesserNoblePartyLog,
        RevoltEnding.Release => quest.QuestFailedAfterTalkingWithLesserNoblePartyLog,
        RevoltEnding.TimedOut => quest.QuestFailedWithTimeOutLogText,
        _ => quest.QuestCanceledWarDeclared,
    };

    // Each ending takes the path the live game takes to the server's branch.
    private void End(RevoltEnding ending, EnvironmentInstance owner)
    {
        var server = testEnvironment.Server;
        var ownerQuest = QuestOf(owner);
        var serverQuest = QuestOf(server);
        void RunAsOwner(Action<Quest> listener) =>
            server.Call(() => server.Resolve<IOutcomeDispatcher>().RunAsOwners(listener));

        switch (ending)
        {
            case RevoltEnding.BattleWon:
                RunAsOwner(quest => LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.OwnerWon));
                break;
            case RevoltEnding.BattleLost:
                RunAsOwner(quest => LesserNobleRevoltWorldEventPatches.ApplyBattleEnd(quest, LesserNobleRevoltWorldEventPatches.RevoltBattleEnd.OwnerLost));
                break;
            case RevoltEnding.Agreement:
                owner.Call(() => ownerQuest.QuestSuccessPlayerComesToAnAgreementWithLesserNobleParty());
                break;
            case RevoltEnding.Release:
                owner.Call(() => ownerQuest.QuestFailedAfterTalkingWithLesserNobleParty());
                break;
            case RevoltEnding.TimedOut:
                server.Call(() =>
                {
                    serverQuest.QuestDueTime = new CampaignTime(CampaignTime.Now.NumTicks - 1);
                    LesserNobleRevoltWorldEventPatches.HourlyTick();
                });
                break;
            case RevoltEnding.CanceledByWar:
                // What OnClanChangedKingdom and QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest call when the realms go to war.
                RunAsOwner(quest => quest.CompleteQuestWithCancel(quest.QuestCanceledWarDeclared));
                break;
        }

        testEnvironment.FlushCoalescer();
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
    public void OwnerRetreat_EndsTheFirstTalkOnTheOwnersClient()
    {
        const string issueId = "issue_lesser_noble";
        var server = testEnvironment.Server;
        var (ownerPartyId, revoltPartyId) = RegisterOwnerWithRevoltParty(issueId);

        // The owner attacks, like vanilla's "I will talk no more" consequence.
        var mapEventId = StartServerBattle(ownerPartyId, revoltPartyId);
        Assert.True(QuestOf(server)._checkForEventEnd);

        LeaveBattleOnOwnersClient(mapEventId);

        var ended = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkLesserNobleRevoltOwnerBattleEnded>());
        Assert.Equal(giverId, ended.IssueOwnerId);
        Assert.Equal(issueId, ended.IssueId);
        Assert.Equal("player-A", ended.ControllerId);
        Assert.True(QuestOf(server)._firstTalkIsDone);
        Assert.False(QuestOf(server)._checkForEventEnd);

        // Vanilla's next talk reads these on the owner's client, so it opens on the "snake" line.
        var ownerQuest = QuestOf(testEnvironment.Clients.First());
        Assert.True(ownerQuest._firstTalkIsDone);
        Assert.False(ownerQuest._checkForEventEnd);
        Assert.False(QuestOf(testEnvironment.Clients.Last())._firstTalkIsDone);
    }

    [Fact]
    public void RevoltAttackOnTheOwner_KeepsTheFirstTalkAfterTheOwnerLeaves()
    {
        const string issueId = "issue_lesser_noble";
        var server = testEnvironment.Server;
        var (ownerPartyId, revoltPartyId) = RegisterOwnerWithRevoltParty(issueId);

        // Vanilla only waits for a battle the owner started against the revolt party.
        var mapEventId = StartServerBattle(revoltPartyId, ownerPartyId);
        Assert.False(QuestOf(server)._checkForEventEnd);

        LeaveBattleOnOwnersClient(mapEventId);

        Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkLesserNobleRevoltOwnerBattleEnded>());
        Assert.False(QuestOf(server)._firstTalkIsDone);
        Assert.False(QuestOf(testEnvironment.Clients.First())._firstTalkIsDone);
    }

    [Fact]
    public void OwnerBattleEnded_ForAnotherPlayerOrAnEndedIssue_LeavesTheQuestAlone()
    {
        const string issueId = "issue_lesser_noble";
        var client = testEnvironment.Clients.First();
        RegisterPlayer("player-A");
        AddIssueWithQuest(client, issueId);

        void Send(string sentIssueId, string controllerId) => testEnvironment.Server.Call(() =>
            testEnvironment.Server.Resolve<INetwork>().SendAll(
                new NetworkLesserNobleRevoltOwnerBattleEnded(giverId, sentIssueId, controllerId)));

        Send(issueId, "player-B");
        // The giver's earlier issue ended and this one replaced it.
        Send("issue_lesser_noble_old", "player-A");
        var quest = QuestOf(client);
        Assert.False(quest._firstTalkIsDone);

        client.Call(() => quest._checkForEventEnd = true);
        Send(issueId, "player-A");
        Send(issueId, "player-A");

        Assert.True(quest._firstTalkIsDone);
        Assert.False(quest._checkForEventEnd);
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
    public void DismissedQuest_ReleasesItsOwnersCaptureProtectionItsPartyAndItsTrackers()
    {
        var server = testEnvironment.Server;
        var ownerId = testEnvironment.CreateRegisteredObject<Hero>();
        var revoltPartyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        var villageId = testEnvironment.CreateRegisteredObject<Settlement>();
        var otherVillageId = testEnvironment.CreateRegisteredObject<Settlement>();
        AddIssueWithQuest(server, "issue_lesser_noble", revoltPartyId, constructed: true);
        var quest = QuestOf(server);
        using var destroyed = new MethodCallRecorder(AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(villageId, out var village));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(otherVillageId, out var otherVillage));
            var otherQuest = ObjectHelper.SkipConstructor<Quest>();
            using (new AllowedThread())
            {
                quest._lesserNobleParty.IsActive = true;

                // The server's mirror quest tracks its village like the constructor does, and the save keeps both stores.
                Campaign.Current.VisualTrackerManager = new VisualTrackerManager();
                quest.IsTrackEnabled = true;
                quest.AddTrackedObject(village);
                otherQuest.IsTrackEnabled = true;
                otherQuest.AddTrackedObject(otherVillage);
            }

            Assert.Contains(quest, Campaign.Current.QuestManager.TrackedObjects[village]);
            Assert.True(Campaign.Current.VisualTrackerManager.CheckTracked(village));

            LesserNobleRevoltWorldEventPatches.RecordOwnerBattle(quest, owner, null);
            Assert.True(LesserNobleRevoltWorldEventPatches.IsProtectedHero(owner));

            // Nobody owns the quest, so the server-start sweep dismisses it.
            server.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered());

            Assert.Null(giver.Issue);
            Assert.False(LesserNobleRevoltWorldEventPatches.IsProtectedHero(owner));
            Assert.DoesNotContain(Campaign.Current.QuestManager.TrackedObjects.Values, quests => quests.Contains(quest));
            Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(village));
            Assert.Contains(otherQuest, Campaign.Current.QuestManager.TrackedObjects[otherVillage]);
            Assert.True(Campaign.Current.VisualTrackerManager.CheckTracked(otherVillage));
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

    [Theory]
    [InlineData(RevoltEnding.BattleWon, IssueOutcome.QuestSuccess, 1, 1)]
    [InlineData(RevoltEnding.BattleLost, IssueOutcome.QuestFail, 0, 1)]
    [InlineData(RevoltEnding.Agreement, IssueOutcome.QuestSuccess, 1, 1)]
    [InlineData(RevoltEnding.Release, IssueOutcome.QuestFail, 0, 0)]
    [InlineData(RevoltEnding.TimedOut, IssueOutcome.QuestFail, 0, 1)]
    [InlineData(RevoltEnding.CanceledByWar, IssueOutcome.QuestCancel, 0, 0)]
    public void ServerEnding_AddsItsJournalLineOnceToTheOwnersJournalOnly(RevoltEnding ending, IssueOutcome outcome, int payments, int traitChanges)
    {
        const string issueId = "issue_lesser_noble";
        var server = testEnvironment.Server;
        var (owner, observer) = RegisterOwnerAndObserver(issueId);
        var serverQuest = QuestOf(server);
        var ownerQuest = QuestOf(owner);
        var expected = string.Empty;
        server.Call(() => expected = EndingLog(serverQuest, ending).ToString());

        // These pay the owner, so the server runs each once and no client runs them again.
        using var gold = new MethodCallRecorder(AccessTools.Method(typeof(GiveGoldAction), nameof(GiveGoldAction.ApplyBetweenCharacters)));
        using var renown = new MethodCallRecorder(AccessTools.Method(typeof(GainRenownAction), nameof(GainRenownAction.Apply)));
        using var traits = new MethodCallRecorder(AccessTools.Method(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.OnIssueSolvedThroughQuest),
            new[] { typeof(Hero), typeof(Tuple<TraitObject, int>[]) }));
        // The server's release leaves an encounter the test campaign doesnt have.
        using var encounters = new MethodCallRecorder(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));

        End(ending, owner);

        var finalized = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(outcome, finalized.Outcome);
        var endingLog = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkLesserNobleRevoltEndingLog>());
        Assert.Equal("player-A", endingLog.ControllerId);
        Assert.Equal(issueId, endingLog.IssueId);
        // Both go out on the reliable ordered channel, so the owner adds the line before its replay archives the quest.
        var sent = server.NetworkSentMessages.Messages;
        Assert.True(sent.FindIndex(message => message is NetworkLesserNobleRevoltEndingLog) < sent.FindIndex(message => message is NetworkIssueFinalized));
        server.Call(() => Assert.Single(serverQuest.JournalEntries, log => log.LogText.ToString() == expected));

        owner.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Single(ownerQuest.JournalEntries, log => log.LogText.ToString() == StartLog);
            Assert.Single(ownerQuest.JournalEntries, log => log.LogText.ToString() == expected);

            // Old Quests shows the copy vanilla's journal takes when the quest completes.
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.RelatedHero == ownerQuest.QuestGiver);
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == expected);
        });

        observer.Call(() =>
        {
            Assert.True(observer.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.Null(giver.Issue);
            Assert.Null(Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.RelatedHero == giver));
        });

        foreach (var recorder in new[] { gold, renown, traits })
        {
            Assert.Equal(0, recorder.CountFor(owner));
            Assert.Equal(0, recorder.CountFor(observer));
        }

        Assert.Equal(payments, gold.CountFor(server));
        Assert.Equal(payments, renown.CountFor(server));
        Assert.Equal(traitChanges, traits.CountFor(server));
    }

    [Fact]
    public void EndingLog_AddsOnlyTheServerLinesPastTheOwnersJournal()
    {
        const string issueId = "issue_lesser_noble";
        var client = testEnvironment.Clients.First();
        RegisterPlayer("player-A");
        AddIssueWithQuest(client, issueId);
        var quest = QuestOf(client);
        client.Call(() =>
        {
            AccessTools.Field(typeof(QuestBase), nameof(QuestBase._journalEntries)).SetValue(quest, new MBList<JournalLog>());
            quest._journalEntries.Add(new JournalLog(CampaignTime.Now, new TextObject(StartLog)));
        });

        void Send(string sentIssueId, string controllerId, params string[] logs) => testEnvironment.Server.Call(() =>
            testEnvironment.Server.Resolve<INetwork>().SendAll(new NetworkLesserNobleRevoltEndingLog(
                giverId, sentIssueId, controllerId, logs.Select(log => new TextObject(log)).ToArray())));

        Send(issueId, "player-B", StartLog, "ending");
        // The giver's earlier issue ended and this one replaced it.
        Send("issue_lesser_noble_old", "player-A", StartLog, "ending");
        // A line with the same text as an earlier one is still a new line, and the same journal twice adds it once.
        Send(issueId, "player-A", StartLog, StartLog);
        Send(issueId, "player-A", StartLog, StartLog);

        client.Call(() => Assert.Equal(new[] { StartLog, StartLog }, quest.JournalEntries.Select(log => log.LogText.ToString())));
    }
}
