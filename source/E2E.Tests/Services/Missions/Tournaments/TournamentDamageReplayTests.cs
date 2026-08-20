using Common;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using HarmonyLib;
using Missions;
using Missions.Agents.Patches;
using Missions.Messages;
using Missions.Services.Network;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayerHit_SubmitsProgressionOnlyAfterAcceptedDamage(bool blocked)
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "host");

        host.Call(() =>
        {
            var mock = fixture.CreateMission(host);
            var controller = host.Resolve<CoopTournamentController>();
            ICoopMissionComponent component = GetTournamentComponent(controller);
            var registry = host.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            AccessTools.Property(typeof(Agent), nameof(Agent.HealthLimit))
                .SetValue(victim, 100f);
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("victim-owner", victimId, victim));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));

            var contestants = new[]
            {
                new TournamentContestantData(
                    "attacker-slot", "attacker", 1, "host", "Host", true, false, true, null),
                new TournamentContestantData(
                    "victim-slot", "victim", 2, null, "Victim", false, false, false, null)
            };
            var snapshot = CreateLiveSnapshot(contestants);
            SetField(controller, "snapshot", snapshot);
            Assert.True(controller.Session.TryApplyState(
                snapshot.SessionId,
                snapshot.MissionInstanceId,
                snapshot.Revision,
                snapshot.BracketRevision,
                snapshot.CurrentMatchId,
                snapshot.HostControllerId,
                Array.Empty<string>()));
            SetField(
                controller,
                "latestManifest",
                new TournamentSpawnManifestData(
                    "session",
                    "match",
                    1,
                    1,
                    1,
                    new[]
                    {
                        CreateSpawnData(attackerId, "attacker-slot", "attacker", "host"),
                        CreateSpawnData(victimId, "victim-slot", "victim", null)
                    }));

            var blow = new Blow(attacker.Index) { InflictedDamage = 100 };
            var collisionData = new AttackCollisionData
            {
                _collisionResult = (int)CombatCollisionResult.StrikeAgent
            };
            Assert.False(controller.InterceptBlow(victim, blow, collisionData));
            component.AgentActionHandler.ObserveBlockedHit(
                victim,
                attacker,
                blocked,
                in blow,
                in collisionData);
            InvokeCaptureHitProgression(
                controller,
                victim,
                attacker,
                blow,
                collisionData);

            var network = Assert.IsType<MockClient>(host.Resolve<INetwork>());
            Assert.Empty(
                network.NetworkSentMessages.GetMessages<NetworkSubmitTournamentHitProgression>());

            InvokeProcessPendingLocalDamage(controller);

            if (blocked)
            {
                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkSubmitTournamentHitProgression>());
                return;
            }

            NetworkSubmitTournamentHitProgression request = Assert.Single(
                network.NetworkSentMessages.GetMessages<NetworkSubmitTournamentHitProgression>());
            Assert.Equal("host", request.Data.AttackerControllerId);
            Assert.Equal("host", request.Data.DamageOriginControllerId);
            Assert.Equal(attackerId, request.Data.AttackerAgentId);
            Assert.Equal(victimId, request.Data.VictimAgentId);
            Assert.Equal(1, request.Data.DamageSequence);
            Assert.True(request.Data.Fatal);
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

    [Fact]
    public void OnLeaving_ClearsMissionContextControllers()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "local-player");

        client.Call(() =>
        {
            fixture.CreateMission(client);
            var missionContext = client.Resolve<IMissionContext>();
            client.Resolve<IMessageBroker>().Publish(
                this,
                new NetworkMissionPeerEntered(
                    "former-tournament-peer",
                    "tournament-instance"));
            Assert.Contains(
                "former-tournament-peer",
                missionContext.ControllersInMission);

            InvokeOnLeaving(client.Resolve<CoopTournamentController>());

            Assert.Empty(missionContext.ControllersInMission);
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

    private static void InvokeCaptureHitProgression(
        CoopTournamentController controller,
        Agent victim,
        Agent attacker,
        Blow blow,
        AttackCollisionData collisionData)
    {
        MethodInfo capture = typeof(CoopTournamentController).GetMethod(
            "CaptureHitProgression",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(capture);
        capture.Invoke(
            controller,
            new object[] { victim, attacker, null, blow, collisionData, 0f });
    }

    private static TournamentAgentSpawnData CreateSpawnData(
        Guid agentId,
        string slotId,
        string characterId,
        string controllerId) =>
        new(
            agentId,
            slotId,
            characterId,
            1,
            "team",
            0,
            null,
            controllerId,
            Array.Empty<EquipmentElement>(),
            Vec3.Zero,
            Vec2.Forward,
            100f,
            Guid.Empty,
            null,
            0,
            Array.Empty<EquipmentElement>(),
            0f);

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

    private static TournamentSessionSnapshot CreateLiveSnapshot(
        TournamentContestantData[] contestants = null) =>
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
            contestants ?? Array.Empty<TournamentContestantData>(),
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
