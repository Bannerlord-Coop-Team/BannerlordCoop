using Common.Util;
using E2E.Tests.Environment;
using Common;
using Common.Messaging;
using Common.Network.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.EscortMerchantCaravan;
using GameInterface.Services.Players;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace E2E.Tests.Services.Issues;

public class EscortMerchantCaravanTests : IDisposable
{
    private readonly struct DeferredPartyRegistration : IEvent { }
    private static MobileParty? observedHourlyParty;
    private static PartyBase? observedAttacker;
    private static int observedReroutes;

    private static void ObserveReroute() => observedReroutes++;
    private static readonly MethodBase[] WithoutWorldSetup =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SpawnCaravan)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly MethodCallRecorder spawn;
    private readonly string caravanId;

    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;

    public EscortMerchantCaravanTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        spawn = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.SpawnCaravan)));
        scenario = new QuestScenario(environment, hero => new Issue(hero));
        caravanId = environment.CreateRegisteredObject<MobileParty>();

        var townId = environment.CreateRegisteredObject<Settlement>();
        var townComponentId = environment.CreateRegisteredObject<Town>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townComponentId, out var townComponent));
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    giver.StayingInSettlement = town;
                    giver.Occupation = Occupation.Merchant;
                    giver.Clan = clan;
                    town.Town = townComponent;
                    town.Town.Security = 50;
                    town.Town.Prosperity = 1000;
                }
            });
        }
        foreach (var controllerId in new[] { QuestScenario.ControllerA, QuestScenario.ControllerB })
        {
            var player = Server.Resolve<IPlayerManager>().Players.Single(p => p.ControllerId == controllerId);
            foreach (var instance in new[] { Server, Owner, Other })
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                    using (new AllowedThread())
                    {
                        hero._partyBelongedTo = party;
                        if ((instance == Owner && controllerId == QuestScenario.ControllerA) ||
                            (instance == Other && controllerId == QuestScenario.ControllerB))
                        {
                            Campaign.Current.MainParty = party;
                            Game.Current.PlayerTroop = hero.CharacterObject;
                            Campaign.Current.PlayerDefaultFaction = hero.Clan;
                        }
                    }
                });
            }
        }
    }

    public void Dispose()
    {
        spawn.Dispose();
        environment.Dispose();
    }

    private void Accept()
    {
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            giver.Issue.StartIssueWithQuest();
            ((Quest)giver.Issue.IssueQuest).QuestAcceptedConsequences();
        }, WithoutWorldSetup);
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }

    private Quest QuestOn(EnvironmentInstance instance) => Assert.IsType<Quest>(scenario.QuestOf(instance));

    [Fact]
    public void RealAcceptKeepsTheQuestOnItsOwnerAndSpawnsOnlyOnTheServer()
    {
        Accept();
        Assert.Equal(1, spawn.CountFor(Server));
        Assert.Equal(0, spawn.CountFor(Owner));
        Assert.Equal(0, spawn.CountFor(Other));
        Owner.Call(() => Assert.Contains(QuestOn(Owner), Campaign.Current.QuestManager.Quests));
        Server.Call(() => Assert.DoesNotContain(QuestOn(Server), Campaign.Current.QuestManager.Quests));
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.Null(giver.Issue.IssueQuest);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheFrameworkAllowsTwoOwnersButRefusesASecondEscortForTheSamePlayer(bool samePlayer)
    {
        Accept();
        var secondId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(secondId, out var second));
                using (new AllowedThread())
                {
                    second.StayingInSettlement = giver.CurrentSettlement;
                    second.Clan = giver.Clan;
                    var issue = new Issue(second);
                    issue.StringId = "issue_second_escort";
                    Campaign.Current.IssueManager._issues.Add(second, issue);
                    second.OnIssueCreatedForHero(issue);
                }
            });
        }
        var accepter = samePlayer ? Owner : Other;
        accepter.Call(() =>
        {
            Assert.True(accepter.ObjectManager.TryGetObject<Hero>(secondId, out var giver));
            giver.Issue.StartIssueWithQuest();
            if (giver.Issue.IssueQuest is Quest quest && quest.IsOngoing) quest.QuestAcceptedConsequences();
        }, WithoutWorldSetup);

        if (samePlayer)
        {
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        }
        else
        {
            Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
            Server.Call(() =>
            {
                Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondId, "issue_second_escort", out var owner));
                Assert.Equal(QuestScenario.ControllerB, owner);
            });
            Owner.Call(() =>
            {
                Assert.True(Owner.ObjectManager.TryGetObject<Hero>(secondId, out var giver));
                Assert.Null(giver.Issue.IssueQuest);
            });
            Other.Call(() =>
            {
                Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.Null(giver.Issue.IssueQuest);
            });
        }
    }

    [Fact]
    public void TheUnstartedServerQuestReceivesTheDailyTickAsItsOwner()
    {
        Accept();
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
            quest._questCaravanMobileParty = caravan;
            var days = quest._daysSpentForEscorting;
            Campaign.Current.QuestManager.DailyTick();
            Assert.Equal(days + 1, quest._daysSpentForEscorting);
        });
        Owner.Call(() => Assert.Equal(2, QuestOn(Owner)._daysSpentForEscorting));
    }

    [Fact]
    public void ARejectedAcceptCannotRestartFromTheDelayedNpcConsequence()
    {
        Accept();
        Quest rejected = null!;
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            giver.Issue.StartIssueWithQuest();
            rejected = Assert.IsType<Quest>(giver.Issue.IssueQuest);
        });
        Other.Call(() =>
        {
            Assert.True(rejected.IsFinalized);
            rejected.QuestAcceptedConsequences();
            Assert.True(rejected.IsFinalized);
            Assert.DoesNotContain(rejected, Campaign.Current.QuestManager.Quests);
        });
        Assert.Equal(1, spawn.CountFor(Server));
    }

    [Fact]
    public void TheServerPublishesCaravanAndVisitedSettlementProgressOnlyToTheOwner()
    {
        Accept();
        CampaignTime due = default;
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
            quest._questCaravanMobileParty = caravan;
            quest._daysSpentForEscorting = 4;
            quest._visitedSettlements.Add(quest.QuestGiver.CurrentSettlement);
            quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, 1);
            due = CampaignTime.Now + CampaignTime.Days(7);
            quest.QuestDueTime = due;
            Server.Resolve<IEscortProgress>().Publish(quest);
        });
        Owner.Call(() =>
        {
            var quest = QuestOn(Owner);
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
            Assert.Same(caravan, quest._questCaravanMobileParty);
            Assert.Equal(4, quest._daysSpentForEscorting);
            Assert.Single(quest._visitedSettlements);
            Assert.Equal(1, quest._playerStartsQuestLog.CurrentProgress);
            Assert.Equal(due.NumTicks, quest.QuestDueTime.NumTicks);
        });
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.Null(giver.Issue.IssueQuest);
        });
    }

    [Fact]
    public void CancellationBeforeTheNpcConsequenceCleansUpTheUnstartedQuest()
    {
        Quest pending = null!;
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            giver.Issue.StartIssueWithQuest();
            pending = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            pending.AddTrackedObject(giver);
            Assert.DoesNotContain(pending, Campaign.Current.QuestManager.Quests);
        }, WithoutWorldSetup);
        Server.Call(() => Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(q => q.CompleteQuestWithCancel()));
        Owner.Call(() =>
        {
            Assert.True(pending.IsFinalized);
            Assert.Null(pending.OfferDialogFlow);
            Assert.Null(pending.DiscussDialogFlow);
            Assert.Empty(Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(pending));
            pending.QuestAcceptedConsequences();
            Assert.DoesNotContain(pending, Campaign.Current.QuestManager.Quests);
            Assert.True(pending.IsFinalized);
        });
    }

    [Fact]
    public void BanditsBelongToOnlyOneActiveEscortAndBecomeAvailableWhenReleased()
    {
        Accept();
        using var recruitment = new MethodCallRecorder(Priority.Last,
            AccessTools.Method(typeof(Quest), nameof(Quest.CheckPartyAndMakeItAttackTheCaravan)));
        Server.Call(() =>
        {
            var first = QuestOn(Server);
            var second = new Quest("second_escort", first.QuestGiver, first.QuestDueTime, 0.5f, 100);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var bandit));
            first._otherBanditParty = bandit;
            second.CheckPartyAndMakeItAttackTheCaravan(bandit);
            Assert.Equal(0, recruitment.CountFor(Server));
            first._otherBanditParty = null;
            second.CheckPartyAndMakeItAttackTheCaravan(bandit);
            Assert.Equal(1, recruitment.CountFor(Server));
            first._questBanditMobileParty = bandit;
            second.CheckPartyAndMakeItAttackTheCaravan(bandit);
            Assert.Equal(1, recruitment.CountFor(Server));
            first.CompleteQuestWithCancel();
            second.CheckPartyAndMakeItAttackTheCaravan(bandit);
            Assert.Equal(2, recruitment.CountFor(Server));
            second.CompleteQuestWithCancel();
        });
    }

    [Fact]
    public void AmbushAndRouteNoticesReachOnlyTheQuestOwner()
    {
        Accept();
        using var notices = new MethodCallRecorder(AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        Server.Call(() => Server.Resolve<IEscortProgress>().Notify(QuestOn(Server), new TextObject("The caravan is ambushed"), 100));
        Assert.Equal(1, notices.CountFor(Owner));
        Assert.Equal(0, notices.CountFor(Other));
        Assert.Equal(0, notices.CountFor(Server));
    }

    [Fact]
    public void DeferredRegistrationProgressAndFinalizationApplyInGameThreadOrder()
    {
        Accept();
        var quest = QuestOn(Owner);
        var state = Server.NetworkSentMessages.GetMessages<NetworkEscortProgress>().Last();
        state.CaravanId = caravanId;
        state.Days = 9;
        MobileParty caravan = null!;
        Owner.Call(() =>
        {
            caravan = Owner.GetRegisteredObject<MobileParty>(caravanId);
            Assert.True(Owner.ObjectManager.Remove(caravan));
        });
        void Register(MessagePayload<DeferredPartyRegistration> _)
            => GameThread.RunSafe(() => Assert.True(Owner.ObjectManager.AddExisting(caravanId, caravan)));
        var messages = Owner.Resolve<IMessageBroker>();
        messages.Subscribe<DeferredPartyRegistration>(Register);
        try
        {
            Owner.SimulateMessage(this, new DeferredPartyRegistration(), markGameThread: false);
            Owner.SimulateMessage(Server.NetPeer, state, markGameThread: false);
            Owner.SimulateMessage(Server.NetPeer,
                new NetworkIssueFinalized(scenario.NotableId, QuestScenario.IssueId, IssueOutcome.QuestCancel), markGameThread: false);
            Assert.False(quest.IsFinalized);
            Assert.True(Owner.PendingGameThreadActionCount >= 3);
            Owner.PumpGameThread();
            Owner.Call(() =>
            {
                Assert.Same(caravan, quest._questCaravanMobileParty);
                Assert.Equal(9, quest._daysSpentForEscorting);
                Assert.True(quest.IsFinalized);
            });
        }
        finally
        {
            messages.Unsubscribe<DeferredPartyRegistration>(Register);
        }
    }

    [Fact]
    public void AProgressMessageForAnotherIssueCannotReplaceTheOwnersCaravan()
    {
        Accept();
        Owner.SimulateMessage(Server.NetPeer, new NetworkEscortProgress
        {
            GiverId = scenario.NotableId, IssueId = "old_issue", ControllerId = QuestScenario.ControllerA,
            CaravanId = caravanId,
        });
        Owner.Call(() => Assert.Null(QuestOn(Owner)._questCaravanMobileParty));
    }

    [Theory]
    [InlineData(true, 10)]
    [InlineData(false, -20)]
    public void AlternativeConsequencesAndTheFinalJournalBelongToTheOwner(bool success, int prosperityChange)
    {
        Issue ownerIssue = null!;
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            ownerIssue = Assert.IsType<Issue>(giver.Issue);
            ownerIssue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
        });
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
            var issue = Assert.IsType<Issue>(giver.Issue);
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            var before = giver.CurrentSettlement.Town.Prosperity;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                if (success) issue.AlternativeSolutionEndWithSuccessConsequence();
                else issue.AlternativeSolutionEndWithFailureConsequence();
                issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("Companion returned")));
                issue.IssueFinalized();
            }
            Assert.Equal(before + prosperityChange, giver.CurrentSettlement.Town.Prosperity);
        });
        Owner.Call(() => Assert.Contains(ownerIssue.JournalEntries, log => log.LogText.ToString() == "Companion returned"));
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.Empty(giver.Issue.JournalEntries);
        });
    }

    [Fact]
    public void AReplacementMerchantPreservesTheCompanionIssueAndItsPlayerOwnership()
    {
        var replacementId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                giver.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId,
                    QuestScenario.IssueId, QuestScenario.ControllerA));
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(replacementId, out var replacement));
            Campaign.Current.IssueManager.ChangeIssueOwner(giver.Issue, replacement);
        });
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var previous));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(replacementId, out var replacement));
                Assert.Null(previous.Issue);
                Assert.IsType<Issue>(replacement.Issue);
                Assert.Equal(QuestScenario.IssueId, replacement.Issue.StringId);
                Assert.True(replacement.Issue.IsSolvingWithAlternative);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(replacementId, QuestScenario.IssueId, out var owner));
                Assert.Equal(QuestScenario.ControllerA, owner);
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
            });
        }
    }

    [Fact]
    public void ReplacingADeadMerchantCancelsTheExistingQuestBeforeItCanBeOrphaned()
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        var replacementId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(replacementId, out var replacement));
            using (new AllowedThread()) giver.ChangeState(Hero.CharacterStates.Dead);
            Campaign.Current.IssueManager.ChangeIssueOwner(giver.Issue, replacement);
            Assert.Null(replacement.Issue);
        });
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Fact]
    public void TheStartingJournalUsesTheMerchantTownWithoutAServerCurrentSettlement()
    {
        Accept();
        Server.Call(() =>
        {
            var previous = Campaign.Current.MainParty;
            Campaign.Current.MainParty = null;
            try
            {
                var quest = QuestOn(Server);
                Assert.Contains(quest.QuestGiver.CurrentSettlement.Name.ToString(), quest.PlayerStartsQuestLogText.ToString());
            }
            finally
            {
                Campaign.Current.MainParty = previous;
            }
        });
    }

    [Fact]
    public void CaptivityKeepsHourlyWorkOnTheRegisteredPlayerParty()
    {
        Accept();
        var harmony = new Harmony($"escort-captivity-{Guid.NewGuid()}");
        var method = AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(EscortMerchantCaravanTests), nameof(ObserveHourlyParty)) { priority = Priority.Last });
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
                using (new AllowedThread()) hero._partyBelongedTo = null;
                QuestOn(Server)._questCaravanMobileParty = caravan;
                observedHourlyParty = null;
                Campaign.Current.QuestManager.HourlyTick();
                Assert.Same(party, observedHourlyParty);
            });
        }
        finally
        {
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    private static bool ObserveHourlyParty()
    {
        observedHourlyParty = MobileParty.MainParty;
        return false;
    }

    [Fact]
    public void AQueuedAttackAfterCaptivityUsesTheRegisteredOwnerParty()
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        var harmony = new Harmony($"escort-captive-attack-{Guid.NewGuid()}");
        var method = AccessTools.Method(typeof(BeHostileAction), nameof(BeHostileAction.ApplyEncounterHostileAction));
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(EscortMerchantCaravanTests), nameof(ObserveHostility)) { priority = Priority.Last });
        try
        {
            PartyBase expected = null!;
            Server.Call(() =>
            {
                Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
                using (new AllowedThread())
                {
                    hero._partyBelongedTo = null;
                    caravan.IsActive = true;
                }
                Campaign.Current.MainParty = null;
                QuestOn(Server)._questCaravanMobileParty = caravan;
                expected = party.Party;
                observedAttacker = null;
            });
            Server.SimulateMessage(Owner.NetPeer, new RequestQuestBranch(scenario.NotableId,
                QuestScenario.IssueId, (byte)EscortBranch.AttackCaravan), markGameThread: false);
            Server.PumpGameThread();
            Assert.Same(expected, observedAttacker);
            Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        }
        finally
        {
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    private static bool ObserveHostility(PartyBase __0)
    {
        observedAttacker = __0;
        return false;
    }

    [Fact]
    public void DisconnectDismissalFullyFinalizesTheUnstartedServerQuest()
    {
        Accept();
        var quest = QuestOn(Server);
        Server.Call(() =>
        {
            quest.AddTrackedObject(quest.QuestGiver);
            Server.Resolve<IPlayerManager>().ClearPeer(Owner.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Owner.NetPeer, default(DisconnectInfo)));
            Assert.True(quest.IsFinalized);
            Assert.Null(quest.OfferDialogFlow);
            Assert.Null(quest.DiscussDialogFlow);
            Assert.Empty(Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest));
        });
    }

    [Fact]
    public void SelectingAnHeirCancelsTheOriginalPlayersAgreement()
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        var heirId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer(QuestScenario.ControllerA, out var original));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heirId, out var heir));
            Assert.True(players.ReplacePlayer(original, new Player(original.ControllerId, heirId,
                original.MobilePartyId, original.ClanId, original.CharacterObjectId)));
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerHeirSelectionCompleted(heir));
        });
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Fact]
    public void AnAcceptFailureRestoresTheIssueInsteadOfLeavingAnUnownedQuest()
    {
        var harmony = new Harmony($"escort-accept-failure-{Guid.NewGuid()}");
        var method = AccessTools.Method(typeof(EscortProgress), nameof(EscortProgress.Publish));
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(EscortMerchantCaravanTests), nameof(FailProgress)));
        CampaignTime due = default;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            due = giver.Issue.IssueDueTime;
        });
        try
        {
            Owner.Call(() =>
            {
                Assert.True(Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                giver.Issue.StartIssueWithQuest();
            });
        }
        finally
        {
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            Assert.Null(giver.Issue.IssueQuest);
            Assert.Equal(due, giver.Issue.IssueDueTime);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            Assert.Null(giver.Issue.IssueQuest);
            Assert.Equal(due, giver.Issue.IssueDueTime);
        });
    }

    private static void FailProgress()
    {
        if (ModInformation.IsServer) throw new InvalidOperationException("test progress failure");
    }

    [Fact]
    public void ClientTicksCannotAdvanceOrFinishTheAuthoritativeEscort()
    {
        Accept();
        Owner.Call(() =>
        {
            var quest = QuestOn(Owner);
            var days = quest._daysSpentForEscorting;
            quest.DailyTick();
            quest.HourlyTick();
            quest.SuccessConsequences(false);
            quest.FailConsequences();
            quest.FailByPlayerHostileConsequences();
            Assert.Equal(days, quest._daysSpentForEscorting);
            Assert.True(quest.IsOngoing);
        });
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessPaysOnlyTheOwnerForTheAuthoritativeDaysAndUpdatesTheTown(bool noTarget)
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Assert.True(players.TryGetPlayer(QuestScenario.ControllerB, out var other));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(other.HeroId, out var otherHero));
            var beforeGold = ownerHero.Gold;
            var otherGold = otherHero.Gold;
            var prosperity = quest.QuestGiver.CurrentSettlement.Town.Prosperity;
            quest._daysSpentForEscorting = 4;
            Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(q => q.SuccessConsequences(noTarget));
            Assert.Equal(beforeGold + Math.Min(8000, quest.RewardGold * 4), ownerHero.Gold);
            Assert.Equal(otherGold, otherHero.Gold);
            Assert.Equal(prosperity + 10, quest.QuestGiver.CurrentSettlement.Town.Prosperity);
            Assert.True(quest.IsFinalized);
        });
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Theory]
    [InlineData(false, false, -10)]
    [InlineData(true, false, -10)]
    [InlineData(false, true, -20)]
    public void FailureRunsItsOwnTownConsequenceOnce(bool banditsWon, bool hostile, int prosperityChange)
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            var town = quest.QuestGiver.CurrentSettlement.Town;
            var prosperity = town.Prosperity;
            Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(q =>
            {
                if (hostile) q.FailByPlayerHostileConsequences();
                else q.FailConsequences(banditsWon);
            });
            Assert.Equal(prosperity + prosperityChange, town.Prosperity);
            Assert.True(quest.IsFinalized);
        });
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Fact]
    public void TheServerTimesOutAnOverdueEscortOnceAsItsOwner()
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            var town = quest.QuestGiver.CurrentSettlement.Town;
            var prosperity = town.Prosperity;
            quest._questCaravanMobileParty = Server.GetRegisteredObject<MobileParty>(caravanId);
            quest.QuestDueTime = CampaignTime.Now - CampaignTime.Hours(1);
            Campaign.Current.QuestManager.HourlyTick();
            Campaign.Current.QuestManager.HourlyTick();
            Assert.True(quest.IsFinalized);
            Assert.Equal(prosperity - 20, town.Prosperity);
        });
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Owner.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Contains(ownerQuest.JournalEntries, log => log.LogText.GetID() == "pUrSIed8");
        });
    }

    [Fact]
    public void AWarWithTheMerchantsTownCancelsTheEscortOnTheServerAsItsOwner()
    {
        var kingdomId = environment.CreateRegisteredObject<Kingdom>();
        var playerClanId = environment.CreateRegisteredObject<Clan>();
        var owner = Server.Resolve<IPlayerManager>().Players.Single(p => p.ControllerId == QuestScenario.ControllerA);
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));
                using (new AllowedThread())
                {
                    giver.Clan._kingdom = instance.GetRegisteredObject<Kingdom>(kingdomId);
                    giver.CurrentSettlement.Town._ownerClan = giver.Clan;
                    giver.CurrentSettlement.SettlementComponent = giver.CurrentSettlement.Town;
                    ownerHero.Clan = instance.GetRegisteredObject<Clan>(playerClanId);
                }
            });
        }
        Accept();
        var ownerQuest = QuestOn(Owner);
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            quest._questCaravanMobileParty = Server.GetRegisteredObject<MobileParty>(caravanId);
            DeclareWarAction.ApplyInternal(Server.GetRegisteredObject<Kingdom>(kingdomId),
                Server.GetRegisteredObject<Clan>(playerClanId), DeclareWarAction.DeclareWarDetail.CausedByRebellion);
            Assert.True(quest.IsFinalized);
        });
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Owner.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Contains(ownerQuest.JournalEntries, log => log.LogText.GetID() == "KhNkBd9O");
        });
    }

    [Fact]
    public void ARelayWarRunsTheEscortHandlerOnce()
    {
        Accept();
        var harmony = new Harmony($"escort-relay-war-{Guid.NewGuid()}");
        var reroute = AccessTools.Method(typeof(Quest), nameof(Quest.TryToFindAndSetTargetToNextSettlement));
        harmony.Patch(reroute, postfix: new HarmonyMethod(typeof(EscortMerchantCaravanTests), nameof(ObserveReroute)));
        try
        {
            Server.NetworkSentMessages.Clear();
            observedReroutes = 0;
            Server.Call(() =>
            {
                var quest = QuestOn(Server);
                quest._questCaravanMobileParty = Server.GetRegisteredObject<MobileParty>(caravanId);
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(Server.Resolve<IPlayerManager>().Players
                    .Single(player => player.ControllerId == QuestScenario.ControllerA).HeroId, out var owner));
                CampaignEventDispatcher.Instance.OnWarDeclared(owner.MapFaction, owner.MapFaction,
                    DeclareWarAction.DeclareWarDetail.CausedByRebellion);
                Assert.True(quest.IsOngoing);
            });

            Assert.Equal(1, observedReroutes);
            var progress = Server.NetworkSentMessages.GetMessages<NetworkEscortProgress>().ToArray();
            Assert.Single(progress, state => state.Notice != null);
            Assert.Single(progress, state => state.Notice == null);
        }
        finally
        {
            harmony.Unpatch(reroute, HarmonyPatchType.Postfix, harmony.Id);
        }
    }

    [Fact]
    public void ARelayKingdomChangeRunsTheEscortHandlerOnce()
    {
        Accept();
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(giver.Clan, null, null,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>(),
            message => message.Outcome == IssueOutcome.QuestCancel);
    }

    [Theory]
    [InlineData(IssueOutcome.QuestCancel)]
    [InlineData(IssueOutcome.QuestFail)]
    [InlineData(IssueOutcome.QuestSuccess)]
    [InlineData(IssueOutcome.QuestTimeOut)]
    public void FinalizationReplaysWithoutRunningWorldCleanupOnTheOwner(IssueOutcome outcome)
    {
        Accept();
        var quest = QuestOn(Owner);
        using var cleanup = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnFinalize)));
        Server.Call(() =>
        {
            var serverQuest = QuestOn(Server);
            serverQuest.AddLog(new TextObject("Final escort result"));
            if (outcome == IssueOutcome.QuestCancel) serverQuest.CompleteQuestWithCancel();
            else if (outcome == IssueOutcome.QuestFail) serverQuest.CompleteQuestWithFail();
            else if (outcome == IssueOutcome.QuestSuccess) serverQuest.CompleteQuestWithSuccess();
            else serverQuest.CompleteQuestWithTimeOut();
            if (outcome == IssueOutcome.QuestTimeOut)
                Assert.Equal(980, serverQuest.QuestGiver.CurrentSettlement.Town.Prosperity);
        });
        Assert.Equal(1, cleanup.CountFor(Server));
        Assert.Equal(0, cleanup.CountFor(Owner));
        Owner.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Contains(quest.JournalEntries, log => log.LogText.ToString() == "Final escort result");
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
