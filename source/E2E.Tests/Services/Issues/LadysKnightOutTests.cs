using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using Common.Network.Messages;
using HarmonyLib;
using Helpers;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Quests.LadysKnightOut;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Villages.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Localization;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LadysKnightOutTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string secondGiverId;
    private readonly List<(EnvironmentInstance Instance, HeroCreationModel Model)> models = new();
    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(environment.Server);

    public LadysKnightOutTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        secondGiverId = environment.CreateRegisteredObject<Hero>();
        var playerIds = new List<(string Hero, string Party)>();
        scenario.Server.Call(() =>
        {
            foreach (var controller in new[] { QuestScenario.ControllerA, QuestScenario.ControllerB })
            {
                Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(controller, out var player));
                playerIds.Add((player.HeroId, player.MobilePartyId));
            }
        });
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                models.Add((instance, Campaign.Current.Models.HeroCreationModel));
                var creation = new Mock<HeroCreationModel>();
                creation.Setup(model => model.IsHeroCombatant(It.IsAny<Hero>())).Returns(false);
                creation.Setup(model => model.GenerateFirstAndFullName(It.IsAny<Hero>()))
                    .Returns((new TextObject("Test"), new TextObject("Test Lady")));
                Campaign.Current.Models.HeroCreationModel = creation.Object;
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                var giver = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                var town = instance.GetRegisteredObject<Town>(townId);
                using (new AllowedThread())
                {
                    foreach (var ids in playerIds)
                        instance.GetRegisteredObject<Hero>(ids.Hero).PartyBelongedTo = instance.GetRegisteredObject<MobileParty>(ids.Party);
                    settlement.SetSettlementComponent(town);
                    town.OwnerClan = giver.Clan;
                    town.Security = 40;
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.Lord;
                    giver.IsFemale = true;
                    var issue = new Issue(giver) { StringId = QuestScenario.IssueId };
                    Campaign.Current.IssueManager._issues[giver] = issue;
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
    }

    public void Dispose()
    {
        foreach (var (instance, model) in models)
            instance.Call(() => Campaign.Current.Models.HeroCreationModel = model);
        environment.Dispose();
    }

    private Quest Accept(EnvironmentInstance client, float difficulty = 0)
    {
        client.Call(() =>
        {
            using (new IssueDifficultyOverride(difficulty)) scenario.Accept(client);
            ((Quest)scenario.QuestOf(client)).QuestAcceptedConsequences();
        });
        return (Quest)scenario.QuestOf(client);
    }

    [Fact]
    public void RealAcceptanceCreatesOnlyTheOwnersJournalAndAnUnstartedServerQuest()
    {
        var quest = Accept(scenario.ClientA);
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        scenario.ClientA.Call(() => Assert.Single(quest.JournalEntries));
        scenario.ClientB.Call(() => Assert.Null(scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
        scenario.Server.Call(() =>
        {
            var serverQuest = (Quest)scenario.QuestOf(scenario.Server);
            Assert.Empty(serverQuest.JournalEntries);
            Assert.DoesNotContain(serverQuest, Campaign.Current.QuestManager.Quests);
        });
    }

    [Fact]
    public void ServerCreatesTheRealOfferWithTheSameIdentityOnBothClients()
    {
        var giverId = secondGiverId;
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = instance.GetRegisteredObject<Hero>(giverId);
                    giver.StayingInSettlement = instance.GetRegisteredObject<Hero>(scenario.NotableId).CurrentSettlement;
                    giver.Occupation = Occupation.Lord;
                    giver.IsFemale = true;
                }
            });
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(giverId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero hero) => new Issue(hero), typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        var created = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.False(string.IsNullOrEmpty(created.IssueId));
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var issue = Assert.IsType<Issue>(instance.GetRegisteredObject<Hero>(giverId).Issue);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.True(issue.IsOngoingWithoutQuest);
                Assert.False(issue.IsThereAlternativeSolution);
                Assert.True(issue.IsThereLordSolution);
            });
    }

    [Theory]
    [InlineData(0f, 0, false, IssueOutcome.QuestFail)]
    [InlineData(0f, 1, false, IssueOutcome.QuestSuccess)]
    [InlineData(0.3f, 1, false, IssueOutcome.QuestFail)]
    [InlineData(0.3f, 2, false, IssueOutcome.QuestSuccess)]
    [InlineData(0.6f, 2, false, IssueOutcome.QuestFail)]
    [InlineData(0.6f, 3, false, IssueOutcome.QuestSuccess)]
    [InlineData(0.8f, 3, false, IssueOutcome.QuestFail)]
    [InlineData(0.8f, 3, true, IssueOutcome.QuestSuccess)]
    public void TournamentResultRunsTheRealBranchAsItsOwner(float difficulty, int round, bool champion, IssueOutcome expected)
    {
        var quest = Accept(scenario.ClientA, difficulty);
        int initialGold = 0;
        float initialRenown = 0;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(
                scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue, out var owner));
            initialGold = owner.Gold;
            initialRenown = owner.Clan.Renown;
            scenario.Server.Resolve<IMessageBroker>().Publish(this, new TournamentSessionUpdated(Snapshot(round, false, champion)));
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        scenario.Server.Call(() => scenario.Server.Resolve<IMessageBroker>().Publish(this,
            new TournamentSessionUpdated(Snapshot(round, true, champion))));
        Assert.Equal(expected, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        environment.FlushCoalescer();
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Equal(2, quest.JournalEntries.Count);
        });
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            var hero = scenario.Server.GetRegisteredObject<Hero>(player.HeroId);
            Assert.Equal(initialGold + (expected == IssueOutcome.QuestSuccess ? 750 : 0), hero.Gold);
            Assert.Equal(initialRenown + (expected == IssueOutcome.QuestSuccess ? 1 : 0), hero.Clan.Renown);
        });
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    [Fact]
    public void AnotherCompetitorsResultDoesNotFinishTheQuest()
    {
        Accept(scenario.ClientB);
        scenario.Server.Call(() =>
        {
            var broker = scenario.Server.Resolve<IMessageBroker>();
            broker.Publish(this, new TournamentSessionUpdated(Snapshot(3, false, true)));
            broker.Publish(this, new TournamentSessionUpdated(Snapshot(3, true, true)));
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void AClientCannotClaimATournamentWinWithoutAServerResult()
    {
        var quest = Accept(scenario.ClientA);
        scenario.ClientA.Call(() => quest.PlayerReachedTournamentRoundGoal());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        scenario.ClientA.Call(() => Assert.True(quest.IsOngoing));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrameworkAllowsSeparatePlayersButRefusesASecondQuestForTheSamePlayer(bool samePlayer)
    {
        Accept(scenario.ClientA);
        AddSecondOffer();
        var client = samePlayer ? scenario.ClientA : scenario.ClientB;
        client.Call(() =>
        {
            var issue = client.GetRegisteredObject<Hero>(secondGiverId).Issue;
            issue.StartIssueWithQuest();
            if (!samePlayer) ((Quest)issue.IssueQuest).QuestAcceptedConsequences();
        });
        Assert.Equal(samePlayer ? 1 : 2, scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
        if (samePlayer) Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        else
        {
            scenario.ClientA.Call(() => Assert.Null(scenario.ClientA.GetRegisteredObject<Hero>(secondGiverId).Issue.IssueQuest));
            scenario.ClientB.Call(() => Assert.Null(scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
        }
    }

    private void AddSecondOffer()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var first = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                    var giver = instance.GetRegisteredObject<Hero>(secondGiverId);
                    giver.StayingInSettlement = first.CurrentSettlement;
                    giver.Occupation = Occupation.Lord;
                    giver.IsFemale = true;
                    var issue = new Issue(giver) { StringId = "second_lady" };
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
    }

    [Fact]
    public void ServerRefusesAStaleOfferWhenSecurityHasMadeItIneligible()
    {
        long dueTime = 0;
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            using (new AllowedThread()) giver.CurrentSettlement.Town.Security = 81;
            giver.Issue.IssueDueTime = CampaignTime.DaysFromNow(9);
            dueTime = giver.Issue.IssueDueTime.NumTicks;
        });
        scenario.Accept(scenario.ClientA);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        scenario.ClientA.Call(() =>
        {
            var issue = scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            Assert.True(issue.IsOngoingWithoutQuest);
            Assert.False(issue.IsTriedToSolveBefore);
            Assert.Null(issue.IssueQuest);
            Assert.Equal(dueTime, issue.IssueDueTime.NumTicks);
        });
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
            using (new AllowedThread()) giver.CurrentSettlement.Town.Security = 40;
        });
        scenario.ClientA.Call(() =>
        {
            var giver = scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId);
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(giver));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            quest.QuestAcceptedConsequences();
            Assert.True(quest.IsOngoing);
            Assert.Single(quest.JournalEntries);
        });
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }

    [Fact]
    public void LadyEligibilityUsesTheGiverSettlementFactionForWar()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var giver = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                var hero = Hero.MainHero;
                var playerClan = GameObjectCreator.CreateInitializedObject<Clan>();
                var otherClan = GameObjectCreator.CreateInitializedObject<Clan>();
                using (new AllowedThread())
                {
                    hero.Clan = playerClan;
                    giver.CurrentSettlement.Town.OwnerClan = otherClan;
                }
                Assert.NotNull(giver.CurrentSettlement?.MapFaction);
                Assert.NotSame(hero.MapFaction, giver.CurrentSettlement.MapFaction);
                Assert.NotSame(giver.MapFaction, giver.CurrentSettlement.MapFaction);
            });
        }
    }

    [Fact]
    public void CrimePenaltyChangesOnlyTheOwnersHonorStore()
    {
        foreach (var client in environment.Clients)
            client.Call(() =>
            {
                Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, 83);
            });
        Accept(scenario.ClientA);
        scenario.ClientA.Call(() => Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, 183));
        AddSecondOffer();
        Quest otherQuest = null!;
        scenario.ClientB.Call(() =>
        {
            var issue = scenario.ClientB.GetRegisteredObject<Hero>(secondGiverId).Issue;
            issue.StartIssueWithQuest();
            otherQuest = (Quest)issue.IssueQuest;
            otherQuest.QuestAcceptedConsequences();
        });
        int expectedXp = 0;
        int expectedLevel = 0;
        scenario.Server.Call(() => scenario.Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(quest =>
        {
            if (quest.QuestGiver != scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId)) return;
            Campaign.Current.Models.CharacterDevelopmentModel.GetTraitLevelForTraitXp(Hero.MainHero, DefaultTraits.Honor,
                183 - 50, out expectedLevel, out expectedXp);
            quest.CriminalActionPerformedTowardsSettlement();
        }));
        var penalty = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutHonor>());
        Assert.Equal(QuestScenario.ControllerA, penalty.ControllerId);
        Assert.Equal(expectedXp, penalty.Xp);
        Assert.Equal(expectedLevel, penalty.Level);
        scenario.ClientA.Call(() =>
        {
            Assert.Equal(expectedXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(expectedLevel, scenario.ClientA.GetRegisteredObject<Hero>(penalty.HeroId).GetTraitLevel(DefaultTraits.Honor));
        });
        scenario.ClientB.Call(() =>
        {
            Assert.Equal(83, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.True(otherQuest.IsOngoing);
            Assert.Single(otherQuest.JournalEntries);
        });
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Fact]
    public void TimeoutRunsTheOwnersPenaltyAndJournalOnTheServer()
    {
        var quest = Accept(scenario.ClientA);
        scenario.ClientA.Call(() => quest.CompleteQuestWithTimeOut());
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Equal(2, quest.JournalEntries.Count);
            Assert.Contains("failed to enter", quest.JournalEntries.Last().LogText.ToString());
        });
    }

    [Fact]
    public void GiverDeathCancelsFromTheServerEventWithoutRewardingAnotherPlayer()
    {
        var quest = Accept(scenario.ClientA);
        scenario.Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(
            scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId), null,
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() => Assert.Equal(2, quest.JournalEntries.Count));
    }

    [Fact]
    public void GiverDepartureCancelsFromTheServerEvent()
    {
        var quest = Accept(scenario.ClientA);
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            var settlement = giver.CurrentSettlement;
            Assert.True(scenario.Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(giver.Issue, out var owner));
            using (new AllowedThread()) giver.StayingInSettlement = null;
            CampaignEventDispatcher.Instance.OnSettlementLeft(owner.PartyBelongedTo, settlement);
        });
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() => Assert.Equal(2, quest.JournalEntries.Count));
        scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Theory]
    [InlineData(true, false, IssueOutcome.QuestFail)]
    [InlineData(false, false, IssueOutcome.QuestCancel)]
    [InlineData(false, true, IssueOutcome.QuestCancel)]
    public void WarAndKingdomEventsUseTheOwnerAndTheActualInstigator(bool ownerInitiated, bool kingdomChange, IssueOutcome expected)
    {
        var quest = Accept(scenario.ClientA);
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            Assert.True(scenario.Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(giver.Issue, out var owner));
            using (new AllowedThread())
            {
                owner.Clan.Id = new MBGUID(1);
                giver.Clan.Id = new MBGUID(2);
            }
            VillageHostileFactionStanceHelper.ApplyWarStance(owner.MapFaction, giver.MapFaction);
            var previous = ResolvedMainHeroContext.ResolvedMainHero;
            try
            {
                ResolvedMainHeroContext.ResolvedMainHero = ownerInitiated ? owner : scenario.Server.GetRegisteredObject<Hero>(secondGiverId);
                if (kingdomChange)
                    CampaignEventDispatcher.Instance.OnClanChangedKingdom(owner.Clan, null, null,
                        ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
                else
                    CampaignEventDispatcher.Instance.OnWarDeclared(owner.MapFaction, giver.MapFaction,
                        DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
            }
            finally { ResolvedMainHeroContext.ResolvedMainHero = previous; }
        });
        Assert.Equal(expected, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() => Assert.Equal(2, quest.JournalEntries.Count));
        scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Raid)]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies)]
    [InlineData(MapEvent.BattleTypes.IsForcingVolunteers)]
    public void TheOwnersVillageCrimeIsForwardedFromTheMapEvent(MapEvent.BattleTypes type)
    {
        var quest = Accept(scenario.ClientA);
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            Assert.True(scenario.Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(giver.Issue, out var owner));
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            var village = new Village();
            var mapEvent = new MapEvent();
            using (new AllowedThread())
            {
                settlement.SetSettlementComponent(village);
                village.Bound = giver.CurrentSettlement;
                mapEvent.MapEventSettlement = settlement;
                mapEvent._mapEventType = type;
            }
            CampaignEventDispatcher.Instance.OnMapEventStarted(mapEvent, owner.PartyBelongedTo.Party, null);
        });
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutHonor>());
        scenario.ClientA.Call(() => Assert.Equal(2, quest.JournalEntries.Count));
        scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void SharedCoercionHelperStillSyncsTheGiversPower()
    {
        var quest = Accept(scenario.ClientA);
        float before = 0;
        float after = 0;
        scenario.Server.Call(() => scenario.Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(serverQuest =>
        {
            var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
            using (new AllowedThread()) mapEvent.MapEventSettlement = serverQuest.QuestGiver.CurrentSettlement;
            before = serverQuest.QuestGiver.Power;
            QuestHelper.ApplyGenericMinorMajorCoercionConsequences(serverQuest, mapEvent);
            after = serverQuest.QuestGiver.Power;
        }));
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Equal(before - 10, after);
        environment.FlushCoalescer();
        foreach (var client in environment.Clients)
            client.Call(() => Assert.Equal(after, client.GetRegisteredObject<Hero>(scenario.NotableId).Power));
        scenario.ClientA.Call(() => Assert.Equal(2, quest.JournalEntries.Count));
    }

    [Fact]
    public void ADelayedLordAcceptanceChecksTheAlreadyOpenMenu()
    {
        using var callback = new MethodCallRecorder(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.BeforeGameMenuOpened)));
        scenario.ClientA.Call(() => Campaign.Current.ConversationManager.IsConversationInProgress = false);
        scenario.ClientA.SimulateMessage(this, new NetworkLadysKnightOutLordAccepted(
            scenario.NotableId,
            QuestScenario.IssueId,
            QuestScenario.ControllerA,
            1f,
            "accepted"));
        Assert.Equal(1, callback.CountFor(scenario.ClientA));
    }

    [Fact]
    public void ADelayedLordAcceptanceAutomaticallyCompletesInMapStateOnce()
    {
        var previousStates = GameStateManager.Current;
        try
        {
            PrepareLordSolution();
            var router = scenario.Server.Resolve<TestNetworkRouter>();
            router.PauseLink(scenario.Server.NetPeer, scenario.ClientA.NetPeer);
            IssueBase localIssue = null!;
            scenario.ClientA.Call(() =>
            {
                localIssue = scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
                using (new IssueDifficultyOverride(0.1f)) localIssue.StartIssueWithLordSolution();
                Campaign.Current.ConversationManager.IsConversationInProgress = false;
                var states = Game.Current.GameStateManager;
                states._gameStates.Add(states.CreateState<MapState>());
                GameStateManager._current = states;
                Assert.IsType<MapState>(GameStateManager.Current.ActiveState);
                Assert.Null(Campaign.Current.GameMenuManager.NextLocation);
            });
            router.ResumeLink(scenario.Server.NetPeer, scenario.ClientA.NetPeer);
            router.RunUntilIdle();
            scenario.ClientA.SimulateMessage(this, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutLordAccepted>()));
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            environment.FlushCoalescer();
            scenario.Server.Call(() =>
            {
                Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
                Assert.Equal(82, scenario.Server.GetRegisteredObject<Hero>(player.HeroId).Clan.Influence);
            });
            scenario.ClientA.Call(() =>
            {
                Assert.Equal(2, localIssue.JournalEntries.Count);
                Assert.Contains(localIssue.JournalEntries, log => log.LogText.ToString().Contains("in her honor"));
            });
            scenario.ClientB.Call(() => Assert.Empty(scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue?.JournalEntries ?? Enumerable.Empty<JournalLog>()));
        }
        finally { GameStateManager._current = previousStates; }
    }

    [Fact]
    public void AStaleDirectAcceptCannotOverwriteAKnownLordOwner()
    {
        PrepareLordSolution();
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution());
        scenario.ClientB.Call(() =>
        {
            var issue = scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(issue.IssueOwner));
            Assert.Null(issue.IssueQuest);
            Assert.True(issue.IsSolvingWithLordSolution);
            Assert.False(issue.IsTriedToSolveBefore);
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void ARepeatedDialogueAcceptKeepsTheOwnersQuestAttachedAndCompletesNormally()
    {
        var quest = Accept(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(quest.QuestGiver));
            Assert.Same(quest, quest.QuestGiver.Issue.IssueQuest);
            Assert.True(quest.IsOngoing);
            quest.QuestAcceptedConsequences();
            Assert.Single(quest.JournalEntries);
        });
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        scenario.Server.Call(() =>
        {
            var broker = scenario.Server.Resolve<IMessageBroker>();
            broker.Publish(this, new TournamentSessionUpdated(Snapshot(3, false, true)));
            broker.Publish(this, new TournamentSessionUpdated(Snapshot(3, true, true)));
        });
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() => Assert.True(quest.IsFinalized));
    }

    [Theory]
    [InlineData(0.1f, 18)]
    [InlineData(0.8f, 39)]
    public void LordInfluenceCostKeepsTheClientDifficultyAfterTheRequestIsDelayed(float difficulty, int cost)
    {
        PrepareLordSolution();
        var router = scenario.Server.Resolve<TestNetworkRouter>();
        router.PauseLink(scenario.ClientA.NetPeer, scenario.Server.NetPeer);
        scenario.ClientA.Call(() =>
        {
            using (new IssueDifficultyOverride(difficulty))
                scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution();
        });
        router.ResumeLink(scenario.ClientA.NetPeer, scenario.Server.NetPeer);
        router.RunUntilIdle();
        Assert.Equal(difficulty, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutLordAccepted>()).Difficulty);
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        environment.FlushCoalescer();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            Assert.Equal(100 - cost, scenario.Server.GetRegisteredObject<Hero>(player.HeroId).Clan.Influence);
        });
    }

    [Fact]
    public void TheWinningLordAcceptanceReplacesALosingOptimisticDirectAccept()
    {
        PrepareLordSolution();
        var router = scenario.Server.Resolve<TestNetworkRouter>();
        router.PauseLink(scenario.Server.NetPeer, scenario.ClientB.NetPeer);
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution());
        Quest losingQuest = null!;
        scenario.ClientB.Call(() =>
        {
            var issue = scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            issue.StartIssueWithQuest();
            losingQuest = (Quest)issue.IssueQuest;
            losingQuest.QuestAcceptedConsequences();
        });
        router.ResumeLink(scenario.Server.NetPeer, scenario.ClientB.NetPeer);
        router.RunUntilIdle();
        scenario.ClientB.Call(() =>
        {
            var issue = scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            Assert.Null(issue.IssueQuest);
            Assert.True(issue.IsSolvingWithLordSolution);
            Assert.False(issue.IsTriedToSolveBefore);
            Assert.True(scenario.ClientB.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out var owner));
            Assert.Equal(QuestScenario.ControllerA, owner);
            losingQuest.QuestAcceptedConsequences();
            Assert.True(losingQuest.IsFinalized);
        });
    }

    [Fact]
    public void AnUnfinishedLordSolutionIsDismissedWhenItsOwnerDisconnects()
    {
        PrepareLordSolution();
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution());
        scenario.Server.Call(() =>
        {
            scenario.Server.Resolve<IPlayerManager>().ClearPeer(scenario.ClientA.NetPeer);
            scenario.Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(scenario.ClientA.NetPeer, default));
        });
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    [Fact]
    public void GiverDeathCancelsAPendingLordSolutionOnEveryMachine()
    {
        PrepareLordSolution();
        IssueBase localIssue = null!;
        scenario.ClientA.Call(() =>
        {
            localIssue = scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            localIssue.StartIssueWithLordSolution();
        });
        scenario.ClientA.Call(() => Assert.True(localIssue.IsSolvingWithLordSolution));
        scenario.Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(
            scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId), null,
            KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge, false));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
        scenario.ClientA.Call(() => Assert.Equal(2, localIssue.JournalEntries.Count));
    }

    private void PrepareLordSolution()
    {
        string heroId = null!;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            heroId = player.HeroId;
        });
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var hero = instance.GetRegisteredObject<Hero>(heroId);
                using (new AllowedThread())
                {
                    hero.Clan.Influence = 100;
                    instance.GetRegisteredObject<Hero>(scenario.NotableId).CurrentSettlement.Town.OwnerClan = hero.Clan;
                }
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LordSolutionChargesOnlyTheAcceptedPlayersInfluenceAndCompletesOnce(bool enoughInfluence)
    {
        string heroId = null!;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            heroId = player.HeroId;
        });
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var hero = instance.GetRegisteredObject<Hero>(heroId);
                using (new AllowedThread())
                {
                    hero.Clan.Influence = enoughInfluence ? 100 : 0;
                    instance.GetRegisteredObject<Hero>(scenario.NotableId).CurrentSettlement.Town.OwnerClan = hero.Clan;
                }
            });
        }
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution());
        if (!enoughInfluence)
        {
            Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutLordAccepted>());
            return;
        }
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutLordAccepted>());
        float expectedInfluence = 0;
        scenario.Server.Call(() => expectedInfluence = 100 - scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue.NeededInfluenceForLordSolution);
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        environment.FlushCoalescer();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            Assert.Equal(expectedInfluence, scenario.Server.GetRegisteredObject<Hero>(player.HeroId).Clan.Influence);
        });
    }

    private static TournamentSessionSnapshot Snapshot(int round, bool finished, bool champion)
    {
        var contestant = new TournamentContestantData("slot", "hero", 1, QuestScenario.ControllerA, "Champion", true, false, false, null);
        var team = new TournamentTeamData("team", new[] { "slot" }, 0, champion, 0, null);
        var match = new TournamentMatchData("match", "round", finished ? (int)TournamentMatch.MatchState.Finished : (int)TournamentMatch.MatchState.Ready,
            1, 1, new[] { team }, champion ? new[] { "slot" } : new[] { "opponent" });
        return new TournamentSessionSnapshot("session", "mission", "town", "arena", "prize",
            finished ? TournamentSessionPhase.Completed : TournamentSessionPhase.LiveMatch,
            finished ? 2 : 1, finished ? 1 : 0, "match", QuestScenario.ControllerA, Array.Empty<string>(), new[] { contestant },
            Array.Empty<string>(), new[] { new TournamentPlayerChoiceData(QuestScenario.ControllerA, TournamentPlayerChoice.Join) },
            new[] { new TournamentRoundData("round", round, 0, new[] { match }) }, 1, 0, 1, false, finished && champion, champion ? "slot" : null);
    }
}
