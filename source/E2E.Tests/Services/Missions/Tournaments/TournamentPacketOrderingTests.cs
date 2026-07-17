using System.Reflection;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Tournaments.Data;
using Missions;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentPacketOrderingTests : MissionTestEnvironment
{
    public TournamentPacketOrderingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void FutureRevisionDamageWithoutAgents_AppliesAfterSnapshotAndAgentsResolve()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopTournamentController>();
            SetField(controller, "snapshot", CreateSnapshot(1));

            Guid victimId = Guid.NewGuid();
            var message = new NetworkApplyTournamentDamage(
                "session",
                "match",
                2,
                "fighter",
                1,
                victimId,
                Guid.Empty,
                new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Cut },
                default);

            client.Resolve<IMessageBroker>().Publish(this, message);

            Agent victim = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
            Assert.True(client.Resolve<INetworkAgentRegistry>().TryRegisterAgent("fighter", victimId, victim));
            SetField(controller, "snapshot", CreateSnapshot(2));
            InvokeDrain(controller);

            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(70f, mirror.Health);
        });
    }

    [Fact]
    public void FutureRevisionRuntimeWithoutManifest_AppliesAfterSnapshotAndManifestResolve()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "observer");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopTournamentController>();
            Assert.True(controller.Session.TryApplyState(
                "session", "mission", 1, 1, "match", "host", Array.Empty<string>()));
            SetField(controller, "snapshot", CreateSnapshot(1));

            Guid agentId = Guid.NewGuid();
            var state = new NetworkTournamentRuntimeState(
                "session",
                "match",
                2,
                "host",
                1,
                Array.Empty<TournamentTeamScoreData>(),
                new[]
                {
                    new TournamentAgentRuntimeData(
                        agentId,
                        55,
                        Array.Empty<TournamentMissionWeaponData>())
                },
                Array.Empty<TournamentWorldItemRuntimeData>());

            client.Resolve<IMessageBroker>().Publish(this, state);

            Agent agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
            Assert.True(client.Resolve<INetworkAgentRegistry>().TryRegisterAgent("fighter", agentId, agent));
            SetField(controller, "snapshot", CreateSnapshot(2));
            SetField(controller, "latestManifest", CreateManifest(agentId, "fighter"));
            InvokeDrain(controller);

            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.Equal(55f, mirror.Health);
        });
    }

    [Fact]
    public void FutureRevisionKnockoutWithoutManifest_AppliesAfterSnapshotAndManifestResolve()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "observer");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopTournamentController>();
            Assert.True(controller.Session.TryApplyState(
                "session", "mission", 1, 1, "match", "host", Array.Empty<string>()));
            SetField(controller, "snapshot", CreateSnapshot(1));

            Guid agentId = Guid.NewGuid();
            var message = new NetworkTournamentAgentKnockedOut(
                "session",
                "match",
                2,
                "fighter",
                1,
                agentId,
                null,
                0);

            client.Resolve<IMessageBroker>().Publish(this, message);

            Agent agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
            var registry = client.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryRegisterAgent("fighter", agentId, agent));
            SetField(controller, "snapshot", CreateSnapshot(2));
            SetField(controller, "latestManifest", CreateManifest(agentId, "fighter"));
            InvokeDrain(controller);

            Assert.False(registry.TryGetAgentInfo(agentId, out _));
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.False(mirror.IsActive);
        });
    }

    private static TournamentSpawnManifestData CreateManifest(Guid agentId, string controllerId) =>
        new(
            "session",
            "match",
            2,
            1,
            1,
            new[]
            {
                new TournamentAgentSpawnData(
                    agentId,
                    "slot",
                    "character",
                    1,
                    "team",
                    1,
                    null,
                    controllerId,
                    Array.Empty<EquipmentElement>(),
                    Vec3.Zero,
                    new Vec2(0, 1),
                    100,
                    Guid.Empty,
                    null,
                    0,
                    Array.Empty<EquipmentElement>(),
                    0)
            });

    private static TournamentSessionSnapshot CreateSnapshot(long revision) =>
        new(
            "session",
            "mission",
            "town",
            "scene",
            "prize",
            TournamentSessionPhase.LiveMatch,
            revision,
            1,
            "match",
            "host",
            Array.Empty<string>(),
            Array.Empty<TournamentContestantData>(),
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            Array.Empty<TournamentRoundData>(),
            0,
            0,
            0,
            false,
            false,
            null);

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private static void InvokeDrain(CoopTournamentController controller)
    {
        MethodInfo drain = typeof(CoopTournamentController).GetMethod(
            "DrainPendingTournamentPackets",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(drain);
        drain.Invoke(controller, null);
    }
}