using Common;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
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
using Missions.Tournaments.Patches;
using SandBox.GameComponents;
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
                .SetValue(victim, 100.5f);
            victim.Health = 100.5f;
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
            mock.RegisteredBlow = (_, registeredBlow) =>
                InvokeCaptureHitProgression(
                    controller,
                    victim,
                    attacker,
                    registeredBlow,
                    collisionData,
                    shotDifficulty: -1f);
            Assert.False(controller.InterceptBlow(victim, blow, collisionData));
            component.AgentActionHandler.ObserveBlockedHit(
                victim,
                attacker,
                blocked,
                in blow,
                in collisionData);
            var network = Assert.IsType<MockClient>(host.Resolve<INetwork>());
            var battleNetwork = Assert.IsType<MockBattleNetwork>(host.Resolve<IBattleNetwork>());
            Assert.Empty(
                network.NetworkSentMessages.GetMessages<NetworkSubmitTournamentHitProgression>());

            InvokeProcessPendingLocalDamage(controller);

            if (blocked)
            {
                Assert.Empty(
                    network.NetworkSentMessages.GetMessages<NetworkSubmitTournamentHitProgression>());
                return;
            }

            Assert.Single(
                battleNetwork.NetworkSentMessages.GetMessages<NetworkApplyTournamentDamage>());

            NetworkSubmitTournamentHitProgression request = Assert.Single(
                network.NetworkSentMessages.GetMessages<NetworkSubmitTournamentHitProgression>());
            Assert.Equal("host", request.Data.AttackerControllerId);
            Assert.Equal("host", request.Data.DamageOriginControllerId);
            Assert.Equal(attackerId, request.Data.AttackerAgentId);
            Assert.Equal(victimId, request.Data.VictimAgentId);
            Assert.Equal(1, request.Data.DamageSequence);
            Assert.Equal(-1f, request.Data.ShotDifficulty);
            Assert.True(request.Data.Fatal);
        });
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("observer")]
    public void MissileReplay_DoesNotReuseTransientMissileIndex(
        string localControllerId)
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, localControllerId);

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopTournamentController>();
            var registry = client.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("victim-owner", victimId, victim));
            Assert.True(registry.TryRegisterAgent("origin", attackerId, attacker));
            SetField(controller, "snapshot", CreateLiveSnapshot());

            const int missileIndex = 99;
            var blow = new Blow(attacker.Index) { InflictedDamage = 10 };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = missileIndex;
            var message = new NetworkApplyTournamentDamage(
                "session",
                "match",
                1,
                "origin",
                1,
                victimId,
                attackerId,
                blow,
                default);

            InvokeApplyTournamentDamage(controller, message);

            Assert.False(mock.LastRegisteredBlow.IsMissile);
            Assert.Equal(-1, mock.LastRegisteredBlow.WeaponRecord.AffectorWeaponSlotOrMissileIndex);
        });
    }

    [Fact]
    public void QueuedMissileHit_AppliesDamageAndProgressionAfterSourceMissileRemoved()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "origin");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopTournamentController>();
            var registry = client.Resolve<INetworkAgentRegistry>();
            ICoopMissionComponent component = GetTournamentComponent(controller);
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            AccessTools.Property(typeof(Agent), nameof(Agent.HealthLimit))
                .SetValue(victim, 100f);
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("victim-owner", victimId, victim));
            Assert.True(registry.TryRegisterAgent("origin", attackerId, attacker));

            var contestants = new[]
            {
                new TournamentContestantData(
                    "attacker-slot", "attacker", 1, "origin", "Origin", true, false, true, null),
                new TournamentContestantData(
                    "victim-slot", "victim", 2, null, "Victim", false, false, false, null)
            };
            TournamentSessionSnapshot snapshot = CreateLiveSnapshot(contestants);
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
                        CreateSpawnData(attackerId, "attacker-slot", "attacker", "origin"),
                        CreateSpawnData(victimId, "victim-slot", "victim", null)
                    }));

            MissionWeapon missileWeapon = CreateRangedWeapon();
            Assert.True(client.ObjectManager.AddExisting("queued-test-arrow", missileWeapon.Item));
            var equipment = new MissionEquipment();
            var weaponSlots = new MissionWeapon[(int)EquipmentIndex.NumAllWeaponSlots];
            weaponSlots[(int)EquipmentIndex.Weapon0] = missileWeapon;
            typeof(MissionEquipment)
                .GetField("_weaponSlots", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(equipment, weaponSlots);
            Assert.True(AgentMirror.TryGet(attacker, out var attackerMirror));
            attackerMirror.Equipment = equipment;

            const int missileIndex = 99;
            mock.RegisterMissile(missileIndex, attacker, missileWeapon);
            var blow = new Blow(attacker.Index) { InflictedDamage = 10 };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = missileIndex;
            var collisionData = new AttackCollisionData
            {
                _collisionResult = (int)CombatCollisionResult.StrikeAgent
            };
            mock.RegisteredBlow = (_, registeredBlow) =>
                InvokeCaptureHitProgression(
                    controller,
                    victim,
                    attacker,
                    registeredBlow,
                    collisionData,
                    shotDifficulty: -1f);

            Assert.False(controller.InterceptBlow(victim, blow, collisionData));
            attackerMirror.Equipment = null;
            mock.RemoveMissile(missileIndex);
            Assert.False(mock.HasMissile(missileIndex));
            Assert.False(mock.Shell._missilesDictionary.ContainsKey(missileIndex));

            InvokeProcessPendingLocalDamage(controller);

            Assert.True(AgentMirror.TryGet(victim, out var victimMirror));
            Assert.Equal(90f, victimMirror.Health);
            Assert.False(mock.LastRegisteredBlow.IsMissile);
            Assert.Equal(-1, mock.LastRegisteredBlow.WeaponRecord.AffectorWeaponSlotOrMissileIndex);
            var network = Assert.IsType<MockClient>(client.Resolve<INetwork>());
            NetworkSubmitTournamentHitProgression request = Assert.Single(
                network.NetworkSentMessages
                    .GetMessages<NetworkSubmitTournamentHitProgression>());
            Assert.Equal("queued-test-arrow", request.Data.WeaponItemId);
            Assert.Equal(0, request.Data.WeaponUsageIndex);
            Assert.Equal(mock.ShootDifficulty, request.Data.ShotDifficulty);
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

    [Theory]
    [InlineData(0.25f, false)]
    [InlineData(0.5f, false)]
    [InlineData(1f, false)]
    [InlineData(0.25f, true)]
    public void RemotePlayerVictim_DifficultyModelUsesPlayerReceivedDamage(
        float damageToPlayerMultiplier,
        bool attackerIsHuman)
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = CreateCombatMission(fixture, source, damageToPlayerMultiplier, out var controller);
            var registry = source.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            if (attackerIsHuman)
                mock.MainAgent = attacker;
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("friend", victimId, victim));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            ConfigureLiveMatch(
                controller,
                new[]
                {
                    CreateHumanContestant("victim-slot", "friend"),
                    attackerIsHuman
                        ? CreateHumanContestant("attacker-slot", "host")
                        : CreateNpcContestant("attacker-slot")
                },
                CreateSpawnData(victimId, "victim-slot", "victim", "friend"),
                CreateSpawnData(attackerId, "attacker-slot", "attacker", attackerIsHuman ? "host" : null));

            Assert.Equal(
                damageToPlayerMultiplier,
                Mission.Current.GetDamageMultiplierOfCombatDifficulty(victim, attacker));
        });
    }

    [Theory]
    [InlineData(true, 0.25f)]
    [InlineData(false, 1f)]
    public void RemotePlayerMount_DifficultyModelUsesRiderOnlyWhileRidden(
        bool ridden,
        float expectedMultiplier)
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = CreateCombatMission(fixture, source, 0.25f, out var controller);
            var registry = source.Resolve<INetworkAgentRegistry>();
            Agent rider = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent mount = mock.SpawnMount(ridden ? rider : null);
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            Guid riderId = Guid.NewGuid();
            Guid mountId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("friend", riderId, rider));
            Assert.True(registry.TryRegisterAgent("friend", mountId, mount));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            ConfigureLiveMatch(
                controller,
                new[]
                {
                    CreateHumanContestant("victim-slot", "friend"),
                    CreateNpcContestant("attacker-slot")
                },
                CreateSpawnData(riderId, "victim-slot", "victim", "friend", mountId),
                CreateSpawnData(attackerId, "attacker-slot", "attacker", null));

            Assert.Equal(
                expectedMultiplier,
                Mission.Current.GetDamageMultiplierOfCombatDifficulty(mount, attacker));
            float multiplier = 1f;
            controller.ApplyRemotePlayerDifficulty(mount, ref multiplier);
            Assert.Equal(expectedMultiplier, multiplier);
        });
    }

    [Fact]
    public void OutsideTournament_DifficultyModelKeepsVanillaMultiplier()
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            mock.DamageToPlayerMultiplier = 0.25f;
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));

            Assert.Equal(1f, Mission.Current.GetDamageMultiplierOfCombatDifficulty(victim, null));
        });
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("replaced")]
    [InlineData("unmanifested")]
    public void NonPlayerVictim_DifficultyModelKeepsVanillaMultiplier(string victimKind)
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = CreateCombatMission(fixture, source, 0.25f, out var controller);
            var registry = source.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("friend", victimId, victim));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            TournamentContestantData victimContestant = victimKind switch
            {
                "npc" => CreateNpcContestant("victim-slot"),
                "replaced" => new TournamentContestantData(
                    "victim-slot", "victim", 2, "friend", "Friend", true, true, false, "displaced"),
                _ => CreateHumanContestant("victim-slot", "friend")
            };
            TournamentAgentSpawnData[] agents = victimKind == "unmanifested"
                ? new[] { CreateSpawnData(attackerId, "attacker-slot", "attacker", null) }
                : new[]
                {
                    CreateSpawnData(victimId, "victim-slot", "victim", "friend"),
                    CreateSpawnData(attackerId, "attacker-slot", "attacker", null)
                };
            ConfigureLiveMatch(
                controller,
                new[] { victimContestant, CreateNpcContestant("attacker-slot") },
                agents);

            Assert.Equal(1f, Mission.Current.GetDamageMultiplierOfCombatDifficulty(victim, attacker));
        });
    }

    [Theory]
    [InlineData(true, 0.25f)]
    [InlineData(false, 1f)]
    public void LocalPlayerVictim_DifficultyModelLeavesVanillaMultiplier(
        bool isMainAgent,
        float expectedMultiplier)
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = CreateCombatMission(fixture, source, 0.25f, out var controller);
            var registry = source.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.Player));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            if (isMainAgent)
                mock.MainAgent = victim;
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("host", victimId, victim));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            ConfigureLiveMatch(
                controller,
                new[]
                {
                    CreateHumanContestant("victim-slot", "host"),
                    CreateNpcContestant("attacker-slot")
                },
                CreateSpawnData(victimId, "victim-slot", "victim", "host"),
                CreateSpawnData(attackerId, "attacker-slot", "attacker", null));

            Assert.Equal(
                expectedMultiplier,
                Mission.Current.GetDamageMultiplierOfCombatDifficulty(victim, attacker));
        });
    }

    [Theory]
    [InlineData(36f, false, false, 9)]
    [InlineData(37f, false, false, 10)]
    [InlineData(1f, false, false, 1)]
    [InlineData(5000f, false, false, 1250)]
    [InlineData(37f, true, false, 37)]
    [InlineData(37f, false, true, 37)]
    public void RemotePlayerStrike_BlowDamageCeilsOnceWithTheDifficulty(
        float magnitude,
        bool shieldBlocked,
        bool fallDamage,
        int expectedDamage)
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = CreateCombatMission(fixture, source, 0.25f, out var controller);
            (Agent victim, Agent attacker) = SpawnRemotePlayerAndNpc(mock, source, controller);
            AttackCollisionData collisionData = CreateStrikeCollision(0);
            if (shieldBlocked)
                collisionData._attackBlockedWithShield = true;
            if (fallDamage)
                collisionData.FallSpeed = 5f;

            ComputeVanillaStrikeDamage(victim, attacker, magnitude, ref collisionData);

            Assert.Equal(expectedDamage, collisionData.InflictedDamage);
        });
    }

    [Theory]
    [InlineData(0.25f, true)]
    [InlineData(1f, false)]
    public void RemotePlayerStrike_ShrugOffDecidedOnCorrectedDamage(
        float damageToPlayerMultiplier,
        bool expectedShrugOff)
    {
        using var fixture = new MissionEngineFixture();
        using var combat = new VanillaCombatScope();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = CreateCombatMission(fixture, source, damageToPlayerMultiplier, out var controller);
            (Agent victim, Agent attacker) = SpawnRemotePlayerAndNpc(mock, source, controller);
            AttackCollisionData collisionData = CreateStrikeCollision(0);
            ComputeVanillaStrikeDamage(victim, attacker, 36f, ref collisionData);
            Blow blow = CreateStrike(attacker, collisionData.InflictedDamage);

            Assert.Equal(
                expectedShrugOff,
                MissionGameModels.Current.AgentApplyDamageModel.DecideAgentShrugOffBlow(
                    victim,
                    in collisionData,
                    in blow));
        });
    }

    [Fact]
    public void RemotePlayerVictim_BroadcastsComputedDamageWithoutRescaling()
    {
        using var fixture = new MissionEngineFixture();
        var source = Clients.First();
        SetControllerId(source, "host");

        source.Call(() =>
        {
            var mock = fixture.CreateMission(source);
            mock.DamageToPlayerMultiplier = 0.25f;
            var controller = source.Resolve<CoopTournamentController>();
            (Agent victim, Agent attacker) = SpawnRemotePlayerAndNpc(mock, source, controller);

            // The difficulty model already applied the multiplier to this blow.
            Assert.False(controller.InterceptBlow(victim, CreateStrike(attacker, 9), CreateStrikeCollision(9)));
            InvokeProcessPendingLocalDamage(controller);

            NetworkApplyTournamentDamage damage = Assert.Single(GetSentDamage(source));
            Assert.Equal(9, damage.Blow.InflictedDamage);
            Assert.Equal(9, damage.CollisionData.InflictedDamage);
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(91f, mirror.Health);
        });
    }

    [Fact]
    public void ReceivedDamage_ForLocalMainAgent_IsNotRescaled()
    {
        using var fixture = new MissionEngineFixture();
        var victimPeer = Clients.First();
        SetControllerId(victimPeer, "friend");

        victimPeer.Call(() =>
        {
            var mock = fixture.CreateMission(victimPeer);
            mock.DamageToPlayerMultiplier = 0.25f;
            var controller = victimPeer.Resolve<CoopTournamentController>();
            var registry = victimPeer.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.Player));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            mock.MainAgent = victim;
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("friend", victimId, victim));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            ConfigureLiveMatch(
                controller,
                new[]
                {
                    CreateHumanContestant("victim-slot", "friend"),
                    CreateNpcContestant("attacker-slot")
                },
                CreateSpawnData(victimId, "victim-slot", "victim", "friend"),
                CreateSpawnData(attackerId, "attacker-slot", "attacker", null));

            InvokeApplyTournamentDamage(
                controller,
                new NetworkApplyTournamentDamage(
                    "session",
                    "match",
                    1,
                    "host",
                    1,
                    victimId,
                    attackerId,
                    CreateStrike(attacker, 9),
                    CreateStrikeCollision(9)));

            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(91f, mirror.Health);
            Assert.Empty(GetSentDamage(victimPeer));
        });
    }

    [Fact]
    public void VictimPeerLocalBlow_StaysDropped()
    {
        using var fixture = new MissionEngineFixture();
        var victimPeer = Clients.First();
        SetControllerId(victimPeer, "friend");

        victimPeer.Call(() =>
        {
            var mock = fixture.CreateMission(victimPeer);
            mock.DamageToPlayerMultiplier = 0.25f;
            var controller = victimPeer.Resolve<CoopTournamentController>();
            var registry = victimPeer.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.Player));
            Agent attacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            mock.MainAgent = victim;
            Guid victimId = Guid.NewGuid();
            Guid attackerId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("friend", victimId, victim));
            Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
            ConfigureLiveMatch(
                controller,
                new[]
                {
                    CreateHumanContestant("victim-slot", "friend"),
                    CreateNpcContestant("attacker-slot")
                },
                CreateSpawnData(victimId, "victim-slot", "victim", "friend"),
                CreateSpawnData(attackerId, "attacker-slot", "attacker", null));

            // Only the attacker owner broadcasts, so this locally scaled puppet swing is dropped.
            Assert.False(controller.InterceptBlow(victim, CreateStrike(attacker, 9), CreateStrikeCollision(9)));
            InvokeProcessPendingLocalDamage(controller);

            Assert.Empty(GetSentDamage(victimPeer));
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.Equal(100f, mirror.Health);
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
        AttackCollisionData collisionData,
        WeaponComponentData attackerWeapon = null,
        float shotDifficulty = -1f)
    {
        MethodInfo capture = typeof(CoopTournamentController).GetMethod(
            "CaptureHitProgression",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(capture);
        capture.Invoke(
            controller,
            new object[] { victim, attacker, attackerWeapon, blow, collisionData, shotDifficulty });
    }

    private static TournamentAgentSpawnData CreateSpawnData(
        Guid agentId,
        string slotId,
        string characterId,
        string controllerId,
        Guid mountAgentId = default) =>
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
            mountAgentId,
            null,
            0,
            Array.Empty<EquipmentElement>(),
            0f);

    private static MissionWeapon CreateRangedWeapon()
    {
        var item = new ItemObject();
        var weaponComponent = new WeaponComponent(item);
        var attackerWeapon = new WeaponComponentData(item, WeaponClass.Arrow, default);
        weaponComponent._weaponList.Add(attackerWeapon);
        item.ItemComponent = weaponComponent;
        return new MissionWeapon(item, null, null);
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

    private static void ConfigureLiveMatch(
        CoopTournamentController controller,
        TournamentContestantData[] contestants,
        params TournamentAgentSpawnData[] agents)
    {
        TournamentSessionSnapshot snapshot = CreateLiveSnapshot(contestants);
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
            new TournamentSpawnManifestData("session", "match", 1, 1, 1, agents));
    }

    private static TournamentContestantData CreateHumanContestant(string slotId, string controllerId) =>
        new(slotId, slotId, 1, controllerId, controllerId, true, false, false, null);

    private static TournamentContestantData CreateNpcContestant(string slotId) =>
        new(slotId, slotId, 2, null, slotId, false, false, false, null);

    private static Blow CreateStrike(Agent attacker, int damage) =>
        new(attacker.Index) { InflictedDamage = damage, DamageType = DamageTypes.Cut };

    private static AttackCollisionData CreateStrikeCollision(int damage) =>
        new()
        {
            _collisionResult = (int)CombatCollisionResult.StrikeAgent,
            InflictedDamage = damage
        };

    private static List<NetworkApplyTournamentDamage> GetSentDamage(EnvironmentInstance instance) =>
        Assert.IsType<MockBattleNetwork>(instance.Resolve<IBattleNetwork>())
            .NetworkSentMessages.GetMessages<NetworkApplyTournamentDamage>().ToList();

    private static MockMission CreateCombatMission(
        MissionEngineFixture fixture,
        EnvironmentInstance instance,
        float damageToPlayerMultiplier,
        out CoopTournamentController controller)
    {
        MockMission mock = fixture.CreateMission(instance);
        mock.DamageToPlayerMultiplier = damageToPlayerMultiplier;
        controller = instance.Resolve<CoopTournamentController>();
        mock.Shell.MissionBehaviors.Add(controller);
        return mock;
    }

    private static (Agent Victim, Agent Attacker) SpawnRemotePlayerAndNpc(
        MockMission mock,
        EnvironmentInstance instance,
        CoopTournamentController controller)
    {
        var registry = instance.Resolve<INetworkAgentRegistry>();
        Agent victim = mock.SpawnAgent(
            new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
        Agent attacker = mock.SpawnAgent(
            new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
        Guid victimId = Guid.NewGuid();
        Guid attackerId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent("friend", victimId, victim));
        Assert.True(registry.TryRegisterAgent("host", attackerId, attacker));
        ConfigureLiveMatch(
            controller,
            new[]
            {
                CreateHumanContestant("victim-slot", "friend"),
                CreateNpcContestant("attacker-slot")
            },
            CreateSpawnData(victimId, "victim-slot", "victim", "friend"),
            CreateSpawnData(attackerId, "attacker-slot", "attacker", null));
        return (victim, attacker);
    }

    // Vanilla GetAttackCollisionResults builds AttackInformation with the mission's difficulty multiplier.
    private static void ComputeVanillaStrikeDamage(
        Agent victim,
        Agent attacker,
        float magnitude,
        ref AttackCollisionData collisionData)
    {
        var attackInformation = new AttackInformation
        {
            VictimAgentAbsorbedDamageRatio = 1f,
            DamageMultiplierOfBone = 1f,
            CombatDifficultyMultiplier = Mission.Current.GetDamageMultiplierOfCombatDifficulty(victim, attacker)
        };
        MissionCombatMechanicsHelper.ComputeBlowDamage(
            in attackInformation,
            in collisionData,
            null,
            DamageTypes.Pierce,
            magnitude,
            0,
            false,
            out collisionData.InflictedDamage,
            out collisionData.AbsorbedByArmor,
            out _);
    }

    // The real stagger threshold reads perks and managed parameters, so it is pinned here.
    private sealed class VanillaCombatScope : IDisposable
    {
        private const float StaggerThresholdDamage = 10f;
        private readonly Harmony harmony = new($"e2e.tournamentdamage.difficulty.{Guid.NewGuid()}");
        private readonly MissionGameModels previousModels = MissionGameModels.Current;

        public VanillaCombatScope()
        {
            TournamentCombatPatchInstaller.Install(harmony);
            harmony.Patch(
                AccessTools.Method(
                    typeof(SandboxAgentApplyDamageModel),
                    nameof(SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage)),
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(VanillaCombatScope),
                    nameof(StaggerThreshold))));
            _ = new MissionGameModels(new GameModel[]
            {
                new SandboxMissionDifficultyModel(),
                new SandboxStrikeMagnitudeModel(),
                new SandboxAgentApplyDamageModel()
            });
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            MissionGameModels.Current = previousModels;
        }

        private static bool StaggerThreshold(ref float __result)
        {
            __result = StaggerThresholdDamage;
            return false;
        }
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
