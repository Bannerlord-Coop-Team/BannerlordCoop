using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Missiles.Handlers;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase B headline: two clients over the mesh. A puppet hit on the attacker's client routes the blow to the
/// victim's owner (<c>CoopBattleController.Handle_BattlePuppetHit</c> → <c>IBattleNetwork.SendAll</c> →
/// <see cref="MeshNetworkRouter"/> → the owner's <c>Handle_NetworkApplyBattleDamage</c>), where it is applied.
/// Exercises the real routing, the mesh transport (incl. <c>NetworkApplyBattleDamage</c> serialization), and
/// the owner-authoritative application — none of which a single-client test covers.
/// </summary>
public class BattleMeshRoutingTests : MissionTestEnvironment
{
    public BattleMeshRoutingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PuppetHit_RoutesOverMesh_AndOnlyTheOwnerAppliesDamage()
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();          // holds the victim as an inert puppet
        var owner = Clients.Skip(1).First();     // controls the real victim
        SetControllerId(attacker, "attacker");
        SetControllerId(owner, "owner");

        var victimId = Guid.NewGuid();
        Agent victimOnOwner = null;
        Agent victimPuppetOnAttacker = null;
        CoopBattleController ownerController = null;
        CoopBattleController attackerController = null;

        // Owner side: the agent it authoritatively controls.
        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            victimOnOwner = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            owner.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", victimId, victimOnOwner);
        });

        // Attacker side: the same agent (same network id) replicated as an inert puppet, then "hit".
        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            attackerController = attacker.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            victimPuppetOnAttacker = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            attacker.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", victimId, victimPuppetOnAttacker);

            // What BattleBlowInterceptPatch publishes when a local troop hits a puppet. Damage waits one
            // frame so a guarded OnScoreHit callback from the same collision can cancel it.
            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            attacker.Resolve<IMessageBroker>().Publish(this, new BattlePuppetHit(victimPuppetOnAttacker, null, blow, default));
            GetDamageRouter(attackerController).Tick(0.016f);
        });

        Assert.True(AgentMirror.TryGet(victimOnOwner, out var ownerMirror));
        Assert.Equal(70f, ownerMirror.Health); // routed over the mesh and applied by the owner

        Assert.True(AgentMirror.TryGet(victimPuppetOnAttacker, out var puppetMirror));
        Assert.Equal(100f, puppetMirror.Health); // never applied on the attacker — the puppet's life is the owner's

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardedStrikeAgentHit_DoesNotRouteRawBlowDamage(
        bool mounted)
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();
        var owner = Clients.Skip(1).First();
        SetControllerId(attacker, "attacker");
        SetControllerId(owner, "owner");

        var victimId = Guid.NewGuid();
        Agent victimOnOwner = null;
        Agent victimPuppetOnAttacker = null;
        Agent strikerOnAttacker = null;
        CoopBattleController ownerController = null;
        CoopBattleController attackerController = null;
        int routedDamageMessages = 0;

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            owner.Resolve<IMessageBroker>()
                .Subscribe<NetworkApplyBattleDamage>(
                    _ => routedDamageMessages++);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            victimOnOwner = mock.SpawnAgent(
                new AgentBuildData(character)
                    .Controller(AgentControllerType.AI));
            if (mounted)
                mock.SpawnMount(victimOnOwner);
            owner.Resolve<INetworkAgentRegistry>()
                .TryRegisterAgent("owner", victimId, victimOnOwner);
        });

        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            attackerController = attacker.Resolve<CoopBattleController>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            victimPuppetOnAttacker = mock.SpawnAgent(
                new AgentBuildData(character)
                    .Controller(AgentControllerType.None));
            strikerOnAttacker = mock.SpawnAgent(
                new AgentBuildData(character)
                    .Controller(AgentControllerType.AI));
            if (mounted)
            {
                mock.SpawnMount(victimPuppetOnAttacker);
                mock.SpawnMount(strikerOnAttacker);
            }
            var registry = attacker.Resolve<INetworkAgentRegistry>();
            registry.TryRegisterAgent(
                "owner",
                victimId,
                victimPuppetOnAttacker);
            registry.TryRegisterAgent(
                "attacker",
                Guid.NewGuid(),
                strikerOnAttacker);

            var blow = new Blow(strikerOnAttacker.Index)
            {
                InflictedDamage = 36,
                DamageType = DamageTypes.Pierce
            };
            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };
            attacker.Resolve<IMessageBroker>().Publish(
                this,
                new BattlePuppetHit(
                    victimPuppetOnAttacker,
                    strikerOnAttacker,
                    blow,
                    collisionData));
            GetBattleComponent(attackerController)
                .AgentActionHandler.ObserveBlockedHit(
                    victimPuppetOnAttacker,
                    strikerOnAttacker,
                    isBlocked: true,
                    in blow,
                    in collisionData);
            GetDamageRouter(attackerController).Tick(0.016f);
        });

        Assert.Equal(0, routedDamageMessages);
        Assert.True(AgentMirror.TryGet(victimOnOwner, out var ownerMirror));
        Assert.Equal(100f, ownerMirror.Health);
        Assert.True(
            AgentMirror.TryGet(
                victimPuppetOnAttacker,
                out var puppetMirror));
        Assert.Equal(100f, puppetMirror.Health);

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }

    [Fact]
    public void CleanGuardWithoutCandidate_DoesNotCancelFollowingDamage()
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();
        var owner = Clients.Skip(1).First();
        SetControllerId(attacker, "attacker");
        SetControllerId(owner, "owner");

        var victimId = Guid.NewGuid();
        Agent victimOnOwner = null;
        Agent victimPuppetOnAttacker = null;
        Agent strikerOnAttacker = null;
        CoopBattleController ownerController = null;
        CoopBattleController attackerController = null;

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            victimOnOwner = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));
            owner.Resolve<INetworkAgentRegistry>()
                .TryRegisterAgent(
                    "owner",
                    victimId,
                    victimOnOwner);
        });

        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            attackerController =
                attacker.Resolve<CoopBattleController>();
            victimPuppetOnAttacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            strikerOnAttacker = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));
            var registry =
                attacker.Resolve<INetworkAgentRegistry>();
            registry.TryRegisterAgent(
                "owner",
                victimId,
                victimPuppetOnAttacker);
            registry.TryRegisterAgent(
                "attacker",
                Guid.NewGuid(),
                strikerOnAttacker);

            var guardedBlow =
                new Blow(strikerOnAttacker.Index);
            var guardedCollision = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.Blocked
            };
            GetBattleComponent(attackerController)
                .AgentActionHandler.ObserveBlockedHit(
                    victimPuppetOnAttacker,
                    strikerOnAttacker,
                    isBlocked: true,
                    in guardedBlow,
                    in guardedCollision);

            var damagingBlow = new Blow(
                strikerOnAttacker.Index)
            {
                InflictedDamage = 36,
                DamageType = DamageTypes.Cut
            };
            var damagingCollision = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };
            attacker.Resolve<IMessageBroker>().Publish(
                this,
                new BattlePuppetHit(
                    victimPuppetOnAttacker,
                    strikerOnAttacker,
                    damagingBlow,
                    damagingCollision));
            GetDamageRouter(attackerController).Tick(0.016f);
        });

        Assert.True(
            AgentMirror.TryGet(
                victimOnOwner,
                out var ownerMirror));
        Assert.Equal(64f, ownerMirror.Health);

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }

    [Fact]
    public void GuardedMissilePuppetHit_ConsumesLocalShotWithoutRoutingDamage()
    {
        using var fixture = new MissionEngineFixture();
        var attacker = Clients.First();
        SetControllerId(attacker, "attacker");

        attacker.Call(() =>
        {
            var mock = fixture.CreateMission(attacker);
            mock.Shell._missilesDictionary =
                new Dictionary<int, Mission.Missile>();
            var controller = attacker.Resolve<CoopBattleController>();
            var registry = attacker.Resolve<INetworkAgentRegistry>();
            Agent victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
            Agent shooter = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI));
            Guid victimId = Guid.NewGuid();
            Guid shooterId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent(
                "victim-owner",
                victimId,
                victim));
            Assert.True(registry.TryRegisterAgent(
                "attacker",
                shooterId,
                shooter));

            BattleDamageRouter damageRouter =
                GetDamageRouter(controller);
            ICoopMissionComponent component =
                GetBattleComponent(controller);
            var missileHandler = Assert.IsType<MissileHandler>(
                component.MissileHandler);
            const int missileIndex = 42;
            const long shotSequence = 1234;
            FieldInfo localShotsField = typeof(MissileHandler).GetField(
                "localShots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(localShotsField);
            var localShots = Assert.IsType<
                Dictionary<int, (Guid AgentId, long ShotSequence)>>(
                    localShotsField.GetValue(missileHandler));
            localShots[missileIndex] = (shooterId, shotSequence);

            var blow = new Blow(shooter.Index)
            {
                InflictedDamage = 36,
                DamageType = DamageTypes.Pierce
            };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex =
                missileIndex;
            var collisionData = new AttackCollisionData
            {
                _collisionResult =
                    (int)CombatCollisionResult.StrikeAgent
            };

            attacker.Resolve<IMessageBroker>().Publish(
                this,
                new BattlePuppetHit(
                    victim,
                    shooter,
                    blow,
                    collisionData));
            component.AgentActionHandler.ObserveBlockedHit(
                victim,
                shooter,
                isBlocked: true,
                in blow,
                in collisionData);
            damageRouter.Tick(0.016f);

            var network = Assert.IsType<MockBattleNetwork>(
                attacker.Resolve<IBattleNetwork>());
            Assert.Equal(
                0,
                network.NetworkSentMessages
                    .GetMessageCount<NetworkApplyBattleDamage>());
            Assert.False(missileHandler.TryTakeLocalShot(
                missileIndex,
                out _,
                out _));

            GC.KeepAlive(controller);
        });
    }

    private static BattleDamageRouter GetDamageRouter(
        CoopBattleController controller)
    {
        FieldInfo damageRouterField =
            typeof(CoopBattleController).GetField(
                "damageRouter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(damageRouterField);
        return Assert.IsType<BattleDamageRouter>(
            damageRouterField.GetValue(controller));
    }

    private static ICoopMissionComponent GetBattleComponent(
        CoopBattleController controller)
    {
        BattleDamageRouter damageRouter =
            GetDamageRouter(controller);
        FieldInfo componentField = typeof(BattleDamageRouter).GetField(
            "coopMissionComponent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentField);
        return Assert.IsAssignableFrom<ICoopMissionComponent>(
            componentField.GetValue(damageRouter));
    }
}
