using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattlePuppetTeamOwnershipTests : MissionTestEnvironment
{
    public BattlePuppetTeamOwnershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task WireSpawn_EquipmentCloneRemainsTransientThroughPuppetSpawn()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "remote");
        var client = Clients.First();
        var agentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();
        var capturedLogs = new List<string>();
        var logGate = new object();

        void CaptureLog(string message)
        {
            lock (logGate) capturedLogs.Add(message);
        }

        bool ContainsEquipmentDiagnostic()
        {
            lock (logGate)
            {
                return capturedLogs.Any(message =>
                    (message.Contains("Client created managed") &&
                     message.Contains("TaleWorlds.Core.Equipment")) ||
                    (message.Contains("Client updated managed") &&
                     message.Contains("_equipmentType")));
            }
        }

        NetworkSpawnBattleAgents wire = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var remoteParty));
            var mapEventParty = remoteParty.MapEvent.DefenderSide.Parties
                .Single(party => party.Party == remoteParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));

            var record = new BattleAgentSpawnData(
                agentId,
                characterId,
                default,
                BattleSideEnum.Defender,
                100f,
                "remote",
                mapEventPartyId,
                1,
                new Equipment(Equipment.EquipmentType.Battle),
                new BodyProperties(),
                missionEquipmentData: null);
            NetworkSpawnBattleAgents encoded = new BattleAgentSpawnBatchCodec()
                .Encode(new[] { record }, SpawnBatchPurpose.Initial)
                .Single();
            wire = ProtoBuf.Serializer.DeepClone(encoded);
        });

        PuppetSpawner spawner = null;
        OutputSinkManager.AddLogCallback(CaptureLog);
        try
        {
            MockMission mission = null;
            client.Call(() =>
            {
                mission = fixture.CreateMission(client);
                mission.PlayerTeam = mission.AttackerTeam;
                mission.AddTeam(BattleSideEnum.Defender);
                mission.CloneSpawnEquipmentOnSpawn = true;

                var deployment = new Mock<IBattleDeploymentCoordinator>();
                deployment.SetupGet(value => value.IsCommitted).Returns(true);
                spawner = new PuppetSpawner(
                    client.Resolve<IMessageBroker>(),
                    client.ObjectManager,
                    client.Resolve<GameInterface.Services.Players.IPlayerManager>(),
                    client.Resolve<ICoopMissionComponent>(),
                    Mock.Of<IBattleSession>(),
                    new CasualtyAttributionMap(),
                    deployment.Object,
                    Mock.Of<IAgentFormationAssigner>(),
                    new BattleAgentBudget(),
                    client.Resolve<IMissionWeaponDataMapper>());
            });

            GameThread.Instance.MarkGameThread();
            await Task.Run(() => client.SimulateMessage(this, wire, markGameThread: false));
            Assert.True(client.PendingGameThreadActionCount > 0);
            client.PumpGameThread();

            client.Call(() =>
            {
                var registry = client.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(agentId, out var agentInfo));
                Assert.True(AgentMirror.TryGet(agentInfo.Agent, out var mirror));
                var origin = Assert.IsType<CoopAgentOrigin>(agentInfo.Agent.Origin);
                Assert.Equal(DecodeSpawnBatch(client, wire).Single().MapEventPartyId, origin.MapEventPartyId);
                Assert.NotNull(mirror.SpawnEquipment);
                Assert.False(client.ObjectManager.TryGetId(mirror.SpawnEquipment, out _));
            });
            Assert.False(ContainsEquipmentDiagnostic());

            client.Call(() =>
            {
                var unsupportedEquipment = new Equipment(Equipment.EquipmentType.Battle);
                Assert.False(client.ObjectManager.TryGetId(unsupportedEquipment, out _));
            });
            Assert.True(ContainsEquipmentDiagnostic());
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(CaptureLog);
            if (spawner != null) client.Call(spawner.Dispose);
        }
    }

    [Fact]
    [Trait("Requirement", "BR-022")]
    public void AlliedPlayerJoinsHost_RemotePartyStaysOffHostsPlayerTeam()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("host", "enemy", "ally");
        var host = Clients.First();
        var ownAgentId = Guid.NewGuid();
        var alliedAgentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        const string harmonyId = "e2e.coop-player-ally-team";
        var harmony = new Harmony(harmonyId);
        try
        {
            var patchType = typeof(BattleSpawnGate).Assembly.GetType(
                "GameInterface.Services.MapEvents.Patches.CoopPlayerAllyTeamPatch");
            Assert.NotNull(patchType);
            harmony.CreateClassProcessor(patchType).Patch();

            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var ownParty));
                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var enemyParty));
                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[2], out var alliedParty));

                mock.PlayerTeam = mock.AttackerTeam;
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(
                    mapEventId,
                    new BattleHostAssignment("host", new[] { "enemy", "ally" }));
                BattleSpawnGate.BeginBattle(mapEventId);

                var mapEventParties = ownParty.MapEvent.AttackerSide.Parties;
                var ownMapEventParty = mapEventParties.Single(party => party.Party == ownParty.Party);
                var alliedMapEventParty = mapEventParties.Single(party => party.Party == alliedParty.Party);
                Assert.True(host.ObjectManager.TryGetId(ownMapEventParty, out var ownMapEventPartyId));
                Assert.True(host.ObjectManager.TryGetId(alliedMapEventParty, out var alliedMapEventPartyId));

                var equipment = new Equipment();
                var missionEquipment = new MissionEquipmentData(new());
                var ownRecord = new BattleAgentSpawnData(
                    ownAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                    "host", ownMapEventPartyId, 1, equipment, new BodyProperties(), missionEquipment);
                var alliedRecord = new BattleAgentSpawnData(
                    alliedAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                    "ally", alliedMapEventPartyId, 2, equipment, new BodyProperties(), missionEquipment);

                var broker = host.Resolve<IMessageBroker>();
                broker.Publish(this, new NetworkSpawnBattleAgents(new[] { alliedRecord }));
                // The unsafe fallback used to spawn this record directly onto PlayerTeam.
                Assert.False(registry.TryGetAgentInfo(alliedAgentId, out _));
                Assert.Empty(mock.Agents);

                var combatants = new IBattleCombatant[] { ownParty.Party, enemyParty.Party };
                var combatantsLogic = new MissionCombatantsLogic(
                    combatants,
                    ownParty.Party,
                    enemyParty.Party,
                    ownParty.Party,
                    Mission.MissionTeamAITypeEnum.FieldBattle,
                    isPlayerSergeant: false)
                {
                    Mission = mock.Shell,
                };
                combatantsLogic.OnBehaviorInitialize();

                Assert.Same(mock.AttackerTeam, mock.PlayerTeam);
                Assert.NotNull(mock.AttackerAllyTeam);

                broker.Publish(this, new NetworkSpawnBattleAgents(new[] { ownRecord, alliedRecord }));

                Assert.True(registry.TryGetAgentInfo(ownAgentId, out var ownAgent));
                Assert.True(registry.TryGetAgentInfo(alliedAgentId, out var alliedAgent));
                Assert.True(AgentMirror.TryGet(ownAgent.Agent, out var ownMirror));
                Assert.True(AgentMirror.TryGet(alliedAgent.Agent, out var alliedMirror));

                Assert.Same(mock.PlayerTeam.Shell, ownMirror.Team);
                Assert.Same(mock.AttackerAllyTeam.Shell, alliedMirror.Team);
                Assert.NotSame(mock.PlayerTeam.Shell, alliedMirror.Team);
                Assert.Equal(AgentControllerType.AI, ownMirror.Controller);
                Assert.Equal(AgentControllerType.None, alliedMirror.Controller);
                Assert.True(registry.IsLocallyControlled(ownAgentId));
                Assert.False(registry.IsLocallyControlled(alliedAgentId));
                Assert.Equal("ally", alliedAgent.CurrentAuthority);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    [Trait("Requirement", "BR-023")]
    public void Deployment_RemoteNpcWaitsForTeamSetup_OwnTroopWaitsForCommit()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "enemy", "remote");
        var client = Clients.First();
        var ownAgentId = Guid.NewGuid();
        var remoteAgentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            mission.PlayerTeam = mission.AttackerTeam;
            mission.DeploymentInProgress = true;

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var ownParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[2], out var remoteParty));

            var ownMapEventParty = ownParty.MapEvent.AttackerSide.Parties
                .Single(party => party.Party == ownParty.Party);
            var remoteMapEventParty = ownParty.MapEvent.AttackerSide.Parties
                .Single(party => party.Party == remoteParty.Party);
            Assert.True(client.ObjectManager.TryGetId(ownMapEventParty, out var ownMapEventPartyId));
            Assert.True(client.ObjectManager.TryGetId(remoteMapEventParty, out var remoteMapEventPartyId));

            var session = new Mock<IBattleSession>();
            session.Setup(s => s.IsOwn(It.IsAny<string>()))
                .Returns((string controllerId) => controllerId == "local");
            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(false);

            using var spawner = new PuppetSpawner(
                client.Resolve<IMessageBroker>(),
                client.ObjectManager,
                client.Resolve<GameInterface.Services.Players.IPlayerManager>(),
                client.Resolve<ICoopMissionComponent>(),
                session.Object,
                new CasualtyAttributionMap(),
                deployment.Object,
                Mock.Of<IAgentFormationAssigner>(),
                new BattleAgentBudget(),
                client.Resolve<IMissionWeaponDataMapper>());

            var equipment = new Equipment();
            var missionEquipment = new MissionEquipmentData(new());
            var ownRecord = new BattleAgentSpawnData(
                ownAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                "local", ownMapEventPartyId, 1, equipment, new BodyProperties(), missionEquipment);
            var remoteRecord = new BattleAgentSpawnData(
                remoteAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                "host", remoteMapEventPartyId, 2, equipment, new BodyProperties(), missionEquipment);

            client.Resolve<IMessageBroker>().Publish(
                this, new NetworkSpawnBattleAgents(new[] { ownRecord, remoteRecord }));

            var registry = client.Resolve<INetworkAgentRegistry>();
            Assert.False(registry.TryGetAgentInfo(ownAgentId, out _));
            Assert.False(registry.TryGetAgentInfo(remoteAgentId, out _));

            mission.AddTeam(BattleSideEnum.Attacker);
            mission.AddTeam(BattleSideEnum.Attacker);
            spawner.DrainPendingPuppets();

            Assert.False(registry.TryGetAgentInfo(ownAgentId, out _));
            Assert.False(registry.TryGetAgentInfo(remoteAgentId, out _));

            mission.DeploymentController.TeamSetupOver = true;
            spawner.DrainPendingPuppets();

            Assert.False(registry.TryGetAgentInfo(ownAgentId, out _));
            Assert.True(registry.TryGetAgentInfo(remoteAgentId, out _));

            deployment.SetupGet(d => d.IsCommitted).Returns(true);
            spawner.DrainPendingPuppets();

            Assert.True(registry.TryGetAgentInfo(ownAgentId, out _));
        });
    }

    [Fact]
    public void ExplicitPartyId_ArrivesBeforeRegistration_BuffersUntilPartyRegistered()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "enemy", "remote");
        var client = Clients.First();
        var agentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            mission.PlayerTeam = mission.AttackerTeam;
            mission.AddTeam(BattleSideEnum.Attacker);
            mission.AddTeam(BattleSideEnum.Attacker);

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var ownParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[2], out var remoteParty));
            var remoteMapEventParty = ownParty.MapEvent.AttackerSide.Parties
                .Single(party => party.Party == remoteParty.Party);
            Assert.True(client.ObjectManager.TryGetId(remoteMapEventParty, out var remoteMapEventPartyId));

            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = ownParty;
            try
            {
                Assert.True(client.ObjectManager.Remove(remoteMapEventParty));

                var deployment = new Mock<IBattleDeploymentCoordinator>();
                deployment.SetupGet(d => d.IsCommitted).Returns(true);
                using var spawner = new PuppetSpawner(
                    client.Resolve<IMessageBroker>(),
                    client.ObjectManager,
                    client.Resolve<GameInterface.Services.Players.IPlayerManager>(),
                    client.Resolve<ICoopMissionComponent>(),
                    Mock.Of<IBattleSession>(),
                    new CasualtyAttributionMap(),
                    deployment.Object,
                    Mock.Of<IAgentFormationAssigner>(),
                    new BattleAgentBudget(),
                    client.Resolve<IMissionWeaponDataMapper>());

                var record = new BattleAgentSpawnData(
                    agentId, characterId, default, BattleSideEnum.Attacker, 100f,
                    "host", remoteMapEventPartyId, 1, new Equipment(), new BodyProperties(),
                    new MissionEquipmentData(new()));
                client.Resolve<IMessageBroker>().Publish(
                    this, new NetworkSpawnBattleAgents(new[] { record }));

                var registry = client.Resolve<INetworkAgentRegistry>();
                Assert.False(registry.TryGetAgentInfo(agentId, out _));
                Assert.Empty(mission.Agents);

                Assert.True(client.ObjectManager.AddExisting(remoteMapEventPartyId, remoteMapEventParty));
                spawner.DrainPendingPuppets();

                Assert.True(registry.TryGetAgentInfo(agentId, out var agentInfo));
                Assert.True(AgentMirror.TryGet(agentInfo.Agent, out var mirror));
                var origin = Assert.IsType<CoopAgentOrigin>(mirror.Origin);
                Assert.Same(remoteParty.Party, origin.BattleCombatant);
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
        });
    }

    [Fact]
    public void CrewRecord_BuffersUntilItsHullRegisters()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "enemy");
        var client = Clients.First();
        var agentId = Guid.NewGuid();
        var shipId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            mission.PlayerTeam = mission.AttackerTeam;
            mission.AddTeam(BattleSideEnum.Defender);

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var enemyParty));
            var mapEventParty = enemyParty.MapEvent.DefenderSide.Parties
                .Single(party => party.Party == enemyParty.Party);
            Assert.True(client.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));

            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(true);
            var missionComponent = client.Resolve<ICoopMissionComponent>();
            using var spawner = new PuppetSpawner(
                client.Resolve<IMessageBroker>(),
                client.ObjectManager,
                client.Resolve<GameInterface.Services.Players.IPlayerManager>(),
                missionComponent,
                Mock.Of<IBattleSession>(),
                new CasualtyAttributionMap(),
                deployment.Object,
                Mock.Of<IAgentFormationAssigner>(),
                new BattleAgentBudget(),
                client.Resolve<IMissionWeaponDataMapper>());

            var record = new BattleAgentSpawnData(
                agentId, characterId, default, BattleSideEnum.Defender, 100f, "host", mapEventPartyId, 1,
                new Equipment(), new BodyProperties(), new MissionEquipmentData(new()), shipId: shipId);
            client.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { record }));
            spawner.DrainPendingPuppets();

            var registry = client.Resolve<INetworkAgentRegistry>();
            Assert.False(registry.TryGetAgentInfo(agentId, out _));

            var hull = MissionTestObjects.CreateHull();
            Assert.True(missionComponent.ShipRegistry.TryRegister(
                new NetworkShipInfo(shipId, "host", mapEventPartyId, true, hull, null)));
            spawner.DrainPendingPuppets();

            Assert.True(registry.TryGetAgentInfo(agentId, out _));
        });
    }

    [Fact]
    public void CrewRecord_IsDroppedOnceItsHullGivesUpSpawning()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "enemy");
        var client = Clients.First();
        var agentId = Guid.NewGuid();
        var shipId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            mission.PlayerTeam = mission.AttackerTeam;
            mission.AddTeam(BattleSideEnum.Defender);

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var enemyParty));
            var mapEventParty = enemyParty.MapEvent.DefenderSide.Parties
                .Single(party => party.Party == enemyParty.Party);
            Assert.True(client.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));

            var broker = client.Resolve<IMessageBroker>();
            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(true);
            var missionComponent = client.Resolve<ICoopMissionComponent>();
            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns("local");
            var teams = new Mock<IBattleTeamResolver>();
            teams.Setup(t => t.ResolveReplicatedTeam(BattleSideEnum.Defender, false)).Returns(mission.DefenderTeam.Shell);
            var engine = new Mock<INavalShipEngine>();
            engine.SetupGet(e => e.Hulls).Returns(new List<MissionObject>());
            deployment.SetupGet(d => d.IsTeamSetupOver).Returns(true);
            Formation noFormation = null!;
            engine.Setup(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out noFormation))
                .Returns((MissionObject)null!);
            using var replicator = CreateShipReplicator(broker, session.Object, deployment.Object, missionComponent, engine.Object,
                teams.Object, client.ObjectManager);
            using var spawner = new PuppetSpawner(
                broker,
                client.ObjectManager,
                client.Resolve<GameInterface.Services.Players.IPlayerManager>(),
                missionComponent,
                Mock.Of<IBattleSession>(),
                new CasualtyAttributionMap(),
                deployment.Object,
                Mock.Of<IAgentFormationAssigner>(),
                new BattleAgentBudget(),
                client.Resolve<IMissionWeaponDataMapper>());

            broker.Publish(this, new NetworkSpawnBattleShips(new[]
            {
                new BattleShipSpawnData(shipId, "host", mapEventPartyId, true, BattleSideEnum.Defender, 0,
                    NetworkBattleShipSample.FromFrame(TaleWorlds.Library.MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
                    100f, 50f, 1, ""),
            }));
            var record = new BattleAgentSpawnData(
                agentId, characterId, default, BattleSideEnum.Defender, 100f, "host", mapEventPartyId, 1,
                new Equipment(), new BodyProperties(), new MissionEquipmentData(new()), shipId: shipId);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { record }));
            for (int tick = 1; tick < BattleShipReplicator.ForeignHullSpawnAttempts; tick++)
            {
                Assert.False(missionComponent.ShipRegistry.StateOf(shipId) == ShipLifecycle.SpawnFailed);
                replicator.Tick(BattleShipReplicator.ForeignHullRetrySeconds + 0.1f);
            }

            Assert.True(missionComponent.ShipRegistry.StateOf(shipId) == ShipLifecycle.SpawnFailed);
            engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out noFormation),
                Times.Exactly(BattleShipReplicator.ForeignHullSpawnAttempts));
            spawner.DrainPendingPuppets();

            Assert.True(missionComponent.ShipRegistry.TryRegister(
                new NetworkShipInfo(shipId, "host", mapEventPartyId, true, MissionTestObjects.CreateHull(), null)));
            spawner.DrainPendingPuppets();

            Assert.False(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out _));
        });
    }

    // Vanilla's team setup spawns a hull for every set ship assignment, so a copy spawned before it got a local twin.
    [Fact]
    public void ForeignHullRecord_WaitsForTheInitialShipDeploymentThenSpawnsOnce()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "enemy");
        var client = Clients.First();
        var shipId = Guid.NewGuid();

        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            mission.PlayerTeam = mission.AttackerTeam;
            mission.AddTeam(BattleSideEnum.Defender);

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var enemyParty));
            var mapEventParty = enemyParty.MapEvent.DefenderSide.Parties
                .Single(party => party.Party == enemyParty.Party);
            Assert.True(client.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));

            var broker = client.Resolve<IMessageBroker>();
            var deployment = new Mock<IBattleDeploymentCoordinator>();
            var missionComponent = client.Resolve<ICoopMissionComponent>();
            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns("local");
            var teams = new Mock<IBattleTeamResolver>();
            teams.Setup(t => t.ResolveReplicatedTeam(BattleSideEnum.Defender, false)).Returns(mission.DefenderTeam.Shell);
            var engine = new Mock<INavalShipEngine>();
            var copy = MissionTestObjects.CreateHull();
            engine.SetupGet(e => e.Hulls).Returns(new List<MissionObject> { copy });
            bool initialShipsDeployed = false;
            deployment.SetupGet(d => d.IsTeamSetupOver).Returns(() => initialShipsDeployed);
            Formation noFormation = null!;
            engine.Setup(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out noFormation))
                .Returns(copy);
            using var replicator = CreateShipReplicator(broker, session.Object, deployment.Object, missionComponent, engine.Object,
                teams.Object, client.ObjectManager);

            broker.Publish(this, new NetworkSpawnBattleShips(new[]
            {
                new BattleShipSpawnData(shipId, "host", mapEventPartyId, true, BattleSideEnum.Defender, 0,
                    NetworkBattleShipSample.FromFrame(TaleWorlds.Library.MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
                    100f, 50f, 1, ""),
            }));
            for (int tick = 0; tick < BattleShipReplicator.ForeignHullSpawnAttempts + 1; tick++)
                replicator.Tick(BattleShipReplicator.ForeignHullRetrySeconds + 0.1f);

            engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out noFormation), Times.Never);
            Assert.False(missionComponent.ShipRegistry.TryGet(shipId, out _));
            Assert.False(missionComponent.ShipRegistry.StateOf(shipId) == ShipLifecycle.SpawnFailed);

            initialShipsDeployed = true;
            replicator.Tick(0.1f);
            replicator.Tick(0.1f);

            engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out noFormation), Times.Once);
            Assert.True(missionComponent.ShipRegistry.TryGet(shipId, out var ship));
            Assert.Same(copy, ship.Hull);
        });
    }

    private static BattleShipReplicator CreateShipReplicator(IMessageBroker broker, IBattleSession session,
        IBattleDeploymentCoordinator deployment, ICoopMissionComponent missionComponent, INavalShipEngine engine,
        IBattleTeamResolver teams, GameInterface.Services.ObjectManager.IObjectManager objectManager)
    {
        var epochs = new HostEpochPolicy();
        var members = new Mock<IMissionContext>();
        members.SetupGet(m => m.ControllersInMission).Returns(new[] { "host" });
        var context = new BattleContext(session, deployment, missionComponent, epochs, Mock.Of<IBattleResultCommitter>(),
            Mock.Of<IReinforcementFielder>(), members.Object);
        return new BattleShipReplicator(Mock.Of<IBattleNetwork>(), broker, context, engine, teams, objectManager,
            new NavalHullAuthority(session, missionComponent, engine),
            new BattleShipConditionStream(Mock.Of<IBattleNetwork>(), session, engine, epochs, members.Object),
            Mock.Of<IAgentStationUseReplicator>());
    }

    [Fact]
    public void FleeingCatchUpRecord_SpawnsPuppetAsRunningAway()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("local", "enemy");
        var client = Clients.First();
        var agentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            mission.PlayerTeam = mission.AttackerTeam;
            mission.AddTeam(BattleSideEnum.Defender);

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var enemyParty));
            var mapEventParty = enemyParty.MapEvent.DefenderSide.Parties
                .Single(party => party.Party == enemyParty.Party);
            Assert.True(client.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));

            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(true);
            using var applier = new PuppetRoutApplier(
                client.Resolve<IMessageBroker>(),
                client.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());
            using var spawner = new PuppetSpawner(
                client.Resolve<IMessageBroker>(),
                client.ObjectManager,
                client.Resolve<GameInterface.Services.Players.IPlayerManager>(),
                client.Resolve<ICoopMissionComponent>(),
                Mock.Of<IBattleSession>(),
                new CasualtyAttributionMap(),
                deployment.Object,
                Mock.Of<IAgentFormationAssigner>(),
                new BattleAgentBudget(),
                client.Resolve<IMissionWeaponDataMapper>(),
                applier);

            var record = new BattleAgentSpawnData(
                agentId,
                characterId,
                default,
                BattleSideEnum.Defender,
                100f,
                "host",
                mapEventPartyId,
                1,
                new Equipment(),
                new BodyProperties(),
                new MissionEquipmentData(new()),
                isRunningAway: true);

            client.Resolve<IMessageBroker>().Publish(
                this, new NetworkSpawnBattleAgents(new[] { record }));
            spawner.DrainPendingPuppets();

            var registry = client.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(agentId, out var agentInfo));
            Assert.True(agentInfo.Agent.IsRunningAway);
            Assert.True(AgentMirror.TryGet(agentInfo.Agent, out var mirror));
            Assert.True(mirror.IsActive);
            Assert.Null(mirror.Formation);
        });
    }
}
