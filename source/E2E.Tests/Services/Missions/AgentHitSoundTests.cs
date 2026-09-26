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
                .OfType<NetworkAgentHitSound>());
            foreach (var peer in clients.Where((_, i) => i != owner))
                Assert.Empty(peer.Resolve<MockBattleNetwork>().NetworkSentMessages.OfType<NetworkAgentHitSound>());
        }
        finally
        {
            foreach (var handler in handlers) handler.Dispose();
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
                    .OfType<NetworkAgentHitSound>().Count());
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

            network.Verify(value => value.SendAllBut(null, Moq.It.IsAny<NetworkAgentHitSound>()), Moq.Times.Once);
        });
    }

    [Fact]
    public void NetworkSound_RoundTripsTheSelectedSoundPositionAndArmor()
    {
        var original = new NetworkAgentHitSound(Guid.NewGuid(), true, 123, new Vec3(1f, 2f, 3f), 0.75f);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkAgentHitSound>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.VictimAgentId, result.VictimAgentId);
        Assert.True(result.IsMount);
        Assert.Equal(original.SoundIndex, result.SoundIndex);
        Assert.Equal(original.Position, result.Position);
        Assert.Equal(original.ArmorType, result.ArmorType);
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
