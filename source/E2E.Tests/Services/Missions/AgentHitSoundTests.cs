using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Patches;
using Missions.Battles;
using Missions.Messages;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class AgentHitSoundTests : MissionTestEnvironment
{
    public AgentHitSoundTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BodyHit_ReachesObserversWithoutReplayingOnAttackingPeer(bool crossOwnerHit)
    {
        using var fixture = new MissionEngineFixture();
        using var sounds = new SoundRecorder();
        var clients = Clients.ToArray();
        var missions = new MockMission[3];
        var attackers = new Agent[3];
        var victims = new Agent[3];
        var handlers = new List<ICombatHitPresentationHandler>();
        var victimId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        int owner = crossOwnerHit ? 1 : 0;
        BattleSpawnGate.BeginBattle("hit-sound-test");
        try
        {
            for (int i = 0; i < clients.Length; i++)
            {
                int index = i;
                SetControllerId(clients[i], "peer-" + i);
                clients[i].Call(() =>
                {
                    var mission = CreateConnectedMission(fixture, clients[index], "hit-sound-test");
                    missions[index] = mission;
                    var registry = clients[index].Resolve<INetworkAgentRegistry>();
                    attackers[index] = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                    victims[index] = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                    Assert.True(registry.TryRegisterAgent("peer-0", attackerId, attackers[index]));
                    Assert.True(registry.TryRegisterAgent("peer-" + owner, victimId, victims[index]));
                    handlers.Add(clients[index].Resolve<ICombatHitPresentationHandler>());
                });
            }

            var position = new Vec3(4f, 5f, 6f);
            clients[owner].Call(() =>
            {
                var parameter = new SoundEventParameter("Armor Type", 0.75f);
                missions[owner].Shell.MakeSound(123, position, false, true,
                    attackers[owner].Index, victims[owner].Index, ref parameter);
            });

            Assert.True(sounds.Calls.Count == (crossOwnerHit ? 2 : 3),
                string.Join(", ", sounds.Calls.Select(call =>
                    $"peer-{Array.FindIndex(missions, mission => mission.Shell == call.Mission)}: " +
                    $"agents {call.AttackerIndex}/{call.VictimIndex}, sound {call.SoundIndex}")));
            Assert.Single(sounds.Calls, call => call.Mission == missions[2].Shell);
            Assert.All(sounds.Calls, call =>
            {
                Assert.Equal(123, call.SoundIndex);
                Assert.Equal(position, call.Position);
                Assert.Equal("Armor Type", call.ParameterName);
                Assert.Equal(0.75f, call.ArmorType);
            });
            if (crossOwnerHit)
                Assert.DoesNotContain(sounds.Calls, call => call.Mission == missions[0].Shell);
            Assert.Single(clients[owner].Resolve<MockBattleNetwork>().NetworkSentMessages
                .OfType<NetworkMeleeHitPresentation>(), message => message.Kind == MeleeHitPresentationKind.BodyImpact);
            foreach (var peer in clients.Where((_, i) => i != owner))
                Assert.Empty(peer.Resolve<MockBattleNetwork>().NetworkSentMessages.OfType<NetworkMeleeHitPresentation>());
        }
        finally
        {
            foreach (var handler in handlers) handler.Dispose();
            BattleSpawnGate.EndBattle();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoutedHorseCollision_ExcludesTheSourcePeerRegardlessOfHorseAuthority(bool sourceOwnsHorse)
    {
        using var fixture = new MissionEngineFixture();
        using var sounds = new SoundRecorder();
        var clients = Clients.ToArray();
        var missions = new MockMission[3];
        var attackers = new Agent[3];
        var victims = new Agent[3];
        var controllers = new CoopBattleController[3];
        Guid victimId = Guid.NewGuid();
        Guid horseId = Guid.NewGuid();
        var position = new Vec3(4f, 5f, 6f);
        BattleSpawnGate.BeginBattle("routed-hit-sound-test");
        try
        {
            for (int i = 0; i < clients.Length; i++)
            {
                int index = i;
                SetControllerId(clients[i], "peer-" + i);
                clients[i].Call(() =>
                {
                    var mission = CreateConnectedMission(fixture, clients[index], "routed-hit-sound-test");
                    missions[index] = mission;
                    controllers[index] = clients[index].Resolve<CoopBattleController>();
                    var registry = clients[index].Resolve<INetworkAgentRegistry>();
                    Agent rider = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                    attackers[index] = mission.SpawnMount(rider);
                    victims[index] = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                        .Controller(index == 0 ? AgentControllerType.AI : AgentControllerType.None));
                    Assert.True(registry.TryRegisterAgent(sourceOwnsHorse ? "peer-1" : "peer-0", horseId, attackers[index]));
                    Assert.True(registry.TryRegisterAgent("peer-0", victimId, victims[index]));
                    // The native-blow seam models HandleBlow's sound before damage is applied.
                    mission.RegisteredBlow = (victim, blow) =>
                    {
                        var parameter = new SoundEventParameter("Armor Type", 0.1f);
                        mission.Shell.MakeSound(258, blow.GlobalPosition, false, true,
                            blow.OwnerId, victim.Index, ref parameter);
                    };
                });
            }

            clients[1].Call(() =>
            {
                var parameter = new SoundEventParameter("Armor Type", 0.1f);
                missions[1].Shell.MakeSound(258, position, false, true,
                    attackers[1].Index, victims[1].Index, ref parameter);
                var blow = new Blow(attackers[1].Index)
                {
                    InflictedDamage = 1,
                    GlobalPosition = position,
                    DamageType = DamageTypes.Blunt,
                };
                blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
                clients[1].Resolve<IMessageBroker>().Publish(this,
                    new BattlePuppetHit(victims[1], attackers[1], blow, default));
                var field = typeof(CoopBattleController).GetField("damageRouter", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsAssignableFrom<IBattleDamageRouter>(field.GetValue(controllers[1])).Tick(0.016f);
            });

            var routed = Assert.Single(clients[1].Resolve<MockBattleNetwork>().NetworkSentMessages
                .OfType<NetworkApplyBattleDamage>());
            Assert.Equal("peer-1", routed.SourceControllerId);
            Assert.Equal(3, sounds.Calls.Count);
            foreach (var mission in missions)
                Assert.Single(sounds.Calls, call => call.Mission == mission.Shell);
            Assert.DoesNotContain(sounds.Calls,
                call => call.Mission == missions[1].Shell && call.AttackerIndex == -1);
            Assert.Contains(sounds.Calls,
                call => call.Mission == missions[2].Shell && call.AttackerIndex == -1 && call.VictimIndex == -1);
            Assert.True(AgentMirror.TryGet(victims[0], out var victimMirror));
            Assert.Equal(99f, victimMirror.Health);
            Assert.Null(BattleSpawnGate.RoutedBlowSourceControllerId);
        }
        finally
        {
            foreach (var controller in controllers) controller?.Dispose();
            BattleSpawnGate.EndBattle();
        }
    }

    [Theory]
    [InlineData("Armor Type", true, 1)]
    [InlineData("Force", true, 0)]
    [InlineData("Armor Type", false, 0)]
    public void Capture_OnlyRelaysBattleBodyImpactSounds(string parameterName, bool inBattle, int expected)
    {
        using var fixture = new MissionEngineFixture();
        using var sounds = new SoundRecorder();
        var client = Clients.First();
        SetControllerId(client, "owner");
        if (inBattle) BattleSpawnGate.BeginBattle("hit-sound-capture");
        else BattleSpawnGate.EndBattle();
        try
        {
            client.Call(() =>
            {
                var mission = fixture.CreateMission(client);
                Agent victim = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", Guid.NewGuid(), victim));
                using var handler = client.Resolve<ICombatHitPresentationHandler>();
                var parameter = new SoundEventParameter(parameterName, 0.5f);
                mission.Shell.MakeSound(123, new Vec3(1f, 2f, 3f), false, true, -1, victim.Index, ref parameter);
                Assert.Equal(expected, client.Resolve<MockBattleNetwork>().NetworkSentMessages
                    .OfType<NetworkMeleeHitPresentation>().Count(message => message.Kind == MeleeHitPresentationKind.BodyImpact));
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    [Fact]
    public void SuppressedZeroDamageContact_IsSentByAttackerToTheVictimOwner()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "attacker");
        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            Agent attacker = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
            Agent victim = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
            var registry = client.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryRegisterAgent("attacker", Guid.NewGuid(), attacker));
            Assert.True(registry.TryRegisterAgent("victim-owner", Guid.NewGuid(), victim));
            var network = new Moq.Mock<IBattleNetwork>();
            using var handler = new CombatHitPresentationHandler(registry, network.Object, client.Resolve<IMessageBroker>());

            client.Resolve<IMessageBroker>().Publish(this,
                new AgentHitSound(victim, attacker, null, 123, new Vec3(1f, 2f, 3f), 0.5f));

            network.Verify(value => value.SendAllBut(null, Moq.It.Is<NetworkMeleeHitPresentation>(
                message => message.Kind == MeleeHitPresentationKind.BodyImpact)), Moq.Times.Once);
        });
    }

    [Theory]
    [InlineData(MeleeHitPresentationKind.Blood)]
    [InlineData(MeleeHitPresentationKind.ShieldImpact)]
    [InlineData(MeleeHitPresentationKind.BodyImpact)]
    public void NetworkPresentation_RoundTripsExistingFieldsAndSelectedSound(MeleeHitPresentationKind kind)
    {
        var original = new NetworkMeleeHitPresentation(Guid.NewGuid(), true, kind, 4,
            new Vec3(1f, 2f, 3f), WeaponClass.OneHandedSword, 5, 0.5f, 123, 0.75f);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkMeleeHitPresentation>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.VictimAgentId, result.VictimAgentId);
        Assert.True(result.IsMount);
        Assert.Equal(kind, result.Kind);
        Assert.Equal(original.CollisionBoneIndex, result.CollisionBoneIndex);
        Assert.Equal(original.CollisionPosition, result.CollisionPosition);
        Assert.Equal(original.AttackerWeaponClass, result.AttackerWeaponClass);
        Assert.Equal(original.PhysicsMaterialIndex, result.PhysicsMaterialIndex);
        Assert.Equal(original.Strength, result.Strength);
        Assert.Equal(original.SoundIndex, result.SoundIndex);
        Assert.Equal(original.ArmorType, result.ArmorType);
    }

    [Theory]
    [InlineData(MeleeHitPresentationKind.BodyImpact, true, 1)]
    [InlineData(MeleeHitPresentationKind.BodyImpact, false, 1)]
    [InlineData(MeleeHitPresentationKind.ShieldImpact, true, 1)]
    [InlineData(MeleeHitPresentationKind.ShieldImpact, false, 0)]
    public void NetworkPresentation_InactiveVictimStillPlaysBodyImpactOnly(
        MeleeHitPresentationKind kind, bool active, int expectedSounds)
    {
        using var fixture = new MissionEngineFixture();
        using var sounds = new SoundRecorder();
        sounds.StubShieldSound();
        var client = Clients.First();
        SetControllerId(client, "observer");
        BattleSpawnGate.BeginBattle("presentation-receive-test");
        try
        {
            client.Call(() =>
            {
                var mission = fixture.CreateMission(client);
                Agent victim = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
                Guid victimId = Guid.NewGuid();
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", victimId, victim));
                Assert.True(AgentMirror.TryGet(victim, out var mirror));
                mirror.IsActive = active;
                float healthBefore = mirror.Health;
                using var handler = client.Resolve<ICombatHitPresentationHandler>();
                client.Resolve<IMessageBroker>().Publish(this, new NetworkMeleeHitPresentation(
                    victimId, false, kind, -1, new Vec3(1f, 2f, 3f),
                    WeaponClass.OneHandedSword, -1, 0.5f, 123, 0.75f));

                Assert.Equal(expectedSounds, sounds.Calls.Count);
                Assert.Equal(healthBefore, mirror.Health);
                Assert.Equal(active, mirror.IsActive);
                Assert.All(sounds.Calls, call =>
                {
                    Assert.Equal(123, call.SoundIndex);
                    Assert.Equal(kind == MeleeHitPresentationKind.BodyImpact ? "Armor Type" : "Force", call.ParameterName);
                    Assert.Equal(-1, call.AttackerIndex);
                    Assert.Equal(-1, call.VictimIndex);
                });
                Assert.Empty(client.Resolve<MockBattleNetwork>().NetworkSentMessages);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    private sealed class SoundRecorder : IDisposable
    {
        private static readonly List<SoundCall> Recorded = new();
        private readonly Harmony harmony = new("e2e.agent-hit-sound");
        public IReadOnlyList<SoundCall> Calls => Recorded;

        public SoundRecorder()
        {
            Recorded.Clear();
            var method = AccessTools.Method(typeof(Mission), nameof(Mission.MakeSound),
                new[] { typeof(int), typeof(Vec3), typeof(bool), typeof(bool), typeof(int), typeof(int),
                    typeof(SoundEventParameter).MakeByRefType() });
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(SoundRecorder), nameof(Capture)));
            Assert.Single(Harmony.GetPatchInfo(method).Postfixes,
                patch => patch.PatchMethod.DeclaringType == typeof(AgentHitSoundPatch));
        }

        public void StubShieldSound()
        {
            // This lifecycle test does not initialize the game's native sound table.
            harmony.Patch(AccessTools.Method(typeof(CombatHitPresentationHandler),
                nameof(CombatHitPresentationHandler.SelectShieldImpactSound)),
                prefix: new HarmonyMethod(typeof(SoundRecorder), nameof(SelectShieldSound)));
        }

        private static bool SelectShieldSound(ref int __result)
        {
            __result = 123;
            return false;
        }

        private static bool Capture(Mission __instance, int soundIndex, Vec3 position,
            int relatedAgent1, int relatedAgent2, ref SoundEventParameter parameter)
        {
            Recorded.Add(new SoundCall(__instance, soundIndex, position, parameter.ParamName,
                parameter.Value, relatedAgent1, relatedAgent2));
            return false;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            Recorded.Clear();
        }
    }

    private sealed record SoundCall(Mission Mission, int SoundIndex, Vec3 Position,
        string ParameterName, float ArmorType, int AttackerIndex, int VictimIndex);
}
