using Common.Messaging;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Missions.Sessions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleCharacterResolverTests : MissionTestEnvironment
{
    public BattleCharacterResolverTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CampaignCharacterObjectAgent_ReplicatesWithObjectManagerIdAndTracksUpgrades()
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        owner.Call(() =>
        {
            var mission = fixture.CreateMission(owner);
            var broker = owner.Resolve<IMessageBroker>();
            var network = new Mock<IBattleNetwork>();
            var session = Mock.Of<IBattleSession>(value => value.OwnControllerId == "owner" && value.InstanceId == "battle");
            Assert.True(owner.ObjectManager.TryGetObject(characterId, out CharacterObject character));
            var tracked = new List<TrackTroopForUpgrades>();
            broker.Subscribe<TrackTroopForUpgrades>(payload => tracked.Add(payload.What));
            using var replicator = new OwnedAgentReplicator(network.Object, broker, owner.ObjectManager,
                owner.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new BattleAgentSpawnBatchCodec(), owner.Resolve<IMissionWeaponDataMapper>(),
                characterResolver: new CampaignBattleCharacterResolver(owner.ObjectManager));
            var origin = new CoopAgentOrigin(character, null, -1, null, new UniqueTroopDescriptor(7), mapEventPartyId: "mep-1");
            var agent = mission.SpawnAgent(new AgentBuildData(character)
                .Controller(AgentControllerType.AI).TroopOrigin(origin).Team(mission.DefenderTeam.Shell));

            broker.Publish(this, new AgentSpawnedInBattle(agent));
            replicator.FlushPendingSpawns();

            var batch = Assert.Single(network.Invocations.Select(call => call.Arguments[0]).OfType<NetworkSpawnBattleAgents>());
            Assert.Equal(SpawnBatchPurpose.Initial, batch.Purpose);
            var record = Assert.Single(DecodeSpawnBatch(owner, batch));
            Assert.Equal(characterId, record.CharacterId);
            Assert.Equal("mep-1", record.MapEventPartyId);
            Assert.Equal(7, record.TroopSeed);
            var track = Assert.Single(tracked);
            Assert.Equal("mep-1", track.MapEventPartyId);
            Assert.Equal(characterId, track.CharacterId);
        });
    }

    [Fact]
    public void SessionBasicCharacterHero_IsBroadcastByStringIdWithoutCampaignSideEffects()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        owner.Call(() =>
        {
            var mission = fixture.CreateMission(owner);
            var broker = owner.Resolve<IMessageBroker>();
            var network = new Mock<IBattleNetwork>();
            var session = Mock.Of<IBattleSession>(value => value.OwnControllerId == "owner" && value.InstanceId == "battle");
            var troop = CreateTroop("taom_test_rohan_lord");
            AccessTools.Field(typeof(BasicCharacterObject), "_isBasicHero").SetValue(troop, true);
            var tracked = new List<TrackTroopForUpgrades>();
            broker.Subscribe<TrackTroopForUpgrades>(payload => tracked.Add(payload.What));
            using var replicator = new OwnedAgentReplicator(network.Object, broker, owner.ObjectManager,
                owner.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new BattleAgentSpawnBatchCodec(), owner.Resolve<IMissionWeaponDataMapper>(),
                characterResolver: new SessionBattleCharacterResolver());

            // A CustomGame player troop is a BasicCharacterObject, so any Hero.MainHero read throws as it would in TAOM.
            var previousTroop = Game.Current.PlayerTroop;
            Game.Current.PlayerTroop = troop;
            try
            {
                var agent = mission.SpawnAgent(new AgentBuildData(troop)
                    .Controller(AgentControllerType.AI).TroopOrigin(new BasicBattleAgentOrigin(troop)).Team(mission.DefenderTeam.Shell));

                broker.Publish(this, new AgentSpawnedInBattle(agent));
                replicator.FlushPendingSpawns();

                var batch = Assert.Single(network.Invocations.Select(call => call.Arguments[0]).OfType<NetworkSpawnBattleAgents>());
                Assert.Equal(SpawnBatchPurpose.Initial, batch.Purpose);
                var record = Assert.Single(DecodeSpawnBatch(owner, batch));
                Assert.Equal("taom_test_rohan_lord", record.CharacterId);
                Assert.Null(record.MapEventPartyId);
                Assert.Empty(tracked);
                Assert.NotSame(agent, Mission.Current.MainAgent);
            }
            finally
            {
                Game.Current.PlayerTroop = previousTroop;
            }
        });
    }

    [Fact]
    public void SessionRecord_SpawnsAPartylessPuppetWithTeamColours()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            Mock.Get(session).Setup(value => value.IsOwn(It.IsAny<string>())).Returns((string id) => id == "peer");
            var troop = CreateTroop("taom_test_spearman");
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                peer.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), new BattleAgentBudget(),
                peer.Resolve<IMissionWeaponDataMapper>(),
                characterResolver: new FixedCharacterResolver(troop), puppetOrigins: new SessionPuppetOriginFactory());
            var id = Guid.NewGuid();
            var record = new BattleAgentSpawnData(id, "taom_test_spearman", default, BattleSideEnum.Defender,
                100f, "owner", null, 0, new Equipment(), default, null, formationIndex: 0);

            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { record }));

            Assert.True(registry.TryGetAgentInfo(id, out var info));
            Assert.Equal("owner", info.CurrentAuthority);
            Assert.Same(troop, info.Agent.Character);
            var origin = Assert.IsType<SessionPuppetAgentOrigin>(info.Agent.Origin);
            Assert.Equal(mission.DefenderTeam.Shell.Color, origin.FactionColor);
            var pending = (List<BattleAgentSpawnData>)AccessTools.Field(typeof(PuppetSpawner), "pendingPuppets").GetValue(spawner);
            Assert.Empty(pending);
        });
    }

    // The StringId setter is intercepted in the test engine and drops the value, so the backing field is set directly.
    private static BasicCharacterObject CreateTroop(string stringId)
    {
        var troop = new BasicCharacterObject();
        AccessTools.Field(typeof(MBObjectBase), "<StringId>k__BackingField").SetValue(troop, stringId);
        return troop;
    }

    // The session resolver's engine lookup needs a registered BasicCharacterObject type, which would leak across tests.
    private sealed class FixedCharacterResolver : IBattleCharacterResolver
    {
        private readonly BasicCharacterObject character;
        public FixedCharacterResolver(BasicCharacterObject character) { this.character = character; }
        public bool TryGetCharacterId(BasicCharacterObject value, out string characterId)
        {
            characterId = value?.StringId;
            return characterId != null;
        }
        public bool TryResolveCharacter(string characterId, out BasicCharacterObject value)
        {
            value = characterId == character.StringId ? character : null;
            return value != null;
        }
    }
}
