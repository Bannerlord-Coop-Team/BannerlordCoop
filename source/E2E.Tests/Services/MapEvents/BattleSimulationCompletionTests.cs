using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
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
                Assert.Equal((int)BattleStartMode.Unclaimed,
                    Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>()).Mode);
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(ServerBattleModeArbiter.IsClaimed(context.MapEventId));
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);

        Assert.True(Assert.Single(lootSawRegisteredEvent));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _)));
    }
}
