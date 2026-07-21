using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Surrogates;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// A new AI party joining a live battle is queued until deployment is active and its authoritative reserve
/// arrives. Its local authority then adds the party's full reserve to the native phase, fields only the
/// persistent initial entitlement, and leaves the rest available to native reinforcement waves.
/// </summary>
public class BattleReinforcementSpawnTests : MissionTestEnvironment
{
    private const int PartyTroops = 5;
    private const int InitialTroops = 2;

    private sealed class SpawnLogicHarness
    {
        public readonly DefaultBattleMissionAgentSpawnLogic Logic;
        public readonly MissionSpawnPhase DefenderPhase;
        public readonly MissionBattleSideSpawnContext DefenderContext;

        public SpawnLogicHarness(
            DefaultBattleMissionAgentSpawnLogic logic,
            MissionSpawnPhase defenderPhase,
            MissionBattleSideSpawnContext defenderContext)
        {
            Logic = logic;
            DefenderPhase = defenderPhase;
            DefenderContext = defenderContext;
        }
    }

    private sealed class ThrowOnceFormationAssigner : IAgentFormationAssigner
    {
        public int Attempts { get; private set; }

        public Formation Assign(Agent agent)
        {
            Attempts++;
            if (Attempts == 1)
                throw new InvalidOperationException("injected setup failure");
            return null;
        }

        public Formation Assign(Agent agent, int formationIndex) => Assign(agent);
    }

    private sealed class RecordingBattleNetwork : IBattleNetwork
    {
        public List<IMessage> SentMessages { get; } = new List<IMessage>();

        public void ConnectToInstance(string instanceId) { }
        public void Start() { }
        public void Stop() { }
        public void Send(string controllerId, Common.PacketHandlers.IPacket packet) => throw new NotSupportedException();
        public void SendAll(Common.PacketHandlers.IPacket packet) => throw new NotSupportedException();
        public void SendAllBut(string controllerId, Common.PacketHandlers.IPacket packet) => throw new NotSupportedException();
        public void Send(string controllerId, IMessage message) => throw new NotSupportedException();
        public void SendAll(IMessage message) => SentMessages.Add(message);
        public void SendAllBut(string controllerId, IMessage message) => throw new NotSupportedException();
    }

    public enum PendingRecoveryResolution
    {
        ExactAgent,
        ExactAgentTransferred,
        ExactAgentTransferredDuringCapture,
        LateReplay,
        BothRegistered,
        BothRegisteredAfterExactTransfer,
        BothRegisteredWithRemoteMount,
        StaleDepartedHostReplay,
        StaleDepartedHostReplayWithMount,
        ScopeRemoved,
        ScopeRemovedAfterTransfer,
    }

    public BattleReinforcementSpawnTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Add an unowned AI party to the defender side and return its MapEventParty id.</summary>
    private string AddAiReinforcementParty(
        string mapEventId,
        string reinforcementCharacterId,
        EnvironmentInstance authority)
    {
        var aiPartyId = CreateRegisteredObject<MobileParty>();
        string mapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));

            aiParty.Party.MapEventSide = mapEvent.DefenderSide;

            var mep = mapEvent.DefenderSide.Parties.Last(p => p.Party == aiParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(mep, out mapEventPartyId));
        }, MapEventDisabledMethods);

        authority.Call(() =>
        {
            Assert.True(authority.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));
            Assert.True(authority.ObjectManager.TryGetObject<CharacterObject>(
                reinforcementCharacterId, out var reinforcementCharacter));
            aiParty.Party.MemberRoster.Clear();
            aiParty.Party.MemberRoster.AddToCounts(reinforcementCharacter, PartyTroops);
        });

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId;
    }

    private static void PublishInvolvedPartiesAdded(
        EnvironmentInstance instance,
        string mapEventId,
        string mapEventPartyId,
        IMessageBroker messageBroker = null,
        int initialSpawnCount = InitialTroops)
    {
        (messageBroker ?? instance.Resolve<IMessageBroker>()).Publish(instance,
            new NetworkAddInvolvedParties(
                mapEventId,
                new[] { mapEventPartyId },
                new[] { new CampaignVec2(default, true) },
                new[] { initialSpawnCount },
                new[] { true }));
    }

    private static TroopReserveEntry[] Entries(string reinforcementCharacterId)
    {
        var entries = new TroopReserveEntry[PartyTroops];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = new TroopReserveEntry(194900 + i, reinforcementCharacterId, formationClass: 0);
        return entries;
    }

    private static CoopTroopSupplier RegisterEmptySupplier(
        EnvironmentInstance authority,
        string mapEventId)
    {
        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        var supplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, authority.ObjectManager);
        supplier.SetReserve(Array.Empty<PartyReserve>());
        CoopTroopSupplierRegistry.Register(supplier);
        return supplier;
    }

    private static void FeedReinforcementReserve(
        CoopTroopSupplier supplier,
        string mapEventPartyId,
        string reinforcementCharacterId)
        => supplier.SetReserve(new[]
        {
            new PartyReserve(
                mapEventPartyId,
                suppliedCount: 0,
                Entries(reinforcementCharacterId),
                InitialTroops),
        });

    private static SpawnLogicHarness CreateSpawnLogic(
        string mapEventId,
        EnvironmentInstance authority,
        CoopTroopSupplier defenderSupplier)
    {
        var attackerSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
        attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

        var spawnLogic = ObjectHelper.SkipConstructor<DefaultBattleMissionAgentSpawnLogic>();
        var defenderPhase = new MissionSpawnPhase();
        var attackerPhase = new MissionSpawnPhase();
        var phases = new[]
        {
            new List<MissionSpawnPhase> { defenderPhase },
            new List<MissionSpawnPhase> { attackerPhase },
        };
        var contexts = new[]
        {
            new MissionBattleSideSpawnContext(
                spawnLogic, BattleSideEnum.Defender, defenderSupplier, isPlayerSide: false),
            new MissionBattleSideSpawnContext(
                spawnLogic, BattleSideEnum.Attacker, attackerSupplier, isPlayerSide: true),
        };

        // These engine fields are readonly, so the headless skip-constructor shell needs its arrays installed.
        AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_phases").SetValue(spawnLogic, phases);
        AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_numberOfTroopsInTotal")
            .SetValue(spawnLogic, new int[2]);
        AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_battleSideSpawnContexts")
            .SetValue(spawnLogic, contexts);

        return new SpawnLogicHarness(spawnLogic, defenderPhase, contexts[(int)BattleSideEnum.Defender]);
    }

    private static ReinforcementFielder CreateFielder(
        EnvironmentInstance authority,
        CoopBattleController controller,
        DefaultBattleMissionAgentSpawnLogic spawnLogic,
        IAgentFormationAssigner formationAssigner = null,
        Func<Mission, AgentBuildData, Agent> agentSpawner = null,
        IMessageBroker messageBroker = null,
        ICasualtyAttributionMap casualties = null,
        IBattleNetwork network = null)
        => new ReinforcementFielder(
            messageBroker ?? authority.Resolve<IMessageBroker>(),
            network ?? authority.Resolve<IBattleNetwork>(),
            authority.ObjectManager,
            authority.Resolve<ICoopMissionComponent>(),
            authority.Resolve<IMissionContext>(),
            controller.Session,
            controller.Deployment,
            formationAssigner ?? authority.Resolve<IAgentFormationAssigner>(),
            casualties ?? new CasualtyAttributionMap(),
            () => spawnLogic,
            agentSpawner);

    private static void AssertIntegrated(
        MockMission mock,
        CoopTroopSupplier supplier,
        SpawnLogicHarness spawnLogic)
    {
        Assert.Equal(InitialTroops, mock.Agents.Count(agent => agent.IsActive() && !agent.IsMount));
        Assert.Equal(InitialTroops, Assert.Single(supplier.GetSuppliedByParty()).supplied);
        Assert.Equal(PartyTroops - InitialTroops, supplier.NumTroopsNotSupplied);
        Assert.Equal(PartyTroops, spawnLogic.DefenderPhase.TotalSpawnNumber);
        Assert.Equal(PartyTroops - InitialTroops, spawnLogic.DefenderPhase.RemainingSpawnNumber);
        Assert.Equal(InitialTroops, spawnLogic.DefenderPhase.InitialSpawnedNumber);
        Assert.Equal(InitialTroops, spawnLogic.DefenderPhase.NumberActiveTroops);
        Assert.Equal(InitialTroops, spawnLogic.DefenderContext._numSpawnedTroops);
        Assert.Equal(PartyTroops, spawnLogic.Logic._numberOfTroopsInTotal[(int)BattleSideEnum.Defender]);
    }

    private static int[] GetSpawnedSeeds(MockMission mock)
    {
        return mock.Agents
            .Where(agent => agent.IsActive() && !agent.IsMount)
            .Select(agent => Assert.IsType<CoopAgentOrigin>(agent.Origin).UniqueSeed)
            .OrderBy(seed => seed)
            .ToArray();
    }

    private static void SetFlattenedRoster(
        EnvironmentInstance authority,
        string mapEventPartyId,
        string reinforcementCharacterId,
        params int[] seeds)
    {
        authority.Call(() =>
        {
            Assert.True(authority.ObjectManager.TryGetObject<MapEventParty>(
                mapEventPartyId, out var mapEventParty));
            Assert.True(authority.ObjectManager.TryGetObject<CharacterObject>(
                reinforcementCharacterId, out var reinforcementCharacter));

            var roster = new FlattenedTroopRoster(seeds.Length);
            foreach (int seed in seeds)
            {
                var descriptor = new UniqueTroopDescriptor(seed);
                roster[descriptor] = new FlattenedTroopRosterElement(
                    reinforcementCharacter, default, 0, descriptor, 0);
            }
            mapEventParty._roster = roster;
        });
    }

    [Fact]
    public void ActivatedAuthority_NewAiParty_FieldsEntitlementAndKeepsFullWaveReserve()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            using var fielder = CreateFielder(authority, controller, spawnLogic.Logic);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                fielder.Tick();
                Assert.Empty(mock.Agents);
                Assert.Equal(0, spawnLogic.DefenderPhase.TotalSpawnNumber);

                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);
                fielder.Tick();

                AssertIntegrated(mock, supplier, spawnLogic);
                var waveOrigin = Assert.IsType<CoopAgentOrigin>(supplier.SupplyTroops(1).Single());
                Assert.Equal(aiMapEventPartyId, waveOrigin.MapEventPartyId);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void BeforeActivation_NewAiParty_RemainsQueuedAndFieldsAfterCommit()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            using var fielder = CreateFielder(authority, controller, spawnLogic.Logic);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                fielder.Tick();
                Assert.Empty(mock.Agents);

                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);
                fielder.Tick();
                Assert.Empty(mock.Agents);
                Assert.Equal(0, Assert.Single(supplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, spawnLogic.DefenderPhase.TotalSpawnNumber);

                controller.OnDeploymentFinished();
                fielder.Tick();

                AssertIntegrated(mock, supplier, spawnLogic);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void DuplicateInvolvedPartiesBroadcast_IntegratesTheAiPartyOnce()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            using var fielder = CreateFielder(authority, controller, spawnLogic.Logic);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                fielder.Tick();
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);
                fielder.Tick();
                fielder.Tick();

                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                fielder.Tick();

                AssertIntegrated(mock, supplier, spawnLogic);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void SpawnAgentFailure_RetriesConsumedOriginWithoutAdvancingPhase()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            int spawnAttempts = 0;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                if (spawnAttempts == 1)
                    throw new InvalidOperationException("injected pre-agent spawn failure");
                return mock.SpawnAgent(buildData);
            }

            using var fielder = CreateFielder(
                authority, controller, spawnLogic.Logic, agentSpawner: SpawnAgent);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Empty(mock.Agents);
                Assert.Equal(0, Assert.Single(supplier.GetSuppliedByParty()).supplied);
                Assert.Equal(PartyTroops, spawnLogic.DefenderPhase.TotalSpawnNumber);
                Assert.Equal(PartyTroops, spawnLogic.DefenderPhase.RemainingSpawnNumber);
                Assert.Equal(0, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(0, spawnLogic.DefenderPhase.NumberActiveTroops);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);

                fielder.Tick();

                Assert.Equal(InitialTroops + 1, spawnAttempts);
                AssertIntegrated(mock, supplier, spawnLogic);
                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void MountedSpawnFailureBeforeRiderBuild_FadesPartialMountBeforeRetry()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            int spawnAttempts = 0;
            Agent partialMount = null;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                if (spawnAttempts == 1)
                {
                    var partialRider = mock.SpawnAgent(buildData);
                    partialMount = mock.SpawnMount(partialRider);
                    Assert.True(mock.UntrackAgent(partialRider));
                    throw new InvalidOperationException("injected failure after mount build but before rider build");
                }

                mock.SpawnMounted = true;
                return mock.SpawnAgent(buildData);
            }

            using var fielder = CreateFielder(
                authority, controller, spawnLogic.Logic, agentSpawner: SpawnAgent);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.NotNull(partialMount);
                Assert.False(partialMount.IsActive());
                Assert.DoesNotContain(mock.Agents, agent => agent.IsActive());
                Assert.False(authority.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(partialMount, out _));
                Assert.Equal(0, Assert.Single(supplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);

                fielder.Tick();

                Assert.Equal(InitialTroops + 1, spawnAttempts);
                AssertIntegrated(mock, supplier, spawnLogic);
                Assert.Equal(InitialTroops, mock.Agents.Count(agent => agent.IsActive() && agent.IsMount));
                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));

                fielder.Tick();

                Assert.Equal(InitialTroops + 1, spawnAttempts);
                Assert.Equal(InitialTroops, mock.Agents.Count(agent => agent.IsActive() && agent.IsMount));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void SpawnAgentPostCreationFailure_RetiresUnregisteredBodyBeforeRetry()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            int spawnAttempts = 0;
            Agent recoveredAgent = null;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                var spawned = mock.SpawnAgent(buildData);
                if (spawnAttempts == 1)
                {
                    recoveredAgent = spawned;
                    throw new InvalidOperationException("injected post-agent spawn failure");
                }
                return spawned;
            }

            using var captureBroker = new MessageBroker();
            int recoveredCaptures = 0;
            captureBroker.Subscribe<AgentSpawnedInBattle>(_ => recoveredCaptures++);
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                agentSpawner: SpawnAgent,
                messageBroker: captureBroker);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId, captureBroker);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Equal(1, recoveredCaptures);
                Assert.Empty(GetSpawnedSeeds(mock));
                Assert.Equal(0, Assert.Single(supplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.False(authority.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(recoveredAgent, out _));
                Assert.False(recoveredAgent.IsActive());

                fielder.Tick();

                Assert.Equal(InitialTroops + 1, spawnAttempts);
                Assert.Equal(1, recoveredCaptures);
                AssertIntegrated(mock, supplier, spawnLogic);
                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));

                fielder.Tick();

                Assert.Equal(InitialTroops + 1, spawnAttempts);
                Assert.Equal(1, recoveredCaptures);
                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void PostPlanCaptureTransferredRemotely_DoesNotCommitLocalSpawnStateOrRetry()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            var registry = authority.Resolve<INetworkAgentRegistry>();
            var formationAssigner = new ThrowOnceFormationAssigner();
            int spawnAttempts = 0;
            Agent transferredAgent = null;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                transferredAgent = mock.SpawnAgent(buildData);
                throw new InvalidOperationException("injected post-agent spawn failure");
            }

            using var captureBroker = new MessageBroker();
            captureBroker.Subscribe<AgentSpawnedInBattle>(payload =>
            {
                Assert.True(registry.TryRegisterAgent("client", Guid.NewGuid(), payload.What.Agent));
            });
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                formationAssigner,
                SpawnAgent,
                captureBroker);
            try
            {
                PublishInvolvedPartiesAdded(
                    authority,
                    mapEventId,
                    aiMapEventPartyId,
                    captureBroker,
                    initialSpawnCount: 1);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.True(transferredAgent.IsActive());
                Assert.True(registry.TryGetAgentInfo(transferredAgent, out var transferredInfo));
                Assert.Equal("client", transferredInfo.CurrentAuthority);
                Assert.Equal(1, Assert.Single(supplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, formationAssigner.Attempts);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(0, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(PartyTroops - 1, spawnLogic.DefenderPhase.RemainingSpawnNumber);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Equal(0, formationAssigner.Attempts);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);

                Assert.IsType<CoopAgentOrigin>(transferredAgent.Origin).SetKilled();
                Assert.Equal(0, supplier.NumRemovedTroops);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void PostPlanMigrationReplayWithUnsuppliedPointer_DoesNotDuplicateSeed()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(authority, aiMapEventPartyId, reinforcementCharacterId, 194900);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 1);

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            var registry = authority.Resolve<INetworkAgentRegistry>();
            var casualties = new CasualtyAttributionMap();
            var formationAssigner = new ThrowOnceFormationAssigner();
            int spawnAttempts = 0;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                return mock.SpawnAgent(buildData);
            }

            using var migrationBroker = new MessageBroker();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                formationAssigner,
                SpawnAgent,
                migrationBroker,
                casualties);
            try
            {
                PublishInvolvedPartiesAdded(
                    authority,
                    mapEventId,
                    aiMapEventPartyId,
                    migrationBroker,
                    initialSpawnCount: 1);

                Assert.True(authority.ObjectManager.TryGetObject<CharacterObject>(
                    reinforcementCharacterId, out var reinforcementCharacter));
                Assert.True(authority.ObjectManager.TryGetObject<MapEventParty>(
                    aiMapEventPartyId, out var mapEventParty));
                var replayOrigin = new CoopAgentOrigin(
                    reinforcementCharacter,
                    mapEventParty.Party,
                    -1,
                    null,
                    new UniqueTroopDescriptor(194900),
                    aiMapEventPartyId);
                var replayAgent = mock.SpawnAgent(new AgentBuildData(reinforcementCharacter)
                    .Team(BattleTeams.Resolve(BattleSideEnum.Defender))
                    .TroopOrigin(replayOrigin)
                    .Controller(AgentControllerType.None));
                var replayAgentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent(
                    controller.Session.OwnControllerId,
                    replayAgentId,
                    replayAgent));
                casualties.Record(
                    replayAgentId,
                    aiMapEventPartyId,
                    194900,
                    reinforcementCharacterId);

                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[0] },
                        initialSpawnCount: 1),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(0, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
                Assert.Equal(1, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, formationAssigner.Attempts);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(1, spawnLogic.DefenderPhase.TotalSpawnNumber);
                Assert.Equal(0, spawnLogic.DefenderPhase.RemainingSpawnNumber);

                fielder.Tick();

                Assert.Equal(0, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void LateJoinPromotion_ServerDepartedHistoryDoesNotRespawnOrStrandTheSeed()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(authority, aiMapEventPartyId, reinforcementCharacterId, 194900, 194901);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 2);

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            var registry = authority.Resolve<INetworkAgentRegistry>();
            var casualties = new CasualtyAttributionMap();
            var formationAssigner = new ThrowOnceFormationAssigner();
            int spawnAttempts = 0;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                return mock.SpawnAgent(buildData);
            }

            using var migrationBroker = new MessageBroker();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                formationAssigner,
                SpawnAgent,
                migrationBroker,
                casualties);
            try
            {
                PublishInvolvedPartiesAdded(
                    authority,
                    mapEventId,
                    aiMapEventPartyId,
                    migrationBroker,
                    initialSpawnCount: 2);

                Assert.True(authority.ObjectManager.TryGetObject<CharacterObject>(
                    reinforcementCharacterId, out var reinforcementCharacter));
                Assert.True(authority.ObjectManager.TryGetObject<MapEventParty>(
                    aiMapEventPartyId, out var mapEventParty));
                var replayOrigin = new CoopAgentOrigin(
                    reinforcementCharacter,
                    mapEventParty.Party,
                    -1,
                    null,
                    new UniqueTroopDescriptor(194900),
                    aiMapEventPartyId);
                var replayAgent = mock.SpawnAgent(new AgentBuildData(reinforcementCharacter)
                    .Team(BattleTeams.Resolve(BattleSideEnum.Defender))
                    .TroopOrigin(replayOrigin)
                    .Controller(AgentControllerType.None));
                var replayAgentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent(
                    controller.Session.OwnControllerId,
                    replayAgentId,
                    replayAgent));
                casualties.Record(
                    replayAgentId,
                    aiMapEventPartyId,
                    194900,
                    reinforcementCharacterId);
                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        Entries(reinforcementCharacterId).Take(2).ToArray(),
                        initialSpawnCount: 2,
                        departedSeeds: new[] { 194901 }),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(0, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
                Assert.Equal(2, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, formationAssigner.Attempts);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(1, spawnLogic.DefenderPhase.TotalSpawnNumber);
                Assert.Equal(0, spawnLogic.DefenderPhase.RemainingSpawnNumber);

                fielder.Tick();

                Assert.Equal(0, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void SpawnAgentPostCreationFailure_ScopeShrinkAllowsExpandedRequeueAfterCleanup()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            int spawnAttempts = 0;
            Agent pendingAgent = null;
            Agent pendingMount = null;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                var spawned = mock.SpawnAgent(buildData);
                if (spawnAttempts == 1)
                {
                    pendingAgent = spawned;
                    pendingMount = mock.SpawnMount(spawned);
                    throw new InvalidOperationException("injected post-agent mounted spawn failure");
                }
                return spawned;
            }

            using var captureBroker = new MessageBroker();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                agentSpawner: SpawnAgent,
                messageBroker: captureBroker);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId, captureBroker);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.False(pendingAgent.IsActive());
                Assert.False(pendingMount.IsActive());
                Assert.Equal(PartyTroops, spawnLogic.DefenderPhase.TotalSpawnNumber);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);

                supplier.SetReserve(Array.Empty<PartyReserve>());
                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.False(pendingMount.IsActive());
                Assert.Empty(supplier.GetSuppliedByParty());
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);

                var expandedEntries = new List<TroopReserveEntry>(Entries(reinforcementCharacterId))
                {
                    new TroopReserveEntry(194905, reinforcementCharacterId, formationClass: 0),
                };
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId, captureBroker);
                supplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        expandedEntries.ToArray(),
                        InitialTroops),
                });

                fielder.Tick();

                Assert.Equal(2, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
                Assert.Equal(6, spawnLogic.DefenderPhase.TotalSpawnNumber);
                Assert.Equal(5, spawnLogic.DefenderPhase.RemainingSpawnNumber);
                Assert.Equal(1, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(1, spawnLogic.DefenderContext._numSpawnedTroops);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void ScopeShrink_DoesNotHideLaterPartyCapacityBehindStalePhaseTotal()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            supplier.SetReserve(new[]
            {
                new PartyReserve(
                    "returned-party",
                    suppliedCount: 0,
                    Entries(reinforcementCharacterId),
                    initialSpawnCount: PartyTroops),
            });
            supplier.RecordPhaseCapacity("returned-party", PartyTroops);

            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            spawnLogic.DefenderPhase.TotalSpawnNumber = PartyTroops;
            spawnLogic.Logic._numberOfTroopsInTotal[(int)BattleSideEnum.Defender] = PartyTroops;

            supplier.SetReserve(Array.Empty<PartyReserve>());

            using var fielder = CreateFielder(authority, controller, spawnLogic.Logic);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(InitialTroops, mock.Agents.Count);
                Assert.Equal(InitialTroops, Assert.Single(supplier.GetSuppliedByParty()).supplied);
                Assert.Equal(PartyTroops * 2, spawnLogic.DefenderPhase.TotalSpawnNumber);
                Assert.Equal(PartyTroops - InitialTroops, spawnLogic.DefenderPhase.RemainingSpawnNumber);
                Assert.Equal(PartyTroops * 2,
                    spawnLogic.Logic._numberOfTroopsInTotal[(int)BattleSideEnum.Defender]);
                Assert.Equal(PartyTroops, supplier.GetRepresentedPhaseCapacity("returned-party"));
                Assert.Equal(PartyTroops, supplier.GetRepresentedPhaseCapacity(aiMapEventPartyId));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void SetupFailure_RetriesSameLiveAgentWithoutDuplicatingOrMissingCounters()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            var formationAssigner = new ThrowOnceFormationAssigner();
            using var fielder = CreateFielder(
                authority, controller, spawnLogic.Logic, formationAssigner);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(2, formationAssigner.Attempts);
                AssertIntegrated(mock, supplier, spawnLogic);
                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));

                fielder.Tick();

                Assert.Equal(3, formationAssigner.Attempts);
                AssertIntegrated(mock, supplier, spawnLogic);
                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void SetupFailure_AuthorityTransferDropsStaleRetry()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();

            var supplier = RegisterEmptySupplier(authority, mapEventId);
            var spawnLogic = CreateSpawnLogic(mapEventId, authority, supplier);
            var formationAssigner = new ThrowOnceFormationAssigner();
            using var fielder = CreateFielder(
                authority, controller, spawnLogic.Logic, formationAssigner);
            try
            {
                PublishInvolvedPartiesAdded(authority, mapEventId, aiMapEventPartyId);
                FeedReinforcementReserve(supplier, aiMapEventPartyId, reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(2, formationAssigner.Attempts);
                var pendingAgent = mock.Agents.Single(agent =>
                    agent.IsActive()
                    && !agent.IsMount
                    && Assert.IsType<CoopAgentOrigin>(agent.Origin).UniqueSeed == 194900);
                var registry = authority.Resolve<INetworkAgentRegistry>();
                if (!registry.TryGetAgentInfo(pendingAgent, out var pendingInfo))
                {
                    Assert.True(registry.TryRegisterAgent(
                        controller.Session.OwnControllerId,
                        Guid.NewGuid(),
                        pendingAgent));
                    Assert.True(registry.TryGetAgentInfo(pendingAgent, out pendingInfo));
                }
                Assert.True(registry.TryTransferAuthority("client", pendingInfo.AgentId));

                fielder.Tick();

                Assert.Equal(2, formationAssigner.Attempts);
                Assert.True(pendingAgent.IsActive());
                Assert.True(registry.TryGetAgentInfo(pendingAgent, out pendingInfo));
                Assert.Equal("client", pendingInfo.CurrentAuthority);

                Assert.True(registry.TryTransferAuthority(
                    controller.Session.OwnControllerId,
                    pendingInfo.AgentId));
                fielder.Tick();

                Assert.Equal(2, formationAssigner.Attempts);
                Assert.True(pendingAgent.IsActive());
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void MigrationRecovery_RetriesCreationAndSetupWithoutReportingOrDuplicating()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(
            authority, aiMapEventPartyId, reinforcementCharacterId, 194900);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 1000);

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            int spawnAttempts = 0;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                if (spawnAttempts == 1)
                    throw new InvalidOperationException("injected pre-agent migration failure");
                return mock.SpawnAgent(buildData);
            }

            var formationAssigner = new ThrowOnceFormationAssigner();
            using var migrationBroker = new MessageBroker();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                formationAssigner,
                SpawnAgent,
                migrationBroker);
            try
            {
                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[0] },
                        initialSpawnCount: 1),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Empty(mock.Agents);
                Assert.Equal(0, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(0, formationAssigner.Attempts);

                fielder.Tick();

                Assert.Equal(2, spawnAttempts);
                Assert.Equal(1, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                Assert.Equal(1, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(1, formationAssigner.Attempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));

                fielder.Tick();

                Assert.Equal(2, spawnAttempts);
                Assert.Equal(1, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                Assert.Equal(1, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(2, formationAssigner.Attempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void MigrationRecovery_DepartureAfterQueueConsumesClaimWithoutRespawning()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(
            authority, aiMapEventPartyId, reinforcementCharacterId, 194900);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 1000);

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            int spawnAttempts = 0;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                throw new InvalidOperationException("injected pre-agent migration failure");
            }

            var casualties = new CasualtyAttributionMap();
            var recordingNetwork = new RecordingBattleNetwork();
            using var migrationBroker = new MessageBroker();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                agentSpawner: SpawnAgent,
                messageBroker: migrationBroker,
                casualties: casualties,
                network: recordingNetwork);
            try
            {
                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[0] },
                        initialSpawnCount: 1),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Equal(0, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                var departedAgentId = Guid.NewGuid();
                casualties.Record(
                    departedAgentId,
                    aiMapEventPartyId,
                    194900,
                    reinforcementCharacterId);
                casualties.MarkDeparted(departedAgentId);
                Assert.True(authority.ObjectManager.TryGetObject<CharacterObject>(
                    reinforcementCharacterId, out var reinforcementCharacter));
                Assert.True(authority.ObjectManager.TryGetObject<MapEventParty>(
                    aiMapEventPartyId, out var mapEventParty));
                var replayOrigin = new CoopAgentOrigin(
                    reinforcementCharacter,
                    mapEventParty.Party,
                    -1,
                    null,
                    new UniqueTroopDescriptor(194900),
                    aiMapEventPartyId);
                var replayAgent = mock.SpawnAgent(new AgentBuildData(reinforcementCharacter)
                    .Team(BattleTeams.Resolve(BattleSideEnum.Defender))
                    .TroopOrigin(replayOrigin)
                    .Controller(AgentControllerType.None));
                var registry = authority.Resolve<INetworkAgentRegistry>();
                var replayAgentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent(
                    "older-departed-host", replayAgentId, replayAgent));
                casualties.Record(
                    replayAgentId,
                    aiMapEventPartyId,
                    194900,
                    reinforcementCharacterId);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Equal(1, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                Assert.Empty(GetSpawnedSeeds(mock));
                Assert.False(replayAgent.IsActive());
                Assert.False(registry.TryGetAgentInfo(replayAgentId, out _));
                var routed = Assert.Single(recordingNetwork.SentMessages.OfType<NetworkBattleAgentRouted>());
                Assert.Equal(replayAgentId, routed.AgentId);
                Assert.True(routed.IsAdministrativeRemoval);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);

                fielder.Tick();
                Assert.Equal(1, spawnAttempts);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void MigrationRecovery_ReplacementsDoNotRaiseExpandedPhaseBaselineAgain()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(
            authority,
            aiMapEventPartyId,
            reinforcementCharacterId,
            194900,
            194901,
            194902,
            194903);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 2);

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            using var migrationBroker = new MessageBroker();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                messageBroker: migrationBroker);
            try
            {
                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        Entries(reinforcementCharacterId).Take(4).ToArray(),
                        initialSpawnCount: 2),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(new[] { 194900, 194901 }, GetSpawnedSeeds(mock));
                Assert.Equal(2, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(2, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(2, spawnLogic.DefenderPhase.NumberActiveTroops);

                var registry = authority.Resolve<INetworkAgentRegistry>();
                var firstWave = mock.Agents
                    .Where(agent => agent.IsActive() && !agent.IsMount)
                    .ToArray();
                foreach (var agent in firstWave)
                {
                    Assert.IsType<CoopAgentOrigin>(agent.Origin).SetKilled();
                    Assert.True(registry.RemoveAgent(agent));
                    Assert.True(AgentMirror.TryGet(agent, out var mirror));
                    mirror.IsActive = false;
                }

                fielder.Tick();

                Assert.Equal(new[] { 194902, 194903 }, GetSpawnedSeeds(mock));
                Assert.Equal(4, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(2, defenderSupplier.NumRemovedTroops);
                Assert.Equal(2, spawnLogic.DefenderContext.NumberOfActiveTroops);
                Assert.Equal(2, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(2, spawnLogic.DefenderPhase.NumberActiveTroops);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void MigrationRecovery_RemoteCaptureConsumesCurrentTickSlot()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var firstPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        var secondPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(authority, firstPartyId, reinforcementCharacterId, 194900);
        SetFlattenedRoster(authority, secondPartyId, reinforcementCharacterId, 194901);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 1);
            authority.Resolve<IMessageBroker>().Publish(
                authority,
                new NetworkMissionPeerEntered("client", mapEventId));

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            var registry = authority.Resolve<INetworkAgentRegistry>();
            var casualties = new CasualtyAttributionMap();
            var formationAssigner = new ThrowOnceFormationAssigner();
            int spawnAttempts = 0;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                var agent = mock.SpawnAgent(buildData);
                throw new InvalidOperationException("injected post-agent migration failure");
            }

            using var migrationBroker = new MessageBroker();
            migrationBroker.Subscribe<AgentSpawnedInBattle>(payload =>
            {
                var agent = payload.What.Agent;
                var origin = Assert.IsType<CoopAgentOrigin>(agent.Origin);
                var agentId = Guid.NewGuid();
                Assert.True(registry.TryRegisterAgent("client", agentId, agent));
                casualties.Record(
                    agentId,
                    origin.MapEventPartyId,
                    origin.UniqueSeed,
                    reinforcementCharacterId);
            });
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                formationAssigner,
                SpawnAgent,
                migrationBroker,
                casualties);
            try
            {
                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        firstPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[0] },
                        initialSpawnCount: 1),
                    new PartyReserve(
                        secondPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[1] },
                        initialSpawnCount: 1),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
                Assert.Equal(0, formationAssigner.Attempts);
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                var suppliedByParty = defenderSupplier.GetSuppliedByParty();
                Assert.Equal(1, suppliedByParty.Single(state => state.partyId == firstPartyId).supplied);
                Assert.Equal(0, suppliedByParty.Single(state => state.partyId == secondPartyId).supplied);

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
                var remoteOrigin = Assert.IsType<CoopAgentOrigin>(mock.Agents.Single(
                    agent => agent.IsActive() && !agent.IsMount).Origin);
                remoteOrigin.SetKilled();
                Assert.Equal(0, defenderSupplier.NumRemovedTroops);
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }

    [Theory]
    [InlineData(PendingRecoveryResolution.ExactAgent)]
    [InlineData(PendingRecoveryResolution.ExactAgentTransferred)]
    [InlineData(PendingRecoveryResolution.ExactAgentTransferredDuringCapture)]
    [InlineData(PendingRecoveryResolution.LateReplay)]
    [InlineData(PendingRecoveryResolution.BothRegistered)]
    [InlineData(PendingRecoveryResolution.BothRegisteredAfterExactTransfer)]
    [InlineData(PendingRecoveryResolution.BothRegisteredWithRemoteMount)]
    [InlineData(PendingRecoveryResolution.StaleDepartedHostReplay)]
    [InlineData(PendingRecoveryResolution.StaleDepartedHostReplayWithMount)]
    [InlineData(PendingRecoveryResolution.ScopeRemoved)]
    [InlineData(PendingRecoveryResolution.ScopeRemovedAfterTransfer)]
    public void MigrationRecovery_PendingLocalAgentCommitsOrYieldsToLateReplayWithoutRespawning(
        PendingRecoveryResolution resolution)
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var reinforcementCharacterId = CreateRegisteredObject<CharacterObject>();
        var authority = Clients.First();
        CoopBattleController controller = null;
        MockMission mock = null;

        authority.Call(() =>
        {
            mock = fixture.CreateMission(authority);
            controller = authority.Resolve<CoopBattleController>();
        });
        EnterBattle(authority, mapEventId);
        var aiMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        var competingMapEventPartyId = AddAiReinforcementParty(
            mapEventId, reinforcementCharacterId, authority);
        SetFlattenedRoster(
            authority, aiMapEventPartyId, reinforcementCharacterId, 194900);
        SetFlattenedRoster(
            authority, competingMapEventPartyId, reinforcementCharacterId, 194901);

        authority.Call(() =>
        {
            controller.OnDeploymentFinished();
            BattleSpawnGate.BeginBattle(mapEventId, 1);
            authority.Resolve<IMessageBroker>().Publish(
                authority,
                new NetworkMissionPeerEntered("client", mapEventId));
            Assert.Contains("client", authority.Resolve<IMissionContext>().ControllersInMission);

            var defenderSupplier = RegisterEmptySupplier(authority, mapEventId);
            var attackerSupplier = new CoopTroopSupplier(
                mapEventId, BattleSideEnum.Attacker, authority.ObjectManager);
            attackerSupplier.SetReserve(Array.Empty<PartyReserve>());
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            var spawnLogic = CreateSpawnLogic(mapEventId, authority, defenderSupplier);
            int spawnAttempts = 0;
            Agent recoveredAgent = null;
            Agent replayAgent = null;
            CoopAgentOrigin pendingOrigin = null;
            Agent SpawnAgent(Mission _, AgentBuildData buildData)
            {
                spawnAttempts++;
                pendingOrigin = Assert.IsType<CoopAgentOrigin>(buildData.AgentOrigin);
                throw new InvalidOperationException("injected pre-agent migration failure");
            }

            using var migrationBroker = new MessageBroker();
            var casualties = new CasualtyAttributionMap();
            var recordingNetwork = new RecordingBattleNetwork();
            using var fielder = CreateFielder(
                authority,
                controller,
                spawnLogic.Logic,
                agentSpawner: SpawnAgent,
                messageBroker: migrationBroker,
                casualties: casualties,
                network: recordingNetwork);
            try
            {
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        competingMapEventPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[1] },
                        initialSpawnCount: 0),
                });
                migrationBroker.Publish(authority, new BattleHostMigrated(mapEventId, "departed-host"));
                defenderSupplier.SetReserve(new[]
                {
                    new PartyReserve(
                        aiMapEventPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[0] },
                        initialSpawnCount: 1),
                    new PartyReserve(
                        competingMapEventPartyId,
                        suppliedCount: 0,
                        new[] { Entries(reinforcementCharacterId)[1] },
                        initialSpawnCount: 0),
                });
                attackerSupplier.SetReserve(Array.Empty<PartyReserve>());

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                Assert.NotNull(pendingOrigin);
                Assert.All(defenderSupplier.GetSuppliedByParty(), state => Assert.Equal(0, state.supplied));
                Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Empty(GetSpawnedSeeds(mock));

                bool hasReplay = resolution == PendingRecoveryResolution.LateReplay
                    || resolution == PendingRecoveryResolution.BothRegistered
                    || resolution == PendingRecoveryResolution.BothRegisteredAfterExactTransfer
                    || resolution == PendingRecoveryResolution.BothRegisteredWithRemoteMount
                    || resolution == PendingRecoveryResolution.StaleDepartedHostReplay
                    || resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount;
                bool staleReplay = resolution == PendingRecoveryResolution.StaleDepartedHostReplay
                    || resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount;
                bool transferredExactWins = resolution == PendingRecoveryResolution.BothRegisteredAfterExactTransfer;
                bool remoteAgentWins = (hasReplay && !staleReplay)
                    || resolution == PendingRecoveryResolution.ExactAgentTransferred
                    || resolution == PendingRecoveryResolution.ExactAgentTransferredDuringCapture;
                Assert.True(authority.ObjectManager.TryGetObject<CharacterObject>(
                    reinforcementCharacterId, out var reinforcementCharacter));
                Assert.True(authority.ObjectManager.TryGetObject<MapEventParty>(
                    aiMapEventPartyId, out var mapEventParty));
                var team = BattleTeams.Resolve(BattleSideEnum.Defender);
                Assert.NotNull(team);
                if (resolution != PendingRecoveryResolution.LateReplay)
                {
                    recoveredAgent = mock.SpawnAgent(new AgentBuildData(reinforcementCharacter)
                        .Team(team)
                        .TroopOrigin(pendingOrigin)
                        .Controller(AgentControllerType.AI));
                }
                if (hasReplay)
                {
                    var replayOrigin = new CoopAgentOrigin(
                        reinforcementCharacter,
                        mapEventParty.Party,
                        -1,
                        null,
                        new UniqueTroopDescriptor(194900),
                        aiMapEventPartyId);
                    replayAgent = mock.SpawnAgent(new AgentBuildData(reinforcementCharacter)
                        .Team(team)
                        .TroopOrigin(replayOrigin)
                        .Controller(AgentControllerType.None));
                }

                var registry = authority.Resolve<INetworkAgentRegistry>();
                Agent remoteMount = null;
                Guid? remoteMountId = null;
                Agent staleReplayMount = null;
                Guid? staleReplayMountId = null;
                if (resolution == PendingRecoveryResolution.BothRegisteredWithRemoteMount)
                {
                    remoteMount = mock.SpawnMount();
                    Assert.True(AgentMirror.TryGet(recoveredAgent, out var recoveredMirror));
                    recoveredMirror.MountAgent = remoteMount;
                }
                if (resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount)
                    staleReplayMount = mock.SpawnMount(replayAgent);

                Guid Register(string controllerId, Agent registeredAgent)
                {
                    var registeredAgentId = Guid.NewGuid();
                    Assert.True(registry.TryRegisterAgent(
                        controllerId, registeredAgentId, registeredAgent));
                    casualties.Record(
                        registeredAgentId,
                        aiMapEventPartyId,
                        194900,
                        reinforcementCharacterId);
                    return registeredAgentId;
                }

                Guid? recoveredAgentId = null;
                if (resolution == PendingRecoveryResolution.ExactAgent
                    || resolution == PendingRecoveryResolution.ExactAgentTransferred
                    || resolution == PendingRecoveryResolution.BothRegistered
                    || resolution == PendingRecoveryResolution.BothRegisteredAfterExactTransfer
                    || resolution == PendingRecoveryResolution.BothRegisteredWithRemoteMount
                    || resolution == PendingRecoveryResolution.StaleDepartedHostReplay
                    || resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount
                    || resolution == PendingRecoveryResolution.ScopeRemovedAfterTransfer)
                {
                    bool authorityTransferred = resolution == PendingRecoveryResolution.ExactAgentTransferred
                        || resolution == PendingRecoveryResolution.BothRegisteredAfterExactTransfer
                        || resolution == PendingRecoveryResolution.ScopeRemovedAfterTransfer;
                    string recoveredAuthority = authorityTransferred
                        ? "client"
                        : controller.Session.OwnControllerId;
                    recoveredAgentId = Register(recoveredAuthority, recoveredAgent);
                }
                if (resolution == PendingRecoveryResolution.ExactAgentTransferredDuringCapture)
                {
                    migrationBroker.Subscribe<AgentSpawnedInBattle>(payload =>
                    {
                        Assert.Same(recoveredAgent, payload.What.Agent);
                        recoveredAgentId = Register("client", recoveredAgent);
                    });
                }
                if (remoteMount != null)
                {
                    remoteMountId = Guid.NewGuid();
                    Assert.True(registry.TryRegisterAgent(
                        "client", remoteMountId.GetValueOrDefault(), remoteMount));
                }
                Guid? replayAgentId = null;
                if (hasReplay)
                {
                    string replayAuthority = transferredExactWins
                        ? controller.Session.OwnControllerId
                        : staleReplay ? "older-departed-host" : "client";
                    replayAgentId = Register(replayAuthority, replayAgent);
                }
                if (staleReplayMount != null)
                {
                    staleReplayMountId = Guid.NewGuid();
                    Assert.True(registry.TryRegisterAgent(
                        "older-departed-host", staleReplayMountId.GetValueOrDefault(), staleReplayMount));
                }
                bool scopeRemoved = resolution == PendingRecoveryResolution.ScopeRemoved
                    || resolution == PendingRecoveryResolution.ScopeRemovedAfterTransfer;
                if (scopeRemoved)
                {
                    defenderSupplier.SetReserve(
                        resolution == PendingRecoveryResolution.ScopeRemovedAfterTransfer
                            ? Array.Empty<PartyReserve>()
                            : new[]
                            {
                                new PartyReserve(
                                    competingMapEventPartyId,
                                    suppliedCount: 0,
                                    new[] { Entries(reinforcementCharacterId)[1] },
                                    initialSpawnCount: 1),
                            });
                }

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                bool recoveredIsRegistered = recoveredAgent != null
                    && registry.TryGetAgentInfo(recoveredAgent, out _);
                Assert.Equal(
                    resolution == PendingRecoveryResolution.ExactAgent
                        || resolution == PendingRecoveryResolution.ExactAgentTransferred
                        || resolution == PendingRecoveryResolution.ExactAgentTransferredDuringCapture
                        || resolution == PendingRecoveryResolution.BothRegisteredAfterExactTransfer
                        || resolution == PendingRecoveryResolution.StaleDepartedHostReplay
                        || resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount
                        || resolution == PendingRecoveryResolution.ScopeRemovedAfterTransfer,
                    recoveredIsRegistered);
                Guid[] expectedRoutedIds;
                if (resolution == PendingRecoveryResolution.BothRegistered
                    || resolution == PendingRecoveryResolution.BothRegisteredWithRemoteMount)
                    expectedRoutedIds = new[] { recoveredAgentId.GetValueOrDefault() };
                else if (transferredExactWins)
                    expectedRoutedIds = new[] { replayAgentId.GetValueOrDefault() };
                else if (resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount)
                    expectedRoutedIds = new[]
                    {
                        replayAgentId.GetValueOrDefault(),
                        staleReplayMountId.GetValueOrDefault(),
                    };
                else if (staleReplay)
                    expectedRoutedIds = new[] { replayAgentId.GetValueOrDefault() };
                else
                    expectedRoutedIds = Array.Empty<Guid>();
                var routedMessages = recordingNetwork.SentMessages
                    .OfType<NetworkBattleAgentRouted>()
                    .ToArray();
                Assert.Equal(expectedRoutedIds, routedMessages.Select(message => message.AgentId));
                Assert.Equal(
                    resolution == PendingRecoveryResolution.StaleDepartedHostReplayWithMount
                        ? new[] { true, false }
                        : new bool[expectedRoutedIds.Length],
                    routedMessages.Select(message => message.HideMount));
                Assert.All(routedMessages, message => Assert.True(message.IsAdministrativeRemoval));

                if (scopeRemoved)
                {
                    bool authorityTransferred = resolution == PendingRecoveryResolution.ScopeRemovedAfterTransfer;
                    Assert.Equal(authorityTransferred, recoveredAgent.IsActive());
                    if (authorityTransferred)
                        Assert.Empty(defenderSupplier.GetSuppliedByParty());
                    else
                        Assert.Equal(0, Assert.Single(defenderSupplier.GetSuppliedByParty()).supplied);
                    Assert.Equal(0, spawnLogic.DefenderContext._numSpawnedTroops);
                    Assert.Equal(
                        authorityTransferred ? new[] { 194900 } : Array.Empty<int>(),
                        GetSpawnedSeeds(mock));
                    return;
                }

                var suppliedByParty = defenderSupplier.GetSuppliedByParty();
                Assert.Equal(1, suppliedByParty.Single(state => state.partyId == aiMapEventPartyId).supplied);
                Assert.Equal(0, suppliedByParty.Single(state => state.partyId == competingMapEventPartyId).supplied);
                Assert.Equal(remoteAgentWins ? 0 : 1, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(remoteAgentWins ? 0 : 1, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
                if (recoveredAgent != null)
                    Assert.Equal(!hasReplay || transferredExactWins || staleReplay, recoveredAgent.IsActive());
                else
                    Assert.Equal(PendingRecoveryResolution.LateReplay, resolution);
                if (hasReplay)
                {
                    bool replayWins = !transferredExactWins && !staleReplay;
                    Assert.Equal(replayWins, replayAgent.IsActive());
                    Assert.Equal(replayWins, registry.TryGetAgentInfo(replayAgent, out var replayInfo));
                    if (replayWins)
                        Assert.Equal("client", replayInfo.CurrentAuthority);
                    else if (transferredExactWins)
                    {
                        Assert.True(registry.TryGetAgentInfo(recoveredAgent, out var recoveredInfo));
                        Assert.Equal("client", recoveredInfo.CurrentAuthority);
                    }
                }
                if (remoteMount != null)
                {
                    Assert.True(remoteMount.IsActive());
                    Assert.True(registry.TryGetAgentInfo(remoteMountId.GetValueOrDefault(), out var mountInfo));
                    Assert.Equal("client", mountInfo.CurrentAuthority);
                }
                if (staleReplayMount != null)
                {
                    Assert.False(staleReplayMount.IsActive());
                    Assert.False(registry.TryGetAgentInfo(staleReplayMountId.GetValueOrDefault(), out _));
                }
                if (resolution == PendingRecoveryResolution.ExactAgentTransferred
                    || resolution == PendingRecoveryResolution.ExactAgentTransferredDuringCapture
                    || resolution == PendingRecoveryResolution.BothRegisteredAfterExactTransfer)
                {
                    Assert.IsType<CoopAgentOrigin>(recoveredAgent.Origin).SetKilled();
                    Assert.Equal(0, defenderSupplier.NumRemovedTroops);
                }

                fielder.Tick();

                Assert.Equal(1, spawnAttempts);
                suppliedByParty = defenderSupplier.GetSuppliedByParty();
                Assert.Equal(1, suppliedByParty.Single(state => state.partyId == aiMapEventPartyId).supplied);
                Assert.Equal(0, suppliedByParty.Single(state => state.partyId == competingMapEventPartyId).supplied);
                Assert.Equal(remoteAgentWins ? 0 : 1, spawnLogic.DefenderContext._numSpawnedTroops);
                Assert.Equal(remoteAgentWins ? 0 : 1, spawnLogic.DefenderPhase.InitialSpawnedNumber);
                Assert.Equal(new[] { 194900 }, GetSpawnedSeeds(mock));
            }
            finally
            {
                CoopTroopSupplierRegistry.ClearBattle(mapEventId);
                BattleSpawnGate.EndBattle();
            }
        });

        GC.KeepAlive(controller);
    }
}
