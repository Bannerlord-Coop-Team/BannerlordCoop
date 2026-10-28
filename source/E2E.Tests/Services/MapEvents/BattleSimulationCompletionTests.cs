using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Exercises the shared simulation teardown instead of substituting direct map-event finalization.
/// </summary>
public class BattleSimulationCompletionTests : MapEventTestBase
{
    public BattleSimulationCompletionTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(BattleState.DefenderVictory)]
    [InlineData(BattleState.AttackerVictory)]
    public void CompletedSimulation_FinalizesMapEventOnce(BattleState result)
    {
        var context = CreateServerMapEvent();
        var ended = new List<MapEvent>();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent.BattleState = result;
            var simulation = new BattleSimulationRunHandler.ActiveSimulation { MapEvent = mapEvent };
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                BattleSimulationRunHandler.EndSimulationSession(simulation, completed: true);

                Assert.True(mapEvent.IsFinalized);
                Assert.Same(mapEvent, Assert.Single(ended));

                for (var update = 0; update < 200; update++)
                    BattleSimulationRunHandler.EndSimulationSession(simulation, completed: true);

                Assert.Same(mapEvent, Assert.Single(ended));
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _)));
    }

    [Theory]
    [InlineData(BattleState.None)]
    [InlineData(BattleState.AttackerVictory)]
    [InlineData(BattleState.DefenderVictory)]
    public void CancelledEmptySimulation_RestoresObserverWithoutFinalizing(BattleState result)
    {
        var context = CreateServerMapEvent();
        var ended = new List<MapEvent>();
        var previousObserver = Mock.Of<IBattleObserver>();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent.BattleState = result;
            var simulation = new BattleSimulationRunHandler.ActiveSimulation
            {
                MapEvent = mapEvent,
                PreviousObserver = previousObserver,
            };
            Assert.All(mapEvent._sides, side => Assert.True(side._simulationTroopList == null || side._simulationTroopList.Count == 0));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                BattleSimulationRunHandler.EndSimulationSession(simulation, completed: false);

                Assert.False(mapEvent.IsFinalized);
                Assert.Empty(ended);
                Assert.Same(previousObserver, mapEvent.BattleObserver);
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _)));
    }

    [Fact]
    public void CompletedAdvance_SendsLootBeforeRemovingMapEvent_AndFinishesOnce()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        var lootSawRegisteredEvent = new List<bool>();
        client.Resolve<IMessageBroker>().Subscribe<NetworkBattleSimulationLoot>(payload =>
            lootSawRegisteredEvent.Add(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _)));

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                context.MapEventId, context.AttackerPartyId));
            Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent.BattleState = BattleState.AttackerVictory;
            var ended = new List<MapEvent>();
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                Server.NetworkSentMessages.Clear();
                for (var update = 0; update < 200; update++)
                    Server.Resolve<IMessageBroker>().Publish(client.NetPeer,
                        new NetworkAdvanceBattleSimulation(context.MapEventId, 1));

                Assert.True(mapEvent.IsFinalized);
                Assert.Same(mapEvent, Assert.Single(ended));
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationLoot>());
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);

        Assert.True(Assert.Single(lootSawRegisteredEvent));
    }

    [Fact]
    public void ManuallyFinalizedSimulation_DoesNotRepeatTeardownOrCallbacks()
    {
        var context = CreateServerMapEvent();
        var ended = new List<MapEvent>();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            var simulation = new BattleSimulationRunHandler.ActiveSimulation { MapEvent = mapEvent };
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                mapEvent.FinalizeEvent();
                Assert.True(mapEvent.IsFinalized);
                Assert.Same(mapEvent, Assert.Single(ended));

                using var teardown = new MethodCallRecorder(Priority.First, true,
                    AccessTools.Method(typeof(MapEvent), nameof(MapEvent.CommitXpGains)),
                    AccessTools.Method(typeof(MapEventSide), nameof(MapEventSide.EndSimulation)));
                var observer = Mock.Of<IBattleObserver>();
                mapEvent.BattleObserver = observer;

                for (var update = 0; update < 200; update++)
                    BattleSimulationRunHandler.EndSimulationSession(simulation, completed: true);

                Assert.Equal(0, teardown.CountFor(Server));
                Assert.Same(observer, mapEvent.BattleObserver);
                Assert.Same(mapEvent, Assert.Single(ended));
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);
    }
}
