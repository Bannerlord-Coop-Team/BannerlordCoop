using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public class CrossOwnerMountCatchUpTests : MissionTestEnvironment
{
    public CrossOwnerMountCatchUpTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatchUp_PreservesForeignHorseAuthorityAfterDismount(bool bufferAcrossRiderDeparture)
    {
        using var engine = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("A", "B", "C");
        string characterId = CreateRegisteredObject<CharacterObject>();
        var clients = Clients.ToArray();
        var horseOwner = clients[0];
        var riderOwner = clients[1];
        var joiner = clients[2];
        Guid riderId = Guid.NewGuid();
        Guid horseId = Guid.NewGuid();
        var missions = new Dictionary<EnvironmentInstance, MockMission>();
        var controllers = new Dictionary<EnvironmentInstance, CoopBattleController>();
        try
        {
            foreach (var instance in clients)
            {
                instance.Call(() =>
                {
                    var mission = CreateConnectedMission(engine, instance, mapEventId);
                    var controller = instance.Resolve<CoopBattleController>();
                    controller.Session.TryBegin(mapEventId);
                    BattleSpawnGate.BeginBattle(mapEventId);
                    instance.Resolve<IBattleHostRegistry>().Set(
                        mapEventId, new BattleHostAssignment("C", new[] { "A", "B" }, 1));
                    missions.Add(instance, mission);
                    controllers.Add(instance, controller);
                });
            }
            foreach (var instance in clients)
                foreach (string id in new[] { "A", "B", "C" })
                    instance.Call(() => instance.Resolve<IMessageBroker>().Publish(
                        this, new NetworkMissionPeerEntered(id, mapEventId)));

            foreach (var instance in new[] { horseOwner, riderOwner })
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject(characterId, out CharacterObject character));
                    MockMission mission = missions[instance];
                    Agent rider = mission.SpawnAgent(new AgentBuildData(character)
                        .Controller(instance == riderOwner ? AgentControllerType.AI : AgentControllerType.None)
                        .Team(mission.DefenderTeam.Shell)
                        .Equipment(new Equipment()));
                    Agent horse = mission.SpawnMount(rider);
                    var registry = instance.Resolve<INetworkAgentRegistry>();
                    Assert.True(registry.TryRegisterAgent("B", riderId, 1, rider, 3));
                    Assert.True(registry.TryRegisterAgent("A", horseId, 2, horse, 7));
                });
            }

            Agent capacityBlocker = null;
            joiner.Call(() =>
            {
                if (bufferAcrossRiderDeparture)
                {
                    for (int i = 0; i < 2000; i++)
                        capacityBlocker = missions[joiner].SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                }
                missions[joiner].SpawnMounted = true;
            });
            riderOwner.Resolve<MockBattleNetwork>().NetworkSentMessages.Clear();
            riderOwner.Call(() => riderOwner.Resolve<IMessageBroker>().Publish(
                this, new NetworkMissionPeerEntered("C", mapEventId)));

            riderOwner.Call(() =>
            {
                var batch = Assert.Single(riderOwner.Resolve<MockBattleNetwork>().NetworkSentMessages
                    .GetMessages<NetworkSpawnBattleAgents>());
                Assert.Equal(SpawnBatchPurpose.CatchUp, batch.Purpose);
                var wire = ProtoBuf.Serializer.DeepClone(batch);
                Assert.True(riderOwner.Resolve<IBattleAgentSpawnBatchCodec>()
                    .TryDecode(wire, out var records));
                var record = Assert.Single(records);
                Assert.Equal("B", record.OwnerControllerId);
                Assert.Equal("A", record.MountOwnerControllerId);
                Assert.Equal(3, record.AuthorityRevision);
                Assert.Equal(7, record.MountAuthorityRevision);
            });

            if (bufferAcrossRiderDeparture)
            {
                joiner.Call(() =>
                {
                    Assert.False(joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out _));
                    joiner.Resolve<IMessageBroker>().Publish(this, new MissionPeerDisconnected("B", mapEventId));
                    missions[joiner].DeleteAgent(capacityBlocker);
                    Agent another = missions[joiner].Agents.First();
                    missions[joiner].DeleteAgent(another);
                    var spawner = (IPuppetSpawner)AccessTools.Field(typeof(CoopBattleController), "puppetSpawner")
                        .GetValue(controllers[joiner]);
                    spawner.DrainPendingPuppets();
                });
            }

            joiner.Call(() =>
            {
                var registry = joiner.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
                Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
                Assert.Equal(bufferAcrossRiderDeparture ? "C" : "B", rider.CurrentAuthority);
                Assert.Equal(bufferAcrossRiderDeparture ? 4 : 3, rider.AuthorityRevision);
                Assert.Equal("A", horse.CurrentAuthority);
                Assert.Equal("A", horse.OriginalOwner);
                Assert.Equal("A", horse.MovementScopeId);
                Assert.Equal(7, horse.AuthorityRevision);
            });

            if (!bufferAcrossRiderDeparture)
            {
                riderOwner.Call(() =>
                {
                    var registry = riderOwner.Resolve<INetworkAgentRegistry>();
                    Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
                    rider.Agent.MountAgent = null;
                    riderOwner.Resolve<MockBattleNetwork>().Send("C",
                        new MovementPacket(new[] { riderId }, new[] { new AgentData(rider.Agent) }, "B", new long[] { 3 }));
                });
            }
            else
            {
                joiner.Call(() =>
                {
                    Assert.True(joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out var rider));
                    rider.Agent.MountAgent = null;
                });
            }
            joiner.Call(() =>
            {
                Assert.True(joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out var horse));
                Assert.Null(horse.Agent.RiderAgent);
                Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                mirror.MovementDirection = Vec2.Zero;
            });
            horseOwner.Call(() =>
            {
                var registry = horseOwner.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
                Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
                rider.Agent.MountAgent = null;
                Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                mirror.MovementDirection = new Vec2(1f, 0f);
                horseOwner.Resolve<MockBattleNetwork>().Send("C",
                    new MountMovementPacket(new[] { horseId }, new[] { new AgentMountData(horse.Agent, horseId) },
                        "A", new long[] { 7 }));
            });
            joiner.Call(() =>
            {
                Assert.True(joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out var horse));
                Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                Assert.Equal(new Vec2(1f, 0f), mirror.MovementDirection);
            });
        }
        finally
        {
            foreach (var instance in clients)
                instance.Call(BattleSpawnGate.EndBattle);
        }
    }
}
