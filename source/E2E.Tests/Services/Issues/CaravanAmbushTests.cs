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
using GameInterface.Services.Issues.Quests.CaravanAmbush;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Players;
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
        Owner.Call(() => QuestOf(Owner).StartQuest());
    }

    public void Dispose()
    {
        Owner.Call(() => GameStateManager._current = previousGameStateManager);
        environment.Dispose();
    }

    [Fact]
    public void CreationKeepsTheServersDestination()
    {
        var targetId = environment.CreateRegisteredObject<Settlement>();
        byte[] capture = null!;
        Server.Call(() =>
        {
            Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver);
            Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target);
            using (new AllowedThread()) capture = Server.Resolve<CaravanAmbushCreationCaptureStrategy>().Capture(new Issue(giver, target));
        });
        Owner.Call(() =>
        {
            Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver);
            Owner.ObjectManager.TryGetObject<Settlement>(targetId, out var target);
            using (new AllowedThread())
                Assert.Same(target, ((Issue)Owner.Resolve<CaravanAmbushCreationCaptureStrategy>().CreateIssue(giver, capture))._targetSettlement);
        });
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
        Other.Call(() => Assert.Null(IssueOf(Other).IssueQuest));
    }

    [Fact]
    public void ProgressDoesNotBindAPlayerWhoLostTheAcceptRace()
    {
        scenario.AttachQuest(Other, CreateQuest);
        SendState(QuestScenario.ControllerA, true, 3, "Saved");
        Other.Call(() =>
        {
            Assert.Null(QuestOf(Other)._caravanParty);
            Assert.Empty(QuestOf(Other).JournalEntries);
        });
    }

    [Fact]
    public void StatePublishesOnlyChangedOwnerState()
    {
        scenario.AttachQuest(Other, CreateQuest);
        Server.Call(() =>
        {
            var publisher = Server.Resolve<ICaravanAmbushStatePublisher>();
            var baseline = Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushState>();
            publisher.Publish(QuestOf(Server));
            Assert.Equal(baseline + 1, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushState>());

            publisher.Publish(QuestOf(Server));
            Assert.Equal(baseline + 1, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushState>());

            QuestOf(Server)._vicinityCheckFailCounter = 1;
            publisher.Publish(QuestOf(Server));
            Assert.Equal(baseline + 2, Server.NetworkSentMessages.GetMessageCount<NetworkCaravanAmbushState>());
        });
        Owner.Call(() => Assert.Equal(1, QuestOf(Owner)._vicinityCheckFailCounter));
        Other.Call(() => Assert.Equal(0, QuestOf(Other)._vicinityCheckFailCounter));
    }

    [Fact]
    public void ServerMirrorIsNeverStarted()
    {
        Server.Call(() =>
        {
            var quest = QuestOf(Server);
            quest.StartQuest();
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
        });
    }

    [Fact]
    public void OwnerRewardRequestCannotFinishAnUnsavedCaravan()
    {
        Owner.Call(() => QuestOf(Owner).OnQuestSucceeded());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.True(QuestOf(Server).IsOngoing));
    }

    [Fact]
    public void AnotherPlayersRewardRequestDoesNotReachTheQuest()
    {
        scenario.AttachQuest(Other, CreateQuest);
        Other.Call(() => QuestOf(Other).OnQuestSucceeded());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
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
        Other.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BattleOutcomeUsesTheQuestOwnersParticipation(bool ownerParticipated)
    {
        using var survived = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnCaravanSurvivedWithoutHelp)));
        Server.Call(() =>
        {
            BindParties();
            var quest = QuestOf(Server);
            var attacker = ownerParticipated ? PlayerHero(QuestScenario.ControllerA).PartyBelongedTo : GameObjectCreator.CreateInitializedObject<MobileParty>();
            var battle = GameObjectCreator.CreateInitializedObject<MapEvent>();
            battle.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            battle.Initialize(attacker.Party, quest._banditParty.Party, new FieldBattleEventComponent(battle), MapEvent.BattleTypes.FieldBattle);
            battle.MapEventVisual = null;
            // Keep the caravan on the winning side, as it is in a rescued ambush battle.
            quest._caravanParty.Party.MapEventSide = battle.AttackerSide;
            battle.BattleState = BattleState.AttackerVictory;
            CampaignEventDispatcher.Instance.OnMapEventEnded(battle);
            Assert.True(quest._isCaravanSaved);
            Assert.True(quest._caravanParty.IsCurrentlyUsedByAQuest);
            Assert.True(quest._caravanParty.Ai.DoNotMakeNewDecisions);
            Assert.False(quest._banditParty.IsCurrentlyUsedByAQuest);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });
        Assert.Equal(ownerParticipated ? 0 : 1, survived.CountFor(Server));
        Assert.Equal(ownerParticipated ? 1 : 0, Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushConversation>().Count());
    }

    [Fact]
    public void FinalizationReleasesBothParties()
    {
        Server.Call(BindParties);
        Owner.Call(() => QuestOf(Owner).CompleteQuestWithCancel());
        Server.Call(() =>
        {
            Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
            Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            Assert.False(caravan.IsCurrentlyUsedByAQuest);
            Assert.False(bandits.IsCurrentlyUsedByAQuest);
            Assert.False(caravan.Ai.DoNotMakeNewDecisions);
            Assert.False(bandits.Ai.DoNotMakeNewDecisions);
        });
    }

    [Fact]
    public void DisconnectDismissalReleasesBothParties()
    {
        Server.Call(() =>
        {
            BindParties();
            using (new AllowedThread()) IssueOf(Server)._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            var quest = QuestOf(Server);
            Assert.Equal(2, Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).Count);
            Server.Resolve<IPlayerManager>().ClearPeer(Owner.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Owner.NetPeer, default(DisconnectInfo)));
            Assert.Null(IssueOfHero(Server));
            Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
            Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            Assert.False(caravan.IsCurrentlyUsedByAQuest);
            Assert.False(bandits.IsCurrentlyUsedByAQuest);
            Assert.False(caravan.Ai.DoNotMakeNewDecisions);
            Assert.False(bandits.Ai.DoNotMakeNewDecisions);
            Assert.Empty(Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest));
        });
    }

    [Fact]
    public void ServerStartupDismissesASavedQuestAndReleasesItsParties()
    {
        Server.Call(() =>
        {
            BindParties();
            using (new AllowedThread()) IssueOf(Server)._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            var quest = QuestOf(Server);
            Assert.Equal(2, Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).Count);
            Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            Server.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered());
            Assert.Null(IssueOfHero(Server));
            Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan);
            Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            Assert.False(caravan.IsCurrentlyUsedByAQuest);
            Assert.False(bandits.IsCurrentlyUsedByAQuest);
            Assert.False(caravan.Ai.DoNotMakeNewDecisions);
            Assert.False(bandits.Ai.DoNotMakeNewDecisions);
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
    public void SavedCaravanConversationOpensOnlyForTheOwner()
    {
        using var conversations = new MethodCallRecorder(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        Owner.Call(() =>
        {
            GameStateManager._current = Game.Current.GameStateManager;
            GameStateManager.Current._gameStates.Add(GameStateManager.Current.CreateState<MapState>());
        });
        SendState(QuestScenario.ControllerA, true, 0);
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushConversation(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA, caravanId)));
        Assert.Equal(1, conversations.CountFor(Owner));
        Assert.Equal(0, conversations.CountFor(Other));
    }

    [Fact]
    public void SavedCaravanConversationWaitsForMissionAndLootScreensAndOpensOnce()
    {
        using var conversations = new MethodCallRecorder(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        Owner.Call(() =>
        {
            GameStateManager._current = Game.Current.GameStateManager;
            GameStateManager.Current._gameStates.Add(ObjectHelper.SkipConstructor<MissionState>());
            Assert.IsType<MissionState>(GameStateManager.Current.ActiveState);
        });
        SendState(QuestScenario.ControllerA, true, 0);
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushConversation(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA, caravanId)));
        Assert.Equal(0, conversations.CountFor(Owner));
        Owner.Call(() =>
        {
            CampaignEvents.Instance.Tick(0f);
            Assert.Equal(0, conversations.CountFor(Owner));
            var states = Game.Current.GameStateManager;
            states._gameStates.Clear();
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            CampaignEvents.Instance.Tick(0f);
            Assert.Equal(0, conversations.CountFor(Owner));
            map._menuContext = null;
            CampaignEvents.Instance.Tick(0f);
            Assert.Equal(1, conversations.CountFor(Owner));
            CampaignEvents.Instance.Tick(0f);
            Assert.Equal(1, conversations.CountFor(Owner));
        });
        Assert.Equal(0, conversations.CountFor(Other));
    }

    [Fact]
    public void FinalizedQuestDiscardsItsPendingConversation()
    {
        using var conversations = new MethodCallRecorder(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        Owner.Call(() => GameStateManager._current = Game.Current.GameStateManager);
        SendState(QuestScenario.ControllerA, true, 0);
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushConversation(
            scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA, caravanId)));
        Owner.Call(() => QuestOf(Owner).CompleteQuestWithCancel());
        Owner.Call(() =>
        {
            Game.Current.GameStateManager._gameStates.Add(Game.Current.GameStateManager.CreateState<MapState>());
            CampaignEvents.Instance.Tick(0f);
        });
        Assert.Equal(0, conversations.CountFor(Owner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecruitmentRewardsOnlyTheQuestOwner(bool ownerRecruits)
    {
        using var hiring = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnPlayerHiredBandits)));
        Server.Call(() =>
        {
            BindParties();
            var recruiter = PlayerHero(ownerRecruits ? QuestScenario.ControllerA : QuestScenario.ControllerB).PartyBelongedTo;
            Server.ObjectManager.TryGetObject<MobileParty>(banditId, out var bandits);
            Server.Resolve<IMessageBroker>().Publish(this, new BanditPartyRecruitedByPlayer(bandits, recruiter));
        });
        Assert.Equal(ownerRecruits ? 1 : 0, hiring.CountFor(Server));
        Assert.Equal(ownerRecruits ? 0 : 1, Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientTicksDoNotMovePartiesOrApplyTimeoutPenalties(bool timeout)
    {
        using var ticks = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick)));
        Owner.Call(() =>
        {
            var quest = QuestOf(Owner);
            if (timeout) quest.OnTimedOut();
            else quest.HourlyTick();
            Assert.Equal(0, quest.RelationshipChangeWithQuestGiver);
            Assert.Empty(quest.JournalEntries);
        });
        Assert.Equal(0, ticks.CountFor(Owner));
    }

    [Fact]
    public void TimeoutUsesTheOwnerDispatchPathOnly()
    {
        Server.Call(() => QuestOf(Server).ChangeQuestDueTime(CampaignTime.Now - CampaignTime.Hours(1f)));
        Owner.Call(() => QuestOf(Owner).ChangeQuestDueTime(CampaignTime.Now - CampaignTime.Hours(1f)));
        Server.Call(() => CampaignEventDispatcher.Instance.HourlyTick());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        Owner.Call(() => Campaign.Current.QuestManager.HourlyTick());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.False(QuestOf(Server).IsOngoing));
    }

    [Theory]
    [InlineData(IssueOutcome.QuestCancel)]
    [InlineData(IssueOutcome.QuestFail)]
    public void TerminalReplayKeepsTheOwnersJournalAndEndsOnlyTheirQuest(IssueOutcome outcome)
    {
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
            Assert.Equal(2, ownerQuest.JournalEntries.Count);
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
        });
        Other.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    private static Quest CreateQuest(Hero giver) => new("caravan_test_quest", giver, null, CampaignTime.Never, 1000, 0.5f);

    private Hero PlayerHero(string controller)
    {
        Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controller, out var player));
        Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero);
        Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
        using (new AllowedThread()) hero.PartyBelongedTo = party;
        return hero;
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
        PlayerHero(QuestScenario.ControllerA);
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
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkCaravanAmbushState(
            scenario.NotableId, QuestScenario.IssueId, controller, caravanId, banditId, saved, warnings, journal)));
    }
}
