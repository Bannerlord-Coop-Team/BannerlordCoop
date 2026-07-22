using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleSpawnReplayReconciliationTests : MissionTestEnvironment
{
    private const string OldHost = "old-host";
    private const string NewHost = "new-host";
    private const string Observer = "observer";
    private const int TroopSeed = 194900;

    public BattleSpawnReplayReconciliationTests(ITestOutputHelper output)
        : base(output, numClients: 3) { }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HostReplayOrders_ConvergeAtCapacity_AndOldTerminalIdTargetsWinner(
        bool successorFirst,
        bool isDeath)
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle(OldHost, NewHost, Observer);
        var observer = Clients.Skip(2).First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var mapEventPartyId = AddAiBattleParty(mapEventId);
        var oldAgentId = Guid.NewGuid();
        var newAgentId = Guid.NewGuid();
        var postTerminalAgentId = Guid.NewGuid();
        Agent winningAgent = null;

        try
        {
            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                mock.PlayerTeam = mock.AttackerTeam;
                var controller = observer.Resolve<CoopBattleController>();
                var broker = observer.Resolve<IMessageBroker>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var hosts = observer.Resolve<IBattleHostRegistry>();

                Assert.True(controller.Session.TryBegin(mapEventId));
                BattleSpawnGate.BeginBattle(mapEventId, 1);
                AssignBattleHost(broker, hosts, mapEventId, OldHost, new[] { NewHost }, 1);

                var oldRecord = CreateRecord(oldAgentId, characterId, mapEventPartyId, OldHost);
                var newRecord = CreateRecord(newAgentId, characterId, mapEventPartyId, NewHost);

                if (successorFirst)
                {
                    broker.Publish(this, new MissionPeerDisconnected(OldHost, mapEventId));
                    AssignBattleHost(broker, hosts, mapEventId, NewHost, Array.Empty<string>(), 2);
                    PublishSpawn(broker, newRecord);
                    PublishSpawn(broker, oldRecord);
                }
                else
                {
                    PublishSpawn(broker, oldRecord);
                    AssignBattleHost(broker, hosts, mapEventId, NewHost, Array.Empty<string>(), 2);
                    broker.Publish(this, new MissionPeerDisconnected(OldHost, mapEventId));
                    PublishSpawn(broker, newRecord);
                }

                Assert.False(registry.TryGetAgentInfo(oldAgentId, out _));
                Assert.True(registry.TryGetAgentInfo(newAgentId, out var winner));
                Assert.Equal(NewHost, winner.CurrentAuthority);
                Assert.Single(mock.Agents, agent => agent.IsActive() && !agent.IsMount);
                winningAgent = winner.Agent;

                if (isDeath)
                {
                    broker.Publish(this, new NetworkBattleAgentDied(
                        oldAgentId,
                        wounded: false,
                        Guid.Empty,
                        inflictedDamage: 100,
                        BoneBodyPartType.Head,
                        deathAction: 456));
                }
                else
                {
                    broker.Publish(this, new NetworkBattleAgentRouted(
                        oldAgentId,
                        hideMount: true,
                        isAdministrativeRemoval: false));
                }

                Assert.False(registry.TryGetAgentInfo(newAgentId, out _));
                PublishSpawn(broker, CreateRecord(
                    postTerminalAgentId,
                    characterId,
                    mapEventPartyId,
                    NewHost));
                Assert.False(registry.TryGetAgentInfo(postTerminalAgentId, out _));
                GC.KeepAlive(controller);
            });

            Assert.True(AgentMirror.TryGet(winningAgent, out var winningMirror));
            Assert.False(winningMirror.IsActive);
            Assert.Equal(isDeath, winningMirror.WasKilled);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    public void SuccessorReplayBeforeAssignment_BuffersThenWinsAfterMigration()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle(OldHost, NewHost, Observer);
        var observer = Clients.Skip(2).First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var mapEventPartyId = AddAiBattleParty(mapEventId);
        var oldAgentId = Guid.NewGuid();
        var newAgentId = Guid.NewGuid();

        try
        {
            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                mock.PlayerTeam = mock.AttackerTeam;
                var controller = observer.Resolve<CoopBattleController>();
                var broker = observer.Resolve<IMessageBroker>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var hosts = observer.Resolve<IBattleHostRegistry>();

                Assert.True(controller.Session.TryBegin(mapEventId));
                BattleSpawnGate.BeginBattle(mapEventId, 1);
                AssignBattleHost(broker, hosts, mapEventId, OldHost, new[] { NewHost }, 1);

                PublishSpawn(broker, CreateRecord(
                    oldAgentId,
                    characterId,
                    mapEventPartyId,
                    OldHost));
                PublishSpawn(broker, CreateRecord(
                    newAgentId,
                    characterId,
                    mapEventPartyId,
                    NewHost));

                Assert.True(registry.TryGetAgentInfo(oldAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(newAgentId, out _));
                Assert.Single(mock.Agents, agent => agent.IsActive() && !agent.IsMount);

                AssignBattleHost(broker, hosts, mapEventId, NewHost, Array.Empty<string>(), 2);
                broker.Publish(this, new MissionPeerDisconnected(OldHost, mapEventId));
                controller.OnMissionTick(0f);

                Assert.False(registry.TryGetAgentInfo(oldAgentId, out _));
                Assert.True(registry.TryGetAgentInfo(newAgentId, out var winner));
                Assert.Equal(NewHost, winner.CurrentAuthority);
                Assert.Single(mock.Agents, agent => agent.IsActive() && !agent.IsMount);
                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    public void DelayedAdministrativeRoutForSupersededHost_DoesNotRemoveSuccessor()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle(OldHost, NewHost, Observer);
        var observer = Clients.Skip(2).First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var mapEventPartyId = AddAiBattleParty(mapEventId);
        var oldAgentId = Guid.NewGuid();
        var newAgentId = Guid.NewGuid();
        Agent winningAgent = null;

        try
        {
            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                mock.PlayerTeam = mock.AttackerTeam;
                var controller = observer.Resolve<CoopBattleController>();
                var broker = observer.Resolve<IMessageBroker>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var hosts = observer.Resolve<IBattleHostRegistry>();

                Assert.True(controller.Session.TryBegin(mapEventId));
                BattleSpawnGate.BeginBattle(mapEventId, 1);
                AssignBattleHost(broker, hosts, mapEventId, OldHost, new[] { NewHost }, 1);

                PublishSpawn(broker, CreateRecord(
                    oldAgentId,
                    characterId,
                    mapEventPartyId,
                    OldHost));
                AssignBattleHost(broker, hosts, mapEventId, NewHost, Array.Empty<string>(), 2);
                broker.Publish(this, new MissionPeerDisconnected(OldHost, mapEventId));
                PublishSpawn(broker, CreateRecord(
                    newAgentId,
                    characterId,
                    mapEventPartyId,
                    NewHost));

                Assert.False(registry.TryGetAgentInfo(oldAgentId, out _));
                Assert.True(registry.TryGetAgentInfo(newAgentId, out var winner));
                winningAgent = winner.Agent;

                broker.Publish(this, new NetworkBattleAgentRouted(
                    oldAgentId,
                    hideMount: true,
                    isAdministrativeRemoval: true));

                Assert.True(registry.TryGetAgentInfo(newAgentId, out _));
                Assert.Single(mock.Agents, agent => agent.IsActive() && !agent.IsMount);
                GC.KeepAlive(controller);
            });

            Assert.True(AgentMirror.TryGet(winningAgent, out var winningMirror));
            Assert.True(winningMirror.IsActive);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MountedHostReplayOrders_ReplaceRiderAndMount_AsOneStablePair(bool successorFirst)
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle(OldHost, NewHost, Observer);
        var observer = Clients.Skip(2).First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var mapEventPartyId = AddAiBattleParty(mapEventId);
        var oldAgentId = Guid.NewGuid();
        var oldMountId = Guid.NewGuid();
        var newAgentId = Guid.NewGuid();
        var newMountId = Guid.NewGuid();
        Agent newRider = null;
        Agent newMount = null;

        try
        {
            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                mock.PlayerTeam = mock.AttackerTeam;
                mock.SpawnMounted = true;
                var controller = observer.Resolve<CoopBattleController>();
                var broker = observer.Resolve<IMessageBroker>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                var hosts = observer.Resolve<IBattleHostRegistry>();

                Assert.True(controller.Session.TryBegin(mapEventId));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);
                AssignBattleHost(broker, hosts, mapEventId, OldHost, new[] { NewHost }, 1);

                var oldRecord = CreateRecord(
                    oldAgentId,
                    characterId,
                    mapEventPartyId,
                    OldHost,
                    oldMountId);
                var newRecord = CreateRecord(
                    newAgentId,
                    characterId,
                    mapEventPartyId,
                    NewHost,
                    newMountId);

                if (successorFirst)
                {
                    broker.Publish(this, new MissionPeerDisconnected(OldHost, mapEventId));
                    AssignBattleHost(broker, hosts, mapEventId, NewHost, Array.Empty<string>(), 2);
                    PublishSpawn(broker, newRecord);
                    PublishSpawn(broker, oldRecord);
                }
                else
                {
                    PublishSpawn(broker, oldRecord);
                    AssignBattleHost(broker, hosts, mapEventId, NewHost, Array.Empty<string>(), 2);
                    broker.Publish(this, new MissionPeerDisconnected(OldHost, mapEventId));
                    PublishSpawn(broker, newRecord);
                }

                Assert.False(registry.TryGetAgentInfo(oldAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(oldMountId, out _));
                Assert.True(registry.TryGetAgentInfo(newAgentId, out var riderInfo));
                Assert.True(registry.TryGetAgentInfo(newMountId, out var mountInfo));
                Assert.Single(mock.Agents, agent => agent.IsActive() && !agent.IsMount);
                Assert.Single(mock.Agents, agent => agent.IsActive() && agent.IsMount);
                newRider = riderInfo.Agent;
                newMount = mountInfo.Agent;

                broker.Publish(this, new NetworkBattleAgentDied(
                    oldMountId,
                    wounded: false,
                    Guid.Empty,
                    inflictedDamage: 100,
                    BoneBodyPartType.Head,
                    deathAction: 456));

                Assert.True(registry.TryGetAgentInfo(newAgentId, out _));
                Assert.False(registry.TryGetAgentInfo(newMountId, out _));
                GC.KeepAlive(controller);
            });

            Assert.True(AgentMirror.TryGet(newRider, out var riderMirror));
            Assert.True(riderMirror.IsActive);
            Assert.True(AgentMirror.TryGet(newMount, out var mountMirror));
            Assert.False(mountMirror.IsActive);
            Assert.True(mountMirror.WasKilled);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    private string AddAiBattleParty(string mapEventId)
    {
        var mobilePartyId = CreateRegisteredObject<MobileParty>();
        string mapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));

            mobileParty.Party.MapEventSide = mapEvent.DefenderSide;
            var mapEventParty = mapEvent.DefenderSide.Parties.Last(party => party.Party == mobileParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out mapEventPartyId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId;
    }

    private static BattleAgentSpawnData CreateRecord(
        Guid agentId,
        string characterId,
        string mapEventPartyId,
        string ownerControllerId,
        Guid mountAgentId = default)
    {
        return new BattleAgentSpawnData(
            agentId,
            characterId,
            default,
            BattleSideEnum.Defender,
            100f,
            ownerControllerId,
            mapEventPartyId,
            TroopSeed,
            new Equipment(),
            new BodyProperties(),
            new MissionEquipmentData(new()),
            mountAgentId);
    }

    private static void PublishSpawn(IMessageBroker broker, BattleAgentSpawnData record)
        => broker.Publish(typeof(BattleSpawnReplayReconciliationTests),
            new NetworkSpawnBattleAgents(new[] { record }));

    private static void AssignBattleHost(
        IMessageBroker broker,
        IBattleHostRegistry hosts,
        string mapEventId,
        string hostControllerId,
        string[] successorControllerIds,
        int epoch)
    {
        hosts.Set(
            mapEventId,
            new BattleHostAssignment(hostControllerId, successorControllerIds, epoch));
        broker.Publish(
            typeof(BattleSpawnReplayReconciliationTests),
            new NetworkBattleHostAssigned(
                mapEventId,
                hostControllerId,
                successorControllerIds,
                epoch));
    }
}
