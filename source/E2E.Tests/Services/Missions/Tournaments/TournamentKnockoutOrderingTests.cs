using System.Reflection;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Tournaments.Data;
using Missions;
using Missions.Agents.Patches;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentKnockoutOrderingTests : MissionTestEnvironment
{
    public TournamentKnockoutOrderingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void VictimKnockoutArrivingFirst_IsDeferredUntilHostAppliesCausingDamage()
    {
        using var fixture = new MissionEngineFixture();
        var clients = Clients.ToArray();
        var host = clients[0];
        var victimOwner = clients[1];
        SetControllerId(host, "host");
        SetControllerId(victimOwner, "victim");

        Guid attackerId = Guid.NewGuid();
        Guid victimId = Guid.NewGuid();
        TournamentSpawnManifestData manifest = CreateManifest(attackerId, victimId);
        CoopTournamentController hostController = null;
        Agent hostVictim = null;

        host.Call(() =>
        {
            var mock = fixture.CreateMission(host);
            hostController = host.Resolve<CoopTournamentController>();
            Configure(hostController, manifest);
            var registry = host.Resolve<INetworkAgentRegistry>();
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            hostVictim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));

            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            Assert.True(registry.TryRegisterAgent("victim", victimId, hostVictim));
        });

        victimOwner.Call(() =>
        {
            var mock = fixture.CreateMission(victimOwner);
            var controller = victimOwner.Resolve<CoopTournamentController>();
            Configure(controller, manifest);
            var registry = victimOwner.Resolve<INetworkAgentRegistry>();
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));

            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            Assert.True(registry.TryRegisterAgent("victim", victimId, victim));

            var causingDamage = new NetworkApplyTournamentDamage(
                "session",
                "match",
                1,
                "host",
                7,
                victimId,
                attackerId,
                new Blow(attacker.Index) { InflictedDamage = 100, DamageType = DamageTypes.Cut },
                default);
            SetField(controller, "activeDamageMessage", causingDamage);

            controller.OnAgentRemoved(victim, attacker, AgentState.Killed, default);
        });

        host.Call(() =>
        {
            var registry = host.Resolve<INetworkAgentRegistry>();
            Assert.True(hostController.Session.IsLocalHost);
            Assert.True(registry.TryGetAgentInfo(victimId, out _));
            Assert.True(AgentMirror.TryGet(hostVictim, out var victimMirror));
            Assert.True(victimMirror.IsActive);
            Assert.Equal(100f, victimMirror.Health);
            NetworkTournamentAgentKnockedOut pending = Assert.Single(
                GetField<List<NetworkTournamentAgentKnockedOut>>(hostController, "pendingKnockouts"));
            Assert.Equal("host", pending.DamageOriginControllerId);
            Assert.Equal(7, pending.DamageSequence);

            Assert.True(registry.TryGetAgentInfo(attackerId, out var attackerInfo));
            var causingDamage = new NetworkApplyTournamentDamage(
                "session",
                "match",
                1,
                "host",
                7,
                victimId,
                attackerId,
                new Blow(attackerInfo.Agent.Index)
                {
                    InflictedDamage = 100,
                    DamageType = DamageTypes.Cut
                },
                default);
            Invoke(hostController, "ApplyTournamentDamage", causingDamage);

            Assert.True(GetField<TournamentMessageSequenceLedger>(
                hostController, "appliedDamageSequences").HasReached("host", 7));
            Assert.Equal(0f, victimMirror.Health);
            Assert.False(victimMirror.IsActive);
            Assert.False(registry.TryGetAgentInfo(victimId, out _));
        });
    }

    private static void Configure(
        CoopTournamentController controller,
        TournamentSpawnManifestData manifest)
    {
        Assert.True(controller.Session.TryApplyState(
            "session", "mission", 1, 1, "match", "host", Array.Empty<string>()));
        SetField(controller, "snapshot", CreateSnapshot());
        SetField(controller, "latestManifest", manifest);
    }

    private static TournamentSpawnManifestData CreateManifest(Guid attackerId, Guid victimId) =>
        new(
            "session",
            "match",
            1,
            1,
            1,
            new[]
            {
                CreateAgent(attackerId, "attacker", "host"),
                CreateAgent(victimId, "victim", "victim")
            });

    private static TournamentAgentSpawnData CreateAgent(
        Guid agentId,
        string slotId,
        string controllerId) =>
        new(
            agentId,
            slotId,
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
            0);

    private static TournamentSessionSnapshot CreateSnapshot() =>
        new(
            "session",
            "mission",
            "town",
            "scene",
            null,
            TournamentSessionPhase.LiveMatch,
            1,
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

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object value = field.GetValue(target);
        Assert.IsType<T>(value);
        return (T)value;
    }

    private static void Invoke(object target, string name, object argument)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(target, new[] { argument });
    }
}
