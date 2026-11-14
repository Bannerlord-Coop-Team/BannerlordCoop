using Common.Network;
using Common.Util;
using Common.Messaging;
using Common.Network.Messages;
using LiteNetLib;
using GameInterface.Registry.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.OwnerChange;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.Issues.Quests.CaravanAmbush;
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.Players;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using SandBox.GauntletUI.Map;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace E2E.Tests.Services.Issues;

public class CaravanAmbushTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string caravanId;
    private readonly string banditId;
    private readonly GameStateManager previousGameStateManager = GameStateManager.Current;
    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;

    public CaravanAmbushTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment, giver => new Issue(giver, null));
        caravanId = environment.CreateRegisteredObject<MobileParty>();
        banditId = environment.CreateRegisteredObject<MobileParty>();
        scenario.AttachQuest(Server, CreateQuest);
        scenario.AttachQuest(Owner, CreateQuest);
        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA)));
        Server.Call(() =>
        {
            PlayerHero(QuestScenario.ControllerA);
            PlayerHero(QuestScenario.ControllerB);
        });
        Owner.Call(() => QuestOf(Owner).StartQuest());
    }

    public void Dispose()
    {
        Owner.Call(() => GameStateManager._current = previousGameStateManager);
        environment.Dispose();
    }

    [Fact]
    public void ProgressBindsTheOwnersQuestToServerPartiesAndJournal()
    {
        SendState(QuestScenario.ControllerA, true, 3, "Follow", "Saved");
        Owner.Call(() =>
        {
            Owner.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
            Owner.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            var quest = QuestOf(Owner);
            Assert.Same(caravan, quest._caravanParty);
            Assert.Same(bandits, quest._banditParty);
            Assert.True(quest._isCaravanSaved);
            Assert.Equal(3, quest._vicinityCheckFailCounter);
            Assert.Equal(new[] { "Follow", "Saved" }, quest.JournalEntries.Select(log => log.LogText.ToString()));
            Assert.True(quest.IsTracked(caravan));
        });
    }

    [Fact]
    public void ProgressWaitsForTheGiverChangeHeldByAConversation()
    {
        var quest = QuestOf(Owner);
        var newGiverId = ChangeGiverWhileOwnerIsTalking();

        Server.Call(() =>
        {
            var serverQuest = Server.GetRegisteredObject<Hero>(newGiverId).Issue.IssueQuest as Quest;
            Assert.NotNull(serverQuest);
            serverQuest._isCaravanSaved = true;
            Server.Resolve<ICaravanAmbushProgress>().Publish(serverQuest);
        });
        Owner.Call(() =>
        {
            Assert.False(quest._isCaravanSaved);
            Assert.Null(Owner.GetRegisteredObject<Hero>(newGiverId).Issue);
            Campaign.Current.ConversationManager.EndConversation();
            Assert.Same(quest, Owner.GetRegisteredObject<Hero>(newGiverId).Issue.IssueQuest);
            Assert.True(quest._isCaravanSaved);
        });
    }

    [Fact]
    public void PublishedJournalKeepsTextKeysVariablesTimeAndProgress()
    {
        Server.Call(() =>
        {
            var text = new TextObject("{=caravan_progress_test}Follow {CARAVAN}");
            text.SetTextVariable("CARAVAN", "Atilon's caravan");
            QuestOf(Server)._journalEntries.Add(new JournalLog(new CampaignTime(123456L), text,
                new TextObject("{=caravan_task_test}Caravans saved"), 2, 5, LogType.Discreate));
            Server.Resolve<ICaravanAmbushProgress>().Publish(QuestOf(Server));
        });
        Owner.Call(() =>
        {
            var log = Assert.Single(QuestOf(Owner).JournalEntries);
            Assert.Equal("caravan_progress_test", log.LogText.GetID());
            Assert.Equal("Follow Atilon's caravan", log.LogText.ToString());
            Assert.Equal(123456L, log.LogTime.NumTicks);
            Assert.Equal("caravan_task_test", log.TaskName.GetID());
            Assert.Equal(2, log.CurrentProgress);
            Assert.Equal(5, log.Range);
            Assert.Equal(LogType.Discreate, log.Type);
        });
    }

    [Fact]
    public void ReceivedJournalDoesNotSendARequestBackToTheServer()
    {
        Owner.Call(() => Assert.True(Owner.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA)));
        var before = Owner.NetworkSentMessages.GetMessageCount<RequestQuestJournal>();
        Server.Call(() =>
        {
            QuestOf(Server)._journalEntries.Add(new JournalLog(new CampaignTime(123456L), new TextObject("Follow the caravan")));
            Server.Resolve<ICaravanAmbushProgress>().Publish(QuestOf(Server));
        });
        Owner.Call(() => Assert.Single(QuestOf(Owner).JournalEntries));
        Assert.Equal(before, Owner.NetworkSentMessages.GetMessageCount<RequestQuestJournal>());
    }

    [Fact]
    public void ProgressPublishesOnlyChangedOwnerState()
    {
        scenario.AttachQuest(Other, CreateQuest);
        Server.Call(() =>
        {
            var events = Server.Resolve<ICaravanAmbushWorldEventService>();
            var baseline = Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>();
            events.Run(quest => quest._vicinityCheckFailCounter = 1);
            Assert.Equal(baseline + 1, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>());

            events.Run(quest => quest._vicinityCheckFailCounter = 1);
            Assert.Equal(baseline + 1, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>());

            events.Run(quest => quest._vicinityCheckFailCounter = 2);
            Assert.Equal(baseline + 2, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>());

            events.Run(quest => quest._journalEntries.Add(new JournalLog(CampaignTime.Now, new TextObject("Follow the caravan"))));
            Assert.Equal(baseline + 3, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>());
        });
        Owner.Call(() => Assert.Equal(2, QuestOf(Owner)._vicinityCheckFailCounter));
        Other.Call(() => Assert.Equal(0, QuestOf(Other)._vicinityCheckFailCounter));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnUnregisteredActivePartyDoesNotClearTheOwnersParty(bool caravanMissing)
    {
        Server.Call(() =>
        {
            BindParties();
            var quest = QuestOf(Server);
            var publisher = Server.Resolve<ICaravanAmbushProgress>();
            publisher.Publish(quest);
            var before = Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>();
            var party = caravanMissing ? quest._caravanParty : quest._banditParty;
            Assert.True(party.IsActive);
            Assert.True(Server.ObjectManager.Remove(party));
            quest._vicinityCheckFailCounter++;
            publisher.Publish(quest);
            Assert.Equal(before, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushProgress>());
        });
        Owner.Call(() =>
        {
            Assert.Same(Owner.GetRegisteredObject<MobileParty>(caravanId), QuestOf(Owner)._caravanParty);
            Assert.Same(Owner.GetRegisteredObject<MobileParty>(banditId), QuestOf(Owner)._banditParty);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullOrInactivePartyClearsTheOwnersReference(bool inactive)
    {
        Server.Call(() =>
        {
            BindParties();
            var quest = QuestOf(Server);
            var publisher = Server.Resolve<ICaravanAmbushProgress>();
            publisher.Publish(quest);
            if (inactive) quest._caravanParty.IsActive = false;
            else quest._caravanParty = null;
            publisher.Publish(quest);
        });
        Owner.Call(() =>
        {
            Assert.Null(QuestOf(Owner)._caravanParty);
            Assert.Same(Owner.GetRegisteredObject<MobileParty>(banditId), QuestOf(Owner)._banditParty);
        });
    }

    [Fact]
    public void EligibleSecondPlayerCanTakeTheSameTypeButTheOwnerCannot()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver);
            Issue second;
            using (new AllowedThread())
            {
                giver.OwnedCaravans.Add(GameObjectCreator.CreateInitializedObject<CaravanPartyComponent>());
                second = new Issue(giver, null);
                IssueOf(Server)._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            }
            var gate = Server.Resolve<IIssueAcceptPreconditions>();
            Assert.False(gate.CanTake(second, PlayerHero(QuestScenario.ControllerA), QuestScenario.ControllerA));
            Assert.True(gate.CanTake(second, PlayerHero(QuestScenario.ControllerB), QuestScenario.ControllerB));
        });
    }

    [Fact]
    public void RewardGoesToTheOwnerAndEndsTheirStartedQuest()
    {
        var goldBefore = 0;
        var ownerQuest = QuestOf(Owner);
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            goldBefore = PlayerHero(QuestScenario.ControllerA).Gold;
            BindParties();
            QuestOf(Server)._isCaravanSaved = true;
        });
        Owner.Call(() => ownerQuest.OnQuestSucceeded());
        Server.Call(() => Assert.Equal(goldBefore + 1000, PlayerHero(QuestScenario.ControllerA).Gold));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Fact]
    public void CaravaneerDialoguePaysTheOwnerWhenTheConversationEnds()
    {
        var goldBefore = 0;
        var ownerQuest = QuestOf(Owner);
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            BindParties();
            QuestOf(Server)._isCaravanSaved = true;
            goldBefore = PlayerHero(QuestScenario.ControllerA).Gold;
        });
        Owner.Call(() =>
        {
            var flow = ownerQuest.GetCaravaneerDialogFlow();
            var rewardLine = Assert.Single(flow.Lines.Where(line => line.ConsequenceDelegate != null));
            rewardLine.ConsequenceDelegate();
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            Campaign.Current.ConversationManager.EndConversation();
        });
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Server.Call(() => Assert.Equal(goldBefore + 1000, PlayerHero(QuestScenario.ControllerA).Gold));
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BattleOutcomeUsesTheQuestOwnersParticipation(bool ownerParticipated)
    {
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var quest = QuestOf(Server);
            var attacker = ownerParticipated ? PlayerHero(QuestScenario.ControllerA).PartyBelongedTo : GameObjectCreator.CreateInitializedObject<MobileParty>();
            var battle = StartAmbushBattle(attacker);
            battle.BattleState = BattleState.AttackerVictory;
            CampaignEventDispatcher.Instance.OnMapEventEnded(battle);
            Assert.True(quest._isCaravanSaved);
            if (ownerParticipated)
            {
                Assert.True(quest._caravanParty.IsCurrentlyUsedByAQuest);
                Assert.True(quest._caravanParty.Ai.DoNotMakeNewDecisions);
                Assert.False(quest._banditParty.IsCurrentlyUsedByAQuest);
            }
            else
            {
                Assert.True(quest.IsFinalized);
                AssertPartiesReleased();
            }
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
        if (ownerParticipated) Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        else Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Equal(ownerParticipated ? 1 : 0, Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushConversation>().Count());
    }

    [Fact]
    public void AnOwnerWhoRetreatedFromTheAmbushBattleDoesNotGetTheRewardConversation()
    {
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var quest = QuestOf(Server);
            var attacker = PlayerHero(QuestScenario.ControllerA).PartyBelongedTo;
            var battle = StartAmbushBattle(attacker);
            Server.Resolve<IRetreatedMapEventPartyTracker>().MarkRetreated(battle, attacker.Party);
            battle.BattleState = BattleState.AttackerVictory;
            CampaignEventDispatcher.Instance.OnMapEventEnded(battle);
            Assert.True(quest._isCaravanSaved);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Server.Call(AssertPartiesReleased);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushConversation>());
    }

    [Fact]
    public void CaravanDestroyedInBattleFailsTheOwnersQuest()
    {
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var quest = QuestOf(Server);
            var battle = StartAmbushBattle(PlayerHero(QuestScenario.ControllerA).PartyBelongedTo);
            quest._caravanParty.Party.MapEventSide = battle.DefenderSide;
            battle.BattleState = BattleState.AttackerVictory;
            CampaignEventDispatcher.Instance.OnMapEventEnded(battle);
            Assert.False(quest._isCaravanSaved);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
    }

    [Fact]
    public void OwnerWinningWithoutTheCaravanOnTheFieldSucceedsDirectly()
    {
        var goldBefore = 0;
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            goldBefore = PlayerHero(QuestScenario.ControllerA).Gold;
            var quest = QuestOf(Server);
            var battle = StartAmbushBattle(PlayerHero(QuestScenario.ControllerA).PartyBelongedTo, caravanOnWinningSide: false);
            battle.BattleState = BattleState.AttackerVictory;
            CampaignEventDispatcher.Instance.OnMapEventEnded(battle);
            Assert.True(quest._isCaravanSaved);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
        Server.Call(() => Assert.Equal(goldBefore + 1000, PlayerHero(QuestScenario.ControllerA).Gold));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushConversation>());
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaravanReachingASettlementCancelsOnlyAnUnsavedQuest(bool saved)
    {
        Server.Call(() =>
        {
            BindParties();
            var quest = QuestOf(Server);
            quest._isCaravanSaved = saved;
            Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
            CampaignEventDispatcher.Instance.OnSettlementEntered(caravan, GameObjectCreator.CreateInitializedObject<Settlement>(), null);
            if (saved)
            {
                Assert.True(quest.IsOngoing);
                Assert.Same(caravan, quest._caravanParty);
            }
            else
            {
                Assert.Null(quest._caravanParty);
            }
        });
        if (saved)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        }
        else
        {
            var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
        }
    }

    [Fact]
    public void CapturedOwnerAmbushBattleLosesInvulnerabilityAfterSixHours()
    {
        Server.Call(() =>
        {
            var battle = StartAmbushBattle(PlayerHero(QuestScenario.ControllerB).PartyBelongedTo);
            PlayerHero(QuestScenario.ControllerA)._partyBelongedTo = null;
            battle.IsInvulnerable = true;
            battle._mapEventStartTime = CampaignTime.Now - CampaignTime.Hours(7f);
            var otherHero = PlayerHero(QuestScenario.ControllerB);
            using (new GameInterface.Services.Heroes.Patches.MainHeroSubstitutionScope(otherHero, otherHero.PartyBelongedTo))
                CampaignEventDispatcher.Instance.HourlyTick();
            Assert.False(battle.IsInvulnerable);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
    }

    [Fact]
    public void AnOwnerWithoutAPartyDoesNotWinUsingTheCallersParty()
    {
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var otherHero = PlayerHero(QuestScenario.ControllerB);
            var battle = StartAmbushBattle(otherHero.PartyBelongedTo);
            PlayerHero(QuestScenario.ControllerA)._partyBelongedTo = null;
            battle.BattleState = BattleState.AttackerVictory;
            using (new GameInterface.Services.Heroes.Patches.MainHeroSubstitutionScope(otherHero, otherHero.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnMapEventEnded(battle);
            AssertPartiesReleased();
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushConversation>());
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredSnapshotCancellationKeepsTheOwnerEndingAndReplacementOwnership(bool replacement)
    {
        var quest = QuestOf(Owner);
        Owner.Call(() =>
        {
            Assert.True(Owner.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var character = ObjectHelper.SkipConstructor<CharacterObject>();
            character.HeroObject = Owner.GetRegisteredObject<Hero>(scenario.NotableId);
            Campaign.Current.ConversationManager.SetupAndStartMapConversation(null,
                new MapConversationAgent(character), new MapConversationAgent(CharacterObject.PlayerCharacter));
            Assert.True(Campaign.Current.ConversationManager.IsConversationInProgress);
            Owner.Resolve<IIssueConversationGuard>().RunWhenNotTalkingTo(() => { }, scenario.NotableId);
        }, new[] { AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.StartNew)) });

        using var cancels = new MethodCallRecorder(Priority.First, true,
            AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel)));
        var fakeEndingsBefore = Owner.InternalMessages.GetMessageCount<NetworkIssueFinalized>();
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Server.Call(() => Server.Resolve<INetwork>().Send(Owner.NetPeer,
            new NetworkIssueOwnershipSnapshot(replacement
                ? new[] { new IssueOwnershipData(scenario.NotableId, "replacement_issue", QuestScenario.ControllerB) }
                : Array.Empty<IssueOwnershipData>())));
        Owner.PumpGameThread();
        Owner.Call(() =>
        {
            Assert.True(quest.IsOngoing);
            Assert.False(Owner.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
            Assert.Equal(0, cancels.CountFor(Owner));
            Campaign.Current.ConversationManager.EndConversation();
            Assert.True(quest.IsFinalized);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            if (replacement)
            {
                Assert.True(Owner.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, "replacement_issue", out var controllerId));
                Assert.Equal(QuestScenario.ControllerB, controllerId);
            }
            else Assert.Empty(Owner.Resolve<IIssueOwnershipRegistry>().GetAll());
        });
        Assert.Equal(1, cancels.CountFor(Owner));
        Assert.Equal(fakeEndingsBefore, Owner.InternalMessages.GetMessageCount<NetworkIssueFinalized>());
    }

    [Fact]
    public void FinalizationReleasesBothParties()
    {
        Server.Call(BindParties);
        Owner.Call(() => QuestOf(Owner).CompleteQuestWithCancel());
        Server.Call(() => AssertPartiesReleased());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DismissalReleasesBothParties(bool serverStartup)
    {
        Server.Call(() =>
        {
            BindParties();
            using (new AllowedThread()) IssueOf(Server)._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            var quest = QuestOf(Server);
            Assert.Equal(2, Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).Count);
            if (serverStartup)
            {
                Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
                Server.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered());
            }
            else
            {
                Server.Resolve<IPlayerManager>().ClearPeer(Owner.NetPeer);
                Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Owner.NetPeer, default(DisconnectInfo)));
            }
            Assert.Null(IssueOfHero(Server));
            AssertPartiesReleased();
            Assert.Empty(Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest));
        });
    }

    [Fact]
    public void ProximityWarningsAreShownOnlyOnceAndOnlyToTheOwner()
    {
        using var warnings = new MethodCallRecorder(AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        SendState(QuestScenario.ControllerA, false, 1);
        SendState(QuestScenario.ControllerA, false, 1);
        SendState(QuestScenario.ControllerA, false, 3);
        SendState(QuestScenario.ControllerA, false, 4);
        Assert.Equal(2, warnings.CountFor(Owner));
        Assert.Equal(0, warnings.CountFor(Other));
    }

    [Fact]
    public void RewardConversationMessageIsDeliveredOnlyToTheOwner()
    {
        Server.Call(() =>
        {
            BindParties();
            Server.Resolve<ICaravanAmbushProgress>().SendConversation(QuestOf(Server));
        });

        var received = Assert.Single(Owner.InternalMessages.GetMessages<NetworkCaravanAmbushConversation>());
        Assert.Equal(QuestScenario.ControllerA, received.ControllerId);
        Assert.Equal(scenario.NotableId, received.IssueOwnerId);
        Assert.Equal(QuestScenario.IssueId, received.IssueId);
        Assert.Empty(Other.InternalMessages.GetMessages<NetworkCaravanAmbushConversation>());
    }

    [Fact]
    public void SavedCaravanConversationWaitsForPausedCampaignAndOpensOnce()
    {
        using var conversations = new MethodCallRecorder(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        Owner.Call(() =>
        {
            GameStateManager._current = Game.Current.GameStateManager;
            // The battle mission still owns the screen.
            GameStateManager.Current._gameStates.Add(ObjectHelper.SkipConstructor<MissionState>());
            Assert.IsType<MissionState>(GameStateManager.Current.ActiveState);
        });
        SendState(QuestScenario.ControllerA, saved: true, warnings: 0);
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushConversation(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA)));
        Assert.Equal(0, conversations.CountFor(Owner));
        Owner.Call(() =>
        {
            TickOwner();
            Assert.Equal(0, conversations.CountFor(Owner));
            var states = Game.Current.GameStateManager;
            states._gameStates.Clear();
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            // Loot or another map menu is still open.
            map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            TickOwner();
            Assert.Equal(0, conversations.CountFor(Owner));
            // The owner is back on the free map.
            map._menuContext = null;
            TickOwner();
            Assert.Equal(1, conversations.CountFor(Owner));
            // The conversation already opened, so another tick must not reopen it.
            TickOwner();
            Assert.Equal(1, conversations.CountFor(Owner));
        });
        Assert.Equal(0, conversations.CountFor(Other));
    }

    [Fact]
    public void FinalizedQuestDiscardsItsPendingConversation()
    {
        using var conversations = new MethodCallRecorder(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        Owner.Call(() => GameStateManager._current = Game.Current.GameStateManager);
        SendState(QuestScenario.ControllerA, saved: true, warnings: 0);
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushConversation(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA)));
        Owner.Call(() => QuestOf(Owner).CompleteQuestWithCancel());
        Owner.Call(() =>
        {
            Game.Current.GameStateManager._gameStates.Add(Game.Current.GameStateManager.CreateState<MapState>());
            TickOwner();
        });
        Assert.Equal(0, conversations.CountFor(Owner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecruitmentRewardsOnlyTheQuestOwner(bool ownerRecruits)
    {
        var ownerGold = 0;
        var otherGold = 0;
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            BindParties();
            ownerGold = PlayerHero(QuestScenario.ControllerA).Gold;
            otherGold = PlayerHero(QuestScenario.ControllerB).Gold;
            var hero = PlayerHero(ownerRecruits ? QuestScenario.ControllerA : QuestScenario.ControllerB);
            Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            using (new GameInterface.Services.Heroes.Patches.MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnBanditPartyRecruited(bandits);
        });
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(ownerRecruits ? IssueOutcome.QuestSuccess : IssueOutcome.QuestCancel, finalized.Outcome);
        Server.Call(() =>
        {
            Assert.Equal(ownerGold + (ownerRecruits ? 1000 : 0), PlayerHero(QuestScenario.ControllerA).Gold);
            Assert.Equal(otherGold, PlayerHero(QuestScenario.ControllerB).Gold);
        });
    }

    [Fact]
    public void RecruitmentByAnotherPlayerCancelsTheQuestOfAnOwnerWithoutAParty()
    {
        Server.Call(() =>
        {
            BindParties();
            PlayerHero(QuestScenario.ControllerA)._partyBelongedTo = null;
            var hero = PlayerHero(QuestScenario.ControllerB);
            Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            using (new GameInterface.Services.Heroes.Patches.MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnBanditPartyRecruited(bandits);
        });
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OwnerTimeoutFinalizesTheQuestOnceWithTheTimeoutOutcome(bool ownerHasParty)
    {
        Server.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            if (!ownerHasParty) PlayerHero(QuestScenario.ControllerA)._partyBelongedTo = null;
        });
        var ownerQuest = QuestOf(Owner);
        Owner.Call(() => ownerQuest.CompleteQuestWithTimeOut());
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestTimeOut, finalized.Outcome);
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Theory]
    [InlineData(IssueOutcome.QuestCancel)]
    [InlineData(IssueOutcome.QuestFail)]
    public void TerminalReplayKeepsTheOwnersJournalAndEndsOnlyTheirQuest(IssueOutcome outcome)
    {
        var otherGiverId = scenario.AddIssue();
        Quest otherQuest = null!;
        Other.Call(() =>
        {
            var giver = Other.GetRegisteredObject<Hero>(otherGiverId);
            using (new AllowedThread())
            {
                otherQuest = new Quest("other_caravan_quest", giver, null, CampaignTime.Never, 1000, 0.5f);
                giver.Issue.IssueQuest = otherQuest;
                otherQuest.StartQuest();
            }
            Assert.True(Other.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                otherGiverId, giver.Issue.StringId, QuestScenario.ControllerB));
        });
        Server.Call(() =>
        {
            QuestOf(Server).AddLog(new TextObject("Follow the caravan"));
            QuestOf(Server).AddLog(new TextObject("The caravan failed"));
        });
        var ownerQuest = QuestOf(Owner);
        Owner.Call(() =>
        {
            if (outcome == IssueOutcome.QuestCancel) ownerQuest.CompleteQuestWithCancel();
            else ownerQuest.CompleteQuestWithFail();
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Owner.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Equal(new[] { "Follow the caravan", "The caravan failed" },
                ownerQuest.JournalEntries.Select(entry => entry.LogText.ToString()));
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
        });
        Other.Call(() =>
        {
            Assert.True(otherQuest.IsOngoing);
            Assert.Contains(otherQuest, Campaign.Current.QuestManager.Quests);
        });
    }

    private void TickOwner()
    {
        Campaign.Current._dt = 0f;
        Owner.Resolve<IMessageBroker>().Publish(this, new CampaignTick());
    }

    private static Quest CreateQuest(Hero giver) => new("caravan_test_quest", giver, null, CampaignTime.Never, 1000, 0.5f);

    private string ChangeGiverWhileOwnerIsTalking()
    {
        var newGiverId = environment.CreateRegisteredObject<Hero>();
        Owner.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var character = ObjectHelper.SkipConstructor<CharacterObject>();
            character.HeroObject = Owner.GetRegisteredObject<Hero>(scenario.NotableId);
            Campaign.Current.ConversationManager.SetupAndStartMapConversation(null,
                new MapConversationAgent(character), new MapConversationAgent(CharacterObject.PlayerCharacter));
            Assert.True(Campaign.Current.ConversationManager.IsConversationInProgress);
            // A held removal keeps later messages for the giver, including its move, in order.
            Owner.Resolve<IIssueConversationGuard>().RunWhenNotTalkingTo(() => { }, scenario.NotableId);
        }, new[] { AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.StartNew)) });
        Server.Call(() => Campaign.Current.IssueManager.ChangeIssueOwner(
            IssueOf(Server), Server.GetRegisteredObject<Hero>(newGiverId)));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueOwnerChanged>());
        return newGiverId;
    }

    private Hero PlayerHero(string controller)
    {
        Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controller, out var player));
        Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero);
        Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
        using (new AllowedThread()) hero.PartyBelongedTo = party;
        return hero;
    }

    private MapEvent StartAmbushBattle(MobileParty attacker, bool caravanOnWinningSide = true)
    {
        BindParties();
        var quest = QuestOf(Server);
        var battle = GameObjectCreator.CreateInitializedObject<MapEvent>();
        battle.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
        battle.Initialize(attacker.Party, quest._banditParty.Party, new FieldBattleEventComponent(battle), MapEvent.BattleTypes.FieldBattle);
        battle.MapEventVisual = null;
        // Keep the caravan on the winning side, as it is in a rescued ambush battle.
        if (caravanOnWinningSide) quest._caravanParty.Party.MapEventSide = battle.AttackerSide;
        return battle;
    }

    private void AssertPartiesReleased()
    {
        Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
        Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
        Assert.False(caravan.IsCurrentlyUsedByAQuest);
        Assert.False(bandits.IsCurrentlyUsedByAQuest);
        Assert.False(caravan.Ai.DoNotMakeNewDecisions);
        Assert.False(bandits.Ai.DoNotMakeNewDecisions);
    }

    private void BindParties()
    {
        Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
        Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
        using (new AllowedThread())
        {
            QuestOf(Server)._caravanParty = caravan;
            QuestOf(Server)._banditParty = bandits;
            caravan.SetPartyUsedByQuest(true);
            bandits.SetPartyUsedByQuest(true);
            caravan.Ai.SetDoNotMakeNewDecisions(true);
            bandits.Ai.SetDoNotMakeNewDecisions(true);
            QuestOf(Server).AddTrackedObject(caravan);
            QuestOf(Server).AddTrackedObject(bandits);
            caravan.Position = PlayerHero(QuestScenario.ControllerA).PartyBelongedTo.Position;
        }
    }

    private IssueBase IssueOf(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
        return giver.Issue;
    }

    private IssueBase? IssueOfHero(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
        return giver.Issue;
    }

    private Quest QuestOf(EnvironmentInstance instance) => Assert.IsType<Quest>(IssueOf(instance).IssueQuest);

    private void SendState(string controller, bool saved, int warnings, params string[] journal)
    {
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushProgress(
            scenario.NotableId, QuestScenario.IssueId, controller, caravanId, banditId, saved, warnings,
            journal.Select(text => new IssueJournalEntry(new JournalLog(CampaignTime.Now, new TextObject(text)))).ToArray())));
    }
}
