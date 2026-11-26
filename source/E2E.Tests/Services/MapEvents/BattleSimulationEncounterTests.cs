using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions.Messages;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.Scoreboard;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>Exercises client encounter exit after authoritative simulation completion.</summary>
public class BattleSimulationEncounterTests : MapEventTestBase
{
    public BattleSimulationEncounterTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DestroyedEvent_BeforePlaybackFinishes_ScoreboardTickKeepsPresentationOpen()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        SPScoreboardVM scoreboard = null;
        BattleSimulation simulation = null;
        string playerPartyId = null;
        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            Assert.True(client.ObjectManager.TryGetId(MobileParty.MainParty.Party, out playerPartyId));
            var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
            simulation = new BattleSimulation(null, null);
            encounter.BattleSimulation = simulation;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = simulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            BattleSimulationReplay.Begin(context.MapEventId);
            scoreboard = SPScoreboardVM.CreateSimulation(simulation);
            scoreboard.IsSimulation = true;
            Assert.False(scoreboard.IsOver);
        }, MapEventDisabledMethods);

        client.SimulateMessage(Server.NetPeer, new NetworkDestroyInstance<MapEvent>(
            client.GetHandle<MapEvent>(context.MapEventId)));
        client.SimulateMessage(Server.NetPeer, new NetworkClosePvpEncounter(
            new[] { playerPartyId }, mapEventId: context.MapEventId));

        client.Call(() =>
        {
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.False(scoreboard.IsOver);
            Assert.False(simulation.IsSimulationFinished);
            // The view can tick between event destruction and the final playback message.
            // OnTick is protected in the game assembly used by the test project.
            using var moraleReads = new MethodCallRecorder(Priority.First, true,
                AccessTools.Method(typeof(MapEventSide), nameof(MapEventSide.GetSideMorale)));
            var tick = AccessTools.Method(typeof(SPScoreboardVM), "OnTick");
            const int ScoreboardTicks = 200;
            const float TickSeconds = 1.1f; // Match the game tick duration used while the scoreboard is displayed.
            for (var frame = 0; frame < ScoreboardTicks; frame++)
                tick.Invoke(scoreboard, new object[] { TickSeconds });
            Assert.Equal(0, moraleReads.CountFor(client));
            Assert.True(Game.Current.GameStateManager.GameStates.OfType<MapState>().Single().IsSimulationActive);
        }, MapEventDisabledMethods);

        client.SimulateMessage(Server.NetPeer, new NetworkBattleSimulationFinished(context.MapEventId));
        client.Call(() =>
        {
            BattleSimulationReplay.Tick(simulation, 0f);
            Assert.True(simulation.IsSimulationFinished);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CompletedAdvance_NonCaptiveDefeat_LeavesEncounterAfterScoreboard()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        LoseSimulationWithoutCapture(client, context, BattleSideEnum.Attacker);
        var holds = new List<PartyBehaviorChangeAttempted>();
        client.Resolve<IMessageBroker>().Subscribe<PartyBehaviorChangeAttempted>(payload => holds.Add(payload.What));

        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            CloseSimulationScoreboard();

            Assert.False(PlayerCaptivity.IsCaptive);
            Assert.Null(PlayerEncounter.Current);
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.False(Game.Current.GameStateManager.GameStates.OfType<MapState>().Single().AtMenu);
            Assert.Same(MobileParty.MainParty, Assert.Single(holds).Party);
        }, ScoreboardCloseDisabledMethods);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
    }

    [Fact]
    public void CompletedAdvance_NonCaptiveDefeatInsideSettlement_AsksForTheSettlementAgain()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        LoseSimulationWithoutCapture(client, context, BattleSideEnum.Defender, settlementId);
        var settlementRequests = new List<StartSettlementEncounterAttempted>();
        var leaves = new List<PartyLeaveSettlementAttempted>();
        client.Resolve<IMessageBroker>().Subscribe<StartSettlementEncounterAttempted>(payload => settlementRequests.Add(payload.What));
        client.Resolve<IMessageBroker>().Subscribe<PartyLeaveSettlementAttempted>(payload => leaves.Add(payload.What));

        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            CloseSimulationScoreboard();

            var settlement = client.GetRegisteredObject<Settlement>(settlementId);
            Assert.False(PlayerCaptivity.IsCaptive);
            Assert.Same(settlement, MobileParty.MainParty.CurrentSettlement);
            Assert.Same(settlement, Assert.Single(settlementRequests).Settlement);
            Assert.Empty(leaves);
        }, ScoreboardCloseDisabledMethods);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
    }

    /// <summary>
    /// Has the client's party on <paramref name="playerSide"/> lose an accepted simulation without being taken prisoner,
    /// leaving its finished scoreboard open. The party stays inside <paramref name="settlementId"/> when one is given.
    /// </summary>
    private void LoseSimulationWithoutCapture(EnvironmentInstance client, MapEventContext context, BattleSideEnum playerSide,
        string? settlementId = null)
    {
        string playerPartyId = context.AttackerPartyId;
        string enemyPartyId = context.DefenderPartyId;
        BattleState enemyVictory = BattleState.DefenderVictory;
        if (playerSide == BattleSideEnum.Defender)
        {
            playerPartyId = context.DefenderPartyId;
            enemyPartyId = context.AttackerPartyId;
            enemyVictory = BattleState.AttackerVictory;
        }

        client.Resolve<IControllerIdProvider>().SetControllerId("1");
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), playerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            new EncounterGameMenuBehavior().OnSessionLaunched(
                new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager));
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(playerPartyId);
            if (settlementId != null)
                MobileParty.MainParty._currentSettlement = client.GetRegisteredObject<Settlement>(settlementId);

            var encounter = SetMockPlayerEncounter(client, enemyPartyId, context.MapEventId);
            encounter._attackerParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId).Party;
            encounter._defenderParty = client.GetRegisteredObject<MobileParty>(context.DefenderPartyId).Party;
            encounter.PlayerSide = playerSide;
            encounter.EncounterState = PlayerEncounterState.End;
            encounter.BattleSimulation = new BattleSimulation(null, null);
            encounter.BattleSimulation.IsSimulationFinished = true;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = encounter.BattleSimulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            Assert.False(PlayerCaptivity.IsCaptive);
        }, MapEventDisabledMethods);

        var ended = new List<MapEvent>();
        Server.Call(() =>
        {
            EnsureEncounterModel();
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                StartAcceptedSimulation(client, context, playerPartyId);
                var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
                // The player's side ran away, so the server takes no prisoners.
                mapEvent.RetreatingSide = playerSide;
                mapEvent.BattleState = enemyVictory;
                Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(context.MapEventId, 1));
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                Assert.Single(ended);
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, BattleResultDisabledMethods);
    }

    /// <summary>Closes the finished scoreboard the way its Done does, which reopens the encounter menu.</summary>
    private static void CloseSimulationScoreboard()
    {
        var simulation = PlayerEncounter.Current.BattleSimulation;
        Game.Current.GameStateManager.GameStates.OfType<MapState>().Single().EndBattleSimulation();
        simulation.OnFinished();
    }

    private IReadOnlyList<MethodBase> ScoreboardCloseDisabledMethods => MapEventDisabledMethods
        // Headless campaigns have no navigation mesh for vanilla's post-encounter displacement.
        .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius)))
        // Only the encounter texts are turned off, because they read party names the headless heroes can't build.
        // EncounterGameMenuBehavior's own init still runs, so a restarted battle shows up as a NetworkRequestCreateMapEvent.
        .Append(AccessTools.Method(typeof(PlayerEncounter), "SetEncounterMenuTexts"))
        .Append(AccessTools.Method(typeof(DefaultEncounter), nameof(DefaultEncounter.game_menu_encounter_on_init)))
        .ToList();

    [Fact]
    public void NativeScoreboardRetreat_FinalizesLeaderMapEvent_AndClosesSimulation()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        var ended = new List<MapEvent>();
        Server.Call(() =>
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            StartAcceptedSimulation(client, context);
        }, MapEventDisabledMethods);

        try
        {
            client.Call(() =>
            {
                Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
                var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
                var simulation = new BattleSimulation(null, null);
                encounter.BattleSimulation = simulation;
                var mapState = Game.Current.GameStateManager.CreateState<MapState>();
                mapState._battleSimulation = simulation;
                Game.Current.GameStateManager._gameStates.Add(mapState);
                BattleSimulationReplay.Begin(context.MapEventId, spectator: false);

                var scoreboard = SPScoreboardVM.CreateSimulation(simulation);
                scoreboard.IsSimulation = true;
                // ScoreboardBaseVM is not publicized; replace only the renderer's release callback.
                AccessTools.Field(typeof(ScoreboardBaseVM), "_releaseSimulationSources")
                    .SetValue(scoreboard, (System.Action)mapState.EndBattleSimulation);
                using (var inquiries = new InquiryRecorder())
                {
                    simulation.Pause();
                    scoreboard.ExecuteQuitAction();
                    Assert.False(simulation.IsPlayerRetreated);
                    Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                    Assert.Single(inquiries.Items).AffirmativeAction();
                    Assert.True(simulation.IsPlayerRetreated);
                    Assert.False(mapState.IsSimulationActive);
                    Assert.Single(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                }
            }, MapEventDisabledMethods
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu))));

            Server.Call(() =>
            {
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationCancelled>());
                Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                Assert.Single(ended);
                Assert.False(ServerBattleModeArbiter.IsClaimed(context.MapEventId));
            }, MapEventDisabledMethods);

            client.Call(() =>
            {
                client.SimulateMessage(Server.NetPeer, new NetworkBattleSimulationCancelled(context.MapEventId));
                var mapState = Game.Current.GameStateManager.GameStates.OfType<MapState>().Single();
                Assert.False(mapState.IsSimulationActive);
                Assert.Null(PlayerEncounter.Current?.BattleSimulation);
                Assert.False(BattleSimulationReplay.IsActiveFor(context.MapEventId));
            }, MapEventDisabledMethods);
        }
        finally
        {
            Server.Call(() => CampaignEvents.MapEventEnded.ClearListeners(this));
        }
    }

    [Fact]
    public void ServerCancellation_ClosesInitiatingSimulation()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        Server.Call(() =>
        {
            StartAcceptedSimulation(client, context);
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
            var simulation = new BattleSimulation(null, null);
            encounter.BattleSimulation = simulation;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = simulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            BattleSimulationReplay.Begin(context.MapEventId, spectator: false);

            client.SimulateMessage(Server.NetPeer, new NetworkBattleSimulationCancelled(context.MapEventId));

            Assert.False(mapState.IsSimulationActive);
            Assert.Null(encounter.BattleSimulation);
            Assert.False(BattleSimulationReplay.IsActiveFor(context.MapEventId));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void RetreatedDefender_LosesSendTroops_IsTakenPrisonerOffTheEncounterMenu()
    {
        const string playerId = "PlayerOne";
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(playerId);
        var (heroId, partyId) = CreatePlayerHeroParty(playerId);
        Server.Resolve<IPlayerManager>().SetPeer(playerId, client.NetPeer);
        var captorId = CreateServerParty();
        var mapEventId = CreateBattleStateDefeatMapEvent(heroId, partyId, captorId);

        Server.Call(() =>
        {
            EnsureEncounterModel();
            Campaign.Current.MapEventManager.OnMapEventCreated(Server.GetRegisteredObject<MapEvent>(mapEventId));
            // The knocked out player retreated from the mission before sending the troops.
            Server.Resolve<IMessageBroker>().Publish(this, new BattlePartyRetreated(playerId, mapEventId));
        }, MapEventDisabledMethods);

        EnableHeadlessEncounterFinish(client);
        var encounter = SetMockPlayerEncounter(client, captorId, mapEventId);
        BattleSimulation simulation = null;
        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(partyId);
            Assert.Same(client.GetRegisteredObject<MapEvent>(mapEventId), MobileParty.MainParty.MapEvent);

            var behavior = new EncounterGameMenuBehavior();
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            behavior.OnSessionLaunched(starter);
            // PlayerTownVisitCampaignBehavior registers prisoner_wait in game; an empty wait menu is enough here.
            starter.AddWaitGameMenu("prisoner_wait", "", null, null, null, null, GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            encounter._alternativeReceivedLootShips = new List<Ship>();
            encounter.PlayerSide = BattleSideEnum.Defender;
            encounter.OpponentSide = BattleSideEnum.Attacker;
            encounter.EncounterState = PlayerEncounterState.Wait;
            Game.Current.GameStateManager._gameStates.Add(Game.Current.GameStateManager.CreateState<MapState>());

            UseBattleStartCoordinatorOf(client);
            behavior.game_menu_encounter_order_attack_on_consequence(new MenuCallbackArgs((MenuContext)null, null));

            simulation = encounter.BattleSimulation;
            Assert.NotNull(simulation);
            Assert.True(BattleSimulationReplay.IsActiveFor(mapEventId));
        }, MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(BeHostileAction), nameof(BeHostileAction.ApplyEncounterHostileAction)))
            // The client's local troop setup reads the engine config the harness lacks; the server owns the simulation.
            .Append(AccessTools.Method(typeof(BattleSimulation), nameof(BattleSimulation.ResetSimulation))));

        // Every sent troop died, so the attackers win the server's simulation on the next client paced advance.
        Server.Call(() => Server.GetRegisteredObject<MapEvent>(mapEventId).BattleState = BattleState.AttackerVictory,
            BattleResultDisabledMethods);
        client.Call(() => BattleSimulationReplay.Tick(simulation, 1f), BattleResultDisabledMethods);

        Server.Call(() =>
        {
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
            Assert.Same(Server.GetRegisteredObject<MobileParty>(captorId).Party,
                Server.GetRegisteredObject<Hero>(heroId).PartyBelongedToAsPrisoner);
        });

        client.Call(() =>
        {
            BattleSimulationReplay.Tick(simulation, 0f);
            Assert.True(simulation.IsSimulationFinished);

            CloseSimulationScoreboard();

            Assert.True(PlayerCaptivity.IsCaptive);
            Assert.Null(PlayerEncounter.Current);
            Assert.Null(MobileParty.MainParty.MapEvent);
            var mapState = Game.Current.GameStateManager.GameStates.OfType<MapState>().Single();
            Assert.Equal("prisoner_wait", mapState.MenuContext?.GameMenu?.StringId);
        }, MapEventDisabledMethods);
    }

    private sealed class InquiryRecorder : IDisposable
    {
        private readonly EventInfo showInquiry = typeof(InformationManager).GetEvent("OnShowInquiry");
        private readonly Action<InquiryData, bool, bool> record;
        public List<InquiryData> Items { get; } = new();

        public InquiryRecorder()
        {
            record = (inquiry, _, _) => Items.Add(inquiry);
            showInquiry.AddEventHandler(null, record);
        }

        public void Dispose() => showInquiry.RemoveEventHandler(null, record);
    }
}
