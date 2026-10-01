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
using Missions.Services.Network;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
        RunCatchUp("C", bufferAcrossRiderDeparture ? "B" : null, disconnected: true);
    }

    [Theory]
    [InlineData("C", "A", false)]
    [InlineData("C", "A", true)]
    [InlineData("A", "A", false)]
    [InlineData("A", "A", true)]
    [InlineData("B", "B", true)]
    public void BufferedCatchUp_DepartureMatchesPopulatedPeers(
        string initialHost, string departingController, bool disconnected)
    {
        RunCatchUp(initialHost, departingController, disconnected);
    }

    public static IEnumerable<object[]> DelayedHorseDepartureCases()
    {
        foreach (string initialHost in new[] { "A", "C" })
            foreach (bool disconnected in new[] { false, true })
                foreach (bool refreshedFirst in new[] { false, true })
                    foreach (bool atCapacity in new[] { false, true })
                        yield return new object[] { initialHost, disconnected, refreshedFirst, atCapacity };
    }

    [Theory]
    [MemberData(nameof(DelayedHorseDepartureCases))]
    public void DelayedCatchUp_HorseAuthorityConvergesAfterOwnerReturns(
        string initialHost, bool disconnected, bool refreshedFirst, bool atCapacity)
    {
        RunCatchUp(initialHost, "A", disconnected, refreshedFirst, atCapacity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityRefresh_BeforeLocalDeparture_DoesNotAdvanceRevisionTwice(bool atCapacity)
    {
        RunCatchUp("C", "A", disconnected: false, refreshedFirst: true, atCapacity: atCapacity,
            refreshBeforeDeparture: true);
    }

    private void RunCatchUp(string initialHost, string departingController, bool disconnected,
        bool? refreshedFirst = null, bool atCapacity = false, bool refreshBeforeDeparture = false)
    {
        bool hasDeparture = departingController != null;
        bool delayedCatchUp = refreshedFirst.HasValue;
        bool bufferAcrossDeparture = hasDeparture && (!delayedCatchUp || atCapacity);
        string expectedRiderOwner = departingController == "B" ? "C" : "B";
        long expectedRiderRevision = departingController == "B" ? 4 : 3;
        string expectedHorseOwner = departingController == "A"
            ? (!disconnected && initialHost != "A" ? "B" : "C")
            : "A";
        long expectedHorseRevision = departingController == "A" ? 8 : 7;
        using var engine = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B", "C");
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
                        mapEventId, new BattleHostAssignment(initialHost, new[] { "A", "B", "C" }, 1));
                    missions.Add(instance, mission);
                    controllers.Add(instance, controller);
                });
            }
            foreach (var instance in clients)
                foreach (string id in new[] { "A", "B", "C" })
                    instance.Call(() => instance.Resolve<IMessageBroker>().Publish(
                        this, new NetworkMissionPeerEntered(id, mapEventId)));

            riderOwner.Call(() => controllers[riderOwner].Deployment.OnLocalDeploymentFinished());
            foreach (var instance in new[] { horseOwner, riderOwner })
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject(partyIds[1], out MobileParty party));
                    var mapEventParty = party.Party.MapEventSide.Parties.Single(value => value.Party == party.Party);
                    Assert.True(instance.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));
                    missions[instance].SpawnMounted = true;
                    var record = new BattleAgentSpawnData(
                        riderId, characterId, Vec3.Zero, BattleSideEnum.Defender, 100f, "B",
                        mapEventPartyId, 7, new Equipment(), default, null,
                        mountAgentId: horseId, movementId: 1, mountMovementId: 2,
                        mountOriginalOwnerControllerId: "A", mountMovementScopeId: "A",
                        authorityRevision: 3, mountAuthorityRevision: 7, mountOwnerControllerId: "A");
                    instance.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { record }));
                    Assert.True(instance.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out _));
                    Assert.True(instance.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out _));
                });
            }

            Agent capacityBlocker = null;
            joiner.Call(() =>
            {
                if (bufferAcrossDeparture)
                {
                    for (int i = 0; i < 2000; i++)
                        capacityBlocker = missions[joiner].SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                }
                missions[joiner].SpawnMounted = true;
            });
            var riderNetwork = riderOwner.Resolve<MockBattleNetwork>();
            riderNetwork.NetworkSentMessages.Clear();
            riderNetwork.RouteMessages = !delayedCatchUp;
            NetworkSpawnBattleAgents oldBatch = null;
            NetworkSpawnBattleAgents refreshedBatch = null;
            riderOwner.Call(() => riderOwner.Resolve<IMessageBroker>().Publish(
                this, new NetworkMissionPeerEntered("C", mapEventId)));

            riderOwner.Call(() =>
            {
                var batch = Assert.Single(riderOwner.Resolve<MockBattleNetwork>().NetworkSentMessages
                    .GetMessages<NetworkSpawnBattleAgents>());
                Assert.Equal(SpawnBatchPurpose.CatchUp, batch.Purpose);
                oldBatch = ProtoBuf.Serializer.DeepClone(batch);
                var wire = oldBatch;
                Assert.True(riderOwner.Resolve<IBattleAgentSpawnBatchCodec>()
                    .TryDecode(wire, out var records));
                var record = Assert.Single(records);
                Assert.Equal("B", record.OwnerControllerId);
                Assert.Equal("A", record.MountOwnerControllerId);
                Assert.Equal(3, record.AuthorityRevision);
                Assert.Equal(7, record.MountAuthorityRevision);
            });

            var populatedPeer = departingController == "B" ? horseOwner : riderOwner;
            if (hasDeparture)
            {
                riderNetwork.RouteMessages = false;
                riderNetwork.NetworkSentMessages.Clear();
                joiner.Call(() => Assert.False(joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out _)));
                foreach (var instance in new[] { populatedPeer, joiner })
                {
                    if (refreshBeforeDeparture && instance == joiner) continue;
                    instance.Call(() =>
                    {
                        var broker = instance.Resolve<IMessageBroker>();
                        if (disconnected)
                            broker.Publish(this, new MissionPeerDisconnected(departingController, mapEventId));
                        else
                        {
                            broker.Publish(this, new MissionPeerLeft(departingController, mapEventId));
                            broker.Publish(this, new MissionPeerLeft(departingController, mapEventId));
                        }
                        if (initialHost == departingController)
                            broker.Publish(this, new NetworkBattleHostAssigned(
                                mapEventId, "C", Array.Empty<string>(), epoch: 2));
                    });
                }
                if (delayedCatchUp)
                {
                    riderOwner.Call(() =>
                    {
                        var batches = riderNetwork.NetworkSentMessages.GetMessages<NetworkSpawnBattleAgents>();
                        Assert.NotEmpty(batches);
                        foreach (var batch in batches)
                        {
                            var wire = ProtoBuf.Serializer.DeepClone(batch);
                            Assert.True(riderOwner.Resolve<IBattleAgentSpawnBatchCodec>().TryDecode(wire, out var records));
                            var record = records.SingleOrDefault(value => value.AgentId == riderId);
                            if (record == null || record.MountAuthorityRevision != expectedHorseRevision) continue;
                            Assert.Equal("B", record.OwnerControllerId);
                            Assert.Equal(3, record.AuthorityRevision);
                            Assert.Equal(expectedHorseOwner, record.MountOwnerControllerId);
                            refreshedBatch = wire;
                        }
                        Assert.NotNull(refreshedBatch);
                    });
                }
                if (refreshBeforeDeparture)
                {
                    riderNetwork.RouteMessages = true;
                    riderOwner.Call(() => riderNetwork.Send("C", refreshedBatch));
                    riderNetwork.RouteMessages = false;
                    joiner.Call(() =>
                    {
                        var registry = joiner.Resolve<INetworkAgentRegistry>();
                        Assert.Equal(!atCapacity, registry.TryGetAgentInfo(horseId, out var horse));
                        if (!atCapacity)
                        {
                            Assert.Equal("B", horse.CurrentAuthority);
                            Assert.Equal(8, horse.AuthorityRevision);
                        }
                        joiner.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", mapEventId));
                    });
                }
                foreach (var instance in new[] { populatedPeer, joiner })
                {
                    instance.Call(() =>
                    {
                        var broker = instance.Resolve<IMessageBroker>();
                        // Returning players cannot reclaim a horse or rider that already transferred.
                        broker.Publish(this, new NetworkMissionPeerEntered(departingController, mapEventId));
                        if (!delayedCatchUp)
                            broker.Publish(this, new MissionPeerLeft(departingController, mapEventId));
                    });
                }
                riderNetwork.RouteMessages = true;
                if (delayedCatchUp)
                {
                    horseOwner.Call(() =>
                    {
                        var network = horseOwner.Resolve<MockBattleNetwork>();
                        network.Stop();
                        network.Start();
                        network.ConnectToInstance(mapEventId);
                    });
                    riderOwner.Call(() => riderNetwork.Send(
                        "C", refreshedFirst.Value ? refreshedBatch : oldBatch));
                    joiner.Call(() => Assert.Equal(!atCapacity,
                        joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out _)));
                    riderOwner.Call(() => riderNetwork.Send(
                        "C", refreshedFirst.Value ? oldBatch : refreshedBatch));
                }
                if (bufferAcrossDeparture)
                {
                    joiner.Call(() =>
                    {
                        Assert.False(joiner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out _));
                        missions[joiner].DeleteAgent(capacityBlocker);
                        for (int i = 0; i < (delayedCatchUp ? 3 : 1); i++)
                            missions[joiner].DeleteAgent(missions[joiner].Agents.First());
                        var spawner = (IPuppetSpawner)AccessTools.Field(typeof(CoopBattleController), "puppetSpawner")
                            .GetValue(controllers[joiner]);
                        spawner.DrainPendingPuppets();
                    });
                }
            }

            joiner.Call(() =>
            {
                var registry = joiner.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
                Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
                Assert.Equal(expectedRiderOwner, rider.CurrentAuthority);
                Assert.Equal(expectedRiderRevision, rider.AuthorityRevision);
                Assert.Equal(expectedHorseOwner, horse.CurrentAuthority);
                Assert.Equal("A", horse.OriginalOwner);
                Assert.Equal("A", horse.MovementScopeId);
                Assert.Equal(expectedHorseRevision, horse.AuthorityRevision);
            });

            populatedPeer.Call(() =>
            {
                var registry = populatedPeer.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
                Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
                Assert.Equal(expectedRiderOwner, rider.CurrentAuthority);
                Assert.Equal(expectedRiderRevision, rider.AuthorityRevision);
                Assert.Equal(expectedHorseOwner, horse.CurrentAuthority);
                Assert.Equal(expectedHorseRevision, horse.AuthorityRevision);
            });

            if (delayedCatchUp)
            {
                AssertConflictingSnapshotsRejected(riderOwner, joiner, refreshedBatch, riderId, horseId, expectedHorseOwner);
                Guid returningRiderId = Guid.NewGuid();
                Guid returningHorseId = Guid.NewGuid();
                horseOwner.Call(() =>
                {
                    var controller = controllers[horseOwner];
                    controller.Deployment.OnLocalDeploymentFinished();
                    Assert.True(horseOwner.ObjectManager.TryGetObject(partyIds[0], out MobileParty party));
                    var mapEventParty = party.Party.MapEventSide.Parties.Single(value => value.Party == party.Party);
                    Assert.True(horseOwner.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));
                    var record = new BattleAgentSpawnData(
                        returningRiderId, characterId, Vec3.Zero, BattleSideEnum.Attacker, 100f, "A",
                        mapEventPartyId, 8, new Equipment(), default, null,
                        mountAgentId: returningHorseId, movementId: 3, mountMovementId: 4,
                        authorityRevision: 0, mountAuthorityRevision: 0);
                    horseOwner.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { record }));
                    horseOwner.Resolve<IMessageBroker>().Publish(this, new NetworkMissionPeerEntered("C", mapEventId));
                });
                joiner.Call(() =>
                {
                    var registry = joiner.Resolve<INetworkAgentRegistry>();
                    foreach (Guid id in new[] { returningRiderId, returningHorseId })
                    {
                        Assert.True(registry.TryGetAgentInfo(id, out var info));
                        Assert.Equal("A", info.CurrentAuthority);
                        Assert.Equal(0, info.AuthorityRevision);
                    }
                });
            }

            var movementSender = expectedHorseOwner == "A" ? horseOwner
                : expectedHorseOwner == "B" ? riderOwner : joiner;
            var movementReceiver = movementSender == joiner ? populatedPeer : joiner;
            var riderAuthority = expectedRiderOwner == "B" ? riderOwner : joiner;
            riderAuthority.Call(() =>
            {
                Assert.True(riderAuthority.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(riderId, out var rider));
                rider.Agent.MountAgent = null;
                var packet = new MovementPacket(new[] { riderId }, new[] { new AgentData(rider.Agent) },
                    expectedRiderOwner, new[] { expectedRiderRevision });
                foreach (var recipient in new[] { movementSender, movementReceiver }.Distinct())
                {
                    if (recipient == riderAuthority) continue;
                    riderAuthority.Resolve<MockBattleNetwork>().Send(
                        recipient == horseOwner ? "A" : recipient == riderOwner ? "B" : "C", packet);
                }
            });
            foreach (var instance in new[] { movementSender, movementReceiver })
            {
                instance.Call(() =>
                {
                    var registry = instance.Resolve<INetworkAgentRegistry>();
                    Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
                    Assert.Null(horse.Agent.RiderAgent);
                    Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                    mirror.MovementDirection = Vec2.Zero;
                });
            }
            movementSender.Call(() =>
            {
                Assert.True(movementSender.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out var horse));
                Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                mirror.MovementDirection = new Vec2(1f, 0f);
                movementSender.Resolve<MockBattleNetwork>().Send(movementReceiver == joiner ? "C"
                    : departingController == "B" ? "A" : "B",
                    new MountMovementPacket(new[] { horseId }, new[] { new AgentMountData(horse.Agent, horseId) },
                        expectedHorseOwner, new[] { expectedHorseRevision }));
            });
            movementReceiver.Call(() =>
            {
                Assert.True(movementReceiver.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out var horse));
                Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                Assert.Equal(new Vec2(1f, 0f), mirror.MovementDirection);
            });
            if (delayedCatchUp)
            {
                movementReceiver.Call(() =>
                {
                    Assert.Contains("A", movementReceiver.Resolve<IMissionContext>().ControllersInMission);
                    Assert.True(movementReceiver.Resolve<IMissionContext>().TryGetPeer("A", out var peer));
                    Assert.Same(horseOwner.Resolve<MockBattleNetwork>().NetPeer, peer);
                });
                horseOwner.Call(() =>
                {
                    Assert.True(horseOwner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out var staleHorse));
                    Assert.True(AgentMirror.TryGet(staleHorse.Agent, out var mirror));
                    mirror.MovementDirection = new Vec2(0f, 1f);
                    horseOwner.Resolve<MockBattleNetwork>().Send(movementReceiver == joiner ? "C" : "B",
                        new MountMovementPacket(new[] { horseId }, new[] { new AgentMountData(staleHorse.Agent, horseId) },
                            "A", new[] { 7L }));
                });
                movementReceiver.Call(() =>
                {
                    Assert.True(movementReceiver.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(horseId, out var horse));
                    Assert.Equal(expectedHorseOwner, horse.CurrentAuthority);
                    Assert.Equal(8, horse.AuthorityRevision);
                    Assert.True(AgentMirror.TryGet(horse.Agent, out var mirror));
                    Assert.Equal(new Vec2(1f, 0f), mirror.MovementDirection);
                });
            }
        }
        finally
        {
            foreach (var instance in clients)
                instance.Call(BattleSpawnGate.EndBattle);
        }
    }

    private void AssertConflictingSnapshotsRejected(EnvironmentInstance sender, EnvironmentInstance receiver,
        NetworkSpawnBattleAgents currentBatch, Guid riderId, Guid horseId, string expectedHorseOwner)
    {
        BattleAgentSpawnData current = null;
        sender.Call(() =>
        {
            Assert.True(sender.Resolve<IBattleAgentSpawnBatchCodec>().TryDecode(currentBatch, out var records));
            current = records.Single(value => value.AgentId == riderId);
        });
        Agent riderAgent = null;
        Agent horseAgent = null;
        AgentControllerType riderController = default;
        AgentControllerType horseController = default;
        receiver.Call(() =>
        {
            var registry = receiver.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
            Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
            riderAgent = rider.Agent;
            horseAgent = horse.Agent;
            riderController = rider.Agent.Controller;
            horseController = horse.Agent.Controller;
        });
        foreach (bool conflictingRider in new[] { true, false })
        {
            sender.Call(() =>
            {
                var conflict = new BattleAgentSpawnData(
                    current.AgentId, current.CharacterId, current.Position, current.Side, current.Health,
                    conflictingRider ? "A" : current.OwnerControllerId, current.MapEventPartyId, current.TroopSeed,
                    current.SpawnEquipment, current.BodyProperties, current.MissionEquipmentData,
                    current.MountAgentId, current.FormationIndex, current.MovementId, current.MountMovementId,
                    current.OriginalOwnerControllerId, current.HasCurrentEquipment ? current.CurrentEquipment : null,
                    current.MovementScopeId, current.MountOriginalOwnerControllerId, current.MountMovementScopeId,
                    current.IsRunningAway, current.AuthorityRevision, current.MountAuthorityRevision,
                    conflictingRider ? current.MountOwnerControllerId : "A");
                var batch = Assert.Single(sender.Resolve<IBattleAgentSpawnBatchCodec>()
                    .Encode(new[] { conflict }, SpawnBatchPurpose.CatchUp));
                sender.Resolve<MockBattleNetwork>().Send("C", batch);
            });
            receiver.Call(() =>
            {
                var registry = receiver.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(riderId, out var rider));
                Assert.True(registry.TryGetAgentInfo(horseId, out var horse));
                Assert.Equal("B", rider.CurrentAuthority);
                Assert.Equal(3, rider.AuthorityRevision);
                Assert.Equal(expectedHorseOwner, horse.CurrentAuthority);
                Assert.Equal(8, horse.AuthorityRevision);
                Assert.Same(riderAgent, rider.Agent);
                Assert.Same(horseAgent, horse.Agent);
                Assert.Equal(riderController, rider.Agent.Controller);
                Assert.Equal(horseController, horse.Agent.Controller);
                Assert.Same(horse.Agent, rider.Agent.MountAgent);
                Assert.Same(rider.Agent, horse.Agent.RiderAgent);
            });
        }
    }
}
