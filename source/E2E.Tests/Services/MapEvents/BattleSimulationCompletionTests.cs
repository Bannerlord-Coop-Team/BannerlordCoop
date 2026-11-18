using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.Players;
using Missions.Messages;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Exercises the shared simulation teardown instead of substituting direct map-event finalization.
/// </summary>
public class BattleSimulationCompletionTests : MapEventTestBase
{
    private const int MaxAdvancesToFinish = 200;

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
            StartAcceptedSimulation(client, context);
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent.BattleState = BattleState.AttackerVictory;
            var ended = new List<MapEvent>();
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                Server.NetworkSentMessages.Clear();
                AdvanceSimulation(client, context.MapEventId);

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

    [Fact]
    public void RetreatedPlayerStartsSimulation_AndWins_StillReceivesWinnerLoot()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            var playerParty = Server.GetRegisteredObject<MobileParty>(context.AttackerPartyId).Party;
            Server.Resolve<IMessageBroker>().Publish(this, new BattlePartyRetreated("1", context.MapEventId));
            Assert.True(Server.Resolve<IRetreatedMapEventPartyTracker>().IsRetreated(mapEvent, playerParty));

            StartAcceptedSimulation(client, context);

            mapEvent.BattleState = BattleState.AttackerVictory;
            AdvanceSimulation(client, context.MapEventId);

            var results = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkCommitMapEventResults>());
            // Only parties kept in the winners get a loot entry, even when it's empty.
            Assert.Contains(results.PlayerMapEventPartyId, results.PlayerLootData.LootedItems.Keys);
        }, MapEventDisabledMethods);
    }

    /// <summary>Sends <see cref="MaxAdvancesToFinish"/> one-round advances from the client, enough for a decided battle to finish.</summary>
    private void AdvanceSimulation(EnvironmentInstance client, string mapEventId)
    {
        for (var update = 0; update < MaxAdvancesToFinish; update++)
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(mapEventId, 1));
    }
}
