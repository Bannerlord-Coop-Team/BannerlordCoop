using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
            for (var frame = 0; frame < 200; frame++)
                tick.Invoke(scoreboard, new object[] { 1.1f });
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
    public void CompletedAdvance_NonCaptiveDefeat_CanLeaveAfterScoreboard()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
            encounter._attackerParty = MobileParty.MainParty.Party;
            encounter._defenderParty = encounter._encounteredParty;
            encounter.PlayerSide = BattleSideEnum.Attacker;
            encounter.EncounterState = PlayerEncounterState.End;
            encounter.BattleSimulation = new BattleSimulation(null, null);
            encounter.BattleSimulation.IsSimulationFinished = true;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = encounter.BattleSimulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            Assert.False(PlayerCaptivity.IsCaptive);
        }, MapEventDisabledMethods);

        var ended = new List<MapEvent>();
        try
        {
            Server.Call(() =>
            {
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
                Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                    System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                    context.MapEventId, context.AttackerPartyId));
                Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
                Server.GetRegisteredObject<MapEvent>(context.MapEventId).BattleState = BattleState.DefenderVictory;
                Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(context.MapEventId, 1));
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                Assert.Single(ended);
            }, MapEventDisabledMethods);

            client.Call(() =>
            {
                var encounter = PlayerEncounter.Current;
                Assert.NotNull(encounter);
                var simulation = encounter.BattleSimulation;
                Game.Current.GameStateManager.GameStates.OfType<MapState>().Single().EndBattleSimulation();
                simulation.OnFinished();
                PlayerEncounter.Update();
                Assert.Equal(PlayerEncounterState.Begin, encounter.EncounterState);
                Assert.Null(encounter._mapEvent);
                Assert.Null(encounter.BattleSimulation);

                MenuHelper.EncounterLeaveConsequence();

                Assert.Null(PlayerEncounter.Current);
                Assert.Null(MobileParty.MainParty.Party.MapEventSide);
            }, MapEventDisabledMethods
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)))
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)))
                // Headless campaigns have no navigation mesh for vanilla's post-encounter displacement.
                .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius))));
            Server.Call(() => Assert.Single(ended));
        }
        finally
        {
            Server.Call(() => CampaignEvents.MapEventEnded.ClearListeners(this));
        }
    }

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
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                context.MapEventId, context.AttackerPartyId));
            Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
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
                var inquiries = new List<InquiryData>();
                void RecordInquiry(InquiryData inquiry, bool pause, bool prioritize) => inquiries.Add(inquiry);
                // Publicizer exposes both the event and its backing field under the same name.
                var showInquiry = typeof(InformationManager).GetEvent("OnShowInquiry");
                showInquiry.AddEventHandler(null, (System.Action<InquiryData, bool, bool>)RecordInquiry);
                try
                {
                    simulation.Pause();
                    scoreboard.ExecuteQuitAction();
                    Assert.False(simulation.IsPlayerRetreated);
                    Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                    Assert.Single(inquiries).AffirmativeAction();
                    Assert.True(simulation.IsPlayerRetreated);
                    Assert.False(mapState.IsSimulationActive);
                    Assert.Single(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                }
                finally
                {
                    showInquiry.RemoveEventHandler(null, (System.Action<InquiryData, bool, bool>)RecordInquiry);
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
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                context.MapEventId, context.AttackerPartyId));
            Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
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
}
