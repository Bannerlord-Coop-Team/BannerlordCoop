using Common;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Tournaments.Data;
using HarmonyLib;
using Missions;
using Missions.Agents.Patches;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentDamageReplayTests : MissionTestEnvironment
{
    public TournamentDamageReplayTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ReplicatedDamage_AppliesToRemoteVictimCopy()
    {
        using var fixture = new MissionEngineFixture();
        var harmony = new Harmony("e2e.tournamentdamage.registerblow");
        MethodInfo registerBlow = AccessTools.Method(
            typeof(Agent),
            nameof(Agent.RegisterBlow),
            new[] { typeof(Blow), typeof(AttackCollisionData).MakeByRefType() });
        MethodInfo registerBlowPrefix = AccessTools.Method(typeof(RegisterBlowPatch), "Prefix");
        Assert.NotNull(registerBlow);
        Assert.NotNull(registerBlowPrefix);
        harmony.Patch(
            registerBlow,
            prefix: new HarmonyMethod(registerBlowPrefix) { priority = Priority.First });

        try
        {
            var observer = Clients.First();
            SetControllerId(observer, "observer");

            observer.Call(() =>
            {
                var mock = fixture.CreateMission(observer);
                var controller = observer.Resolve<CoopTournamentController>();
                var registry = observer.Resolve<INetworkAgentRegistry>();
                Agent victim = mock.SpawnAgent(
                    new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                Agent attacker = mock.SpawnAgent(
                    new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                Guid victimId = Guid.NewGuid();
                Guid attackerId = Guid.NewGuid();

                Assert.True(registry.TryRegisterAgent("victim-owner", victimId, victim));
                Assert.True(registry.TryRegisterAgent("attacker-owner", attackerId, attacker));
                Assert.False(registry.IsLocallyControlled(victim));

                var message = new NetworkApplyTournamentDamage(
                    "session",
                    "match",
                    1,
                    "attacker-owner",
                    1,
                    victimId,
                    attackerId,
                    new Blow(attacker.Index) { InflictedDamage = 30, DamageType = DamageTypes.Cut },
                    default);
                InvokeApplyTournamentDamage(controller, message);

                Assert.True(AgentMirror.TryGet(victim, out var mirror));
                Assert.Equal(70f, mirror.Health);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardedStrikeAgentCandidate_DoesNotBroadcastRawDamage(
        bool mounted)
    {
        using var fixture = new MissionEngineFixture();
        var attackerClient = Clients.First();
        SetControllerId(attackerClient, "attacker-owner");

        attackerClient.Call(() =>
        {
            var mock = fixture.CreateMission(attackerClient);
            var controller =
                attackerClient.Resolve<CoopTournamentController>();
            SetField(controller, "snapshot", CreateLiveSnapshot());
            ICoopMissionComponent component =
                GetTournamentComponent(controller);
            var registry =
                attackerClient.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));
            if (mounted)
            {
                mock.SpawnMount(victim);
                mock.SpawnMount(attacker);
            }

            Assert.True(registry.TryRegisterAgent(
                "victim-owner",
                Guid.NewGuid(),
                victim));
            Assert.True(registry.TryRegisterAgent(
                "attacker-owner",
                Guid.NewGuid(),
                attacker));

            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };
            var blow = new Blow(attacker.Index)
            {
                InflictedDamage = 36,
                DamageType = DamageTypes.Cut
            };
            bool runOriginal = controller.InterceptBlow(
                victim,
                blow,
                collisionData);
            component.AgentActionHandler.ObserveBlockedHit(
                victim,
                attacker,
                isBlocked: true,
                in blow,
                in collisionData);
            InvokeProcessPendingLocalDamage(controller);

            var network = Assert.IsType<MockBattleNetwork>(
                attackerClient.Resolve<IBattleNetwork>());
            Assert.False(runOriginal);
            Assert.Equal(
                0,
                network.NetworkSentMessages.GetMessageCount<
                    NetworkApplyTournamentDamage>());
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(100f, mirror.Health);
        });
    }

    [Fact]
    public void UnguardedStrikeAgentCandidate_BroadcastsAfterDecisionWindow()
    {
        using var fixture = new MissionEngineFixture();
        var attackerClient = Clients.First();
        SetControllerId(attackerClient, "attacker-owner");

        attackerClient.Call(() =>
        {
            var mock = fixture.CreateMission(attackerClient);
            var controller =
                attackerClient.Resolve<CoopTournamentController>();
            SetField(controller, "snapshot", CreateLiveSnapshot());
            var registry =
                attackerClient.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));

            Assert.True(registry.TryRegisterAgent(
                "victim-owner",
                Guid.NewGuid(),
                victim));
            Assert.True(registry.TryRegisterAgent(
                "attacker-owner",
                Guid.NewGuid(),
                attacker));

            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };
            bool runOriginal = controller.InterceptBlow(
                victim,
                new Blow(attacker.Index)
                {
                    InflictedDamage = 36,
                    DamageType = DamageTypes.Cut
                },
                collisionData);
            var network = Assert.IsType<MockBattleNetwork>(
                attackerClient.Resolve<IBattleNetwork>());

            Assert.False(runOriginal);
            Assert.Equal(
                0,
                network.NetworkSentMessages.GetMessageCount<
                    NetworkApplyTournamentDamage>());
            Assert.True(AgentMirror.TryGet(victim, out var before));
            Assert.Equal(100f, before.Health);

            InvokeProcessPendingLocalDamage(controller);

            Assert.Equal(
                1,
                network.NetworkSentMessages.GetMessageCount<
                    NetworkApplyTournamentDamage>());
            Assert.True(AgentMirror.TryGet(victim, out var after));
            Assert.Equal(64f, after.Health);
        });
    }

    [Fact]
    public void CleanGuardWithoutCandidate_DoesNotCancelFollowingDamage()
    {
        using var fixture = new MissionEngineFixture();
        var attackerClient = Clients.First();
        SetControllerId(attackerClient, "attacker-owner");

        attackerClient.Call(() =>
        {
            var mock = fixture.CreateMission(attackerClient);
            var controller =
                attackerClient.Resolve<CoopTournamentController>();
            SetField(controller, "snapshot", CreateLiveSnapshot());
            ICoopMissionComponent component =
                GetTournamentComponent(controller);
            var registry =
                attackerClient.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));

            Assert.True(registry.TryRegisterAgent(
                "victim-owner",
                Guid.NewGuid(),
                victim));
            Assert.True(registry.TryRegisterAgent(
                "attacker-owner",
                Guid.NewGuid(),
                attacker));

            var guardedBlow = new Blow(attacker.Index);
            var guardedCollision = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.Blocked
            };
            component.AgentActionHandler.ObserveBlockedHit(
                victim,
                attacker,
                isBlocked: true,
                in guardedBlow,
                in guardedCollision);

            var damagingBlow = new Blow(attacker.Index)
            {
                InflictedDamage = 36,
                DamageType = DamageTypes.Cut
            };
            var damagingCollision = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };
            Assert.False(controller.InterceptBlow(
                victim,
                damagingBlow,
                damagingCollision));

            InvokeProcessPendingLocalDamage(controller);

            var network = Assert.IsType<MockBattleNetwork>(
                attackerClient.Resolve<IBattleNetwork>());
            Assert.Equal(
                1,
                network.NetworkSentMessages.GetMessageCount<
                    NetworkApplyTournamentDamage>());
            Assert.True(
                AgentMirror.TryGet(
                    victim,
                    out var mirror));
            Assert.Equal(64f, mirror.Health);
        });
    }

    [Fact]
    public void GuardEvidence_CancelsMatchingCandidateFromSamePair()
    {
        using var fixture = new MissionEngineFixture();
        var attackerClient = Clients.First();
        SetControllerId(attackerClient, "attacker-owner");

        attackerClient.Call(() =>
        {
            var mock = fixture.CreateMission(attackerClient);
            var controller =
                attackerClient.Resolve<CoopTournamentController>();
            SetField(controller, "snapshot", CreateLiveSnapshot());
            ICoopMissionComponent component =
                GetTournamentComponent(controller);
            var registry =
                attackerClient.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));

            Assert.True(registry.TryRegisterAgent(
                "victim-owner",
                Guid.NewGuid(),
                victim));
            Assert.True(registry.TryRegisterAgent(
                "attacker-owner",
                Guid.NewGuid(),
                attacker));

            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };
            var firstBlow = new Blow(attacker.Index)
            {
                InflictedDamage = 36,
                DamageType = DamageTypes.Cut,
                GlobalPosition = new Vec3(1f, 2f, 3f)
            };
            var secondBlow = new Blow(attacker.Index)
            {
                InflictedDamage = 10,
                DamageType = DamageTypes.Cut,
                GlobalPosition = new Vec3(4f, 5f, 6f)
            };
            Assert.False(controller.InterceptBlow(
                victim,
                firstBlow,
                collisionData));
            Assert.False(controller.InterceptBlow(
                victim,
                secondBlow,
                collisionData));
            component.AgentActionHandler.ObserveBlockedHit(
                victim,
                attacker,
                isBlocked: true,
                in secondBlow,
                in collisionData);

            InvokeProcessPendingLocalDamage(controller);

            var network = Assert.IsType<MockBattleNetwork>(
                attackerClient.Resolve<IBattleNetwork>());
            Assert.Equal(
                1,
                network.NetworkSentMessages.GetMessageCount<
                    NetworkApplyTournamentDamage>());
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(64f, mirror.Health);
        });
    }

    [Fact]
    public void PendingCandidate_OnLeaving_BroadcastsBeforeNetworkStops()
    {
        using var fixture = new MissionEngineFixture();
        var attackerClient = Clients.First();
        SetControllerId(attackerClient, "attacker-owner");

        attackerClient.Call(() =>
        {
            var mock = fixture.CreateMission(attackerClient);
            var controller =
                attackerClient.Resolve<CoopTournamentController>();
            SetField(controller, "snapshot", CreateLiveSnapshot());
            var registry =
                attackerClient.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));

            Assert.True(registry.TryRegisterAgent(
                "victim-owner",
                Guid.NewGuid(),
                victim));
            Assert.True(registry.TryRegisterAgent(
                "attacker-owner",
                Guid.NewGuid(),
                attacker));
            Assert.False(controller.InterceptBlow(
                victim,
                new Blow(attacker.Index)
                {
                    InflictedDamage = 36,
                    DamageType = DamageTypes.Cut
                },
                new AttackCollisionData
                {
                    _collisionResult =
                        (int)CombatCollisionResult.StrikeAgent
                }));

            InvokeOnLeaving(controller);

            var network = Assert.IsType<MockBattleNetwork>(
                attackerClient.Resolve<IBattleNetwork>());
            Assert.Equal(
                1,
                network.NetworkSentMessages.GetMessageCount<
                    NetworkApplyTournamentDamage>());
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(64f, mirror.Health);
        });
    }

    private static void InvokeApplyTournamentDamage(
        CoopTournamentController controller,
        NetworkApplyTournamentDamage message)
    {
        MethodInfo applyTournamentDamage =
            typeof(CoopTournamentController).GetMethod(
                "ApplyTournamentDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyTournamentDamage);
        GameThread.Run(
            () => applyTournamentDamage.Invoke(
                controller,
                new object[] { message }),
            true);
    }

    private static void SetField(
        object target,
        string name,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private static ICoopMissionComponent GetTournamentComponent(
        CoopTournamentController controller)
    {
        FieldInfo componentField = typeof(CoopMissionController).GetField(
            "coopMissionComponent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentField);
        return Assert.IsAssignableFrom<ICoopMissionComponent>(
            componentField.GetValue(controller));
    }

    private static void InvokeProcessPendingLocalDamage(
        CoopTournamentController controller)
    {
        MethodInfo process = typeof(CoopTournamentController).GetMethod(
            "ProcessPendingLocalDamage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(process);
        process.Invoke(controller, new object[] { false });
    }

    private static void InvokeOnLeaving(
        CoopTournamentController controller)
    {
        MethodInfo onLeaving = typeof(CoopTournamentController).GetMethod(
            "OnLeaving",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onLeaving);
        onLeaving.Invoke(controller, null);
    }

    private static TournamentSessionSnapshot CreateLiveSnapshot() =>
        new(
            "session",
            "mission",
            "town",
            "scene",
            "prize",
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
}
