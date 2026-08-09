using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;
using MovementAgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase B: the routed-damage application path against the mock engine. Drives the real
/// <see cref="CoopBattleController"/> damage handler so its missile handling (presentation gating and clearing
/// the sender-local projectile index before re-applying a blow) is verified headlessly — the regression guard for the live
/// <c>Mission.OnAgentHit</c> <c>_missilesDictionary</c> KeyNotFound crash.
/// </summary>
public class BattleDamageMirrorTests : MissionTestEnvironment
{
    public BattleDamageMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CombatVelocityProbe_ExposesOnlyFreshRemoteVictimSamples()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopBattleController>();
            var componentField = typeof(CoopMissionController).GetField(
                "coopMissionComponent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var component = Assert.IsAssignableFrom<ICoopMissionComponent>(
                componentField?.GetValue(controller));
            var registry = component.AgentRegistry;
            Agent remoteVictim = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Agent localVictim = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.AI));
            Agent source = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop));
            Assert.True(AgentMirror.TryGet(source, out var sourceMirror));
            sourceMirror.RealGlobalVelocity = new Vec3(3f, 4f, 8f);
            Assert.True(registry.TryRegisterAgent("remote", Guid.NewGuid(), remoteVictim));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), localVictim));

            MovementAgentData data = new MovementAgentData(source);
            component.AgentMovementHandler.Interpolator.SetRiderTarget(remoteVictim, data);
            component.AgentMovementHandler.Interpolator.SetRiderTarget(localVictim, data);
            Func<Agent, Vec2?> probe = BattleSpawnGate.RemoteGlobalVelocityProbe;
            Assert.NotNull(probe);

            Assert.Equal(new Vec2(3f, 4f), probe(remoteVictim));
            Assert.Null(probe(localVictim));

            component.AgentMovementHandler.Interpolator.Tick(0.51f);

            Assert.Null(probe(remoteVictim));
            GC.KeepAlive(controller);
        });
    }

    [Theory]
    [InlineData(true, 0.25f, 90f)]
    [InlineData(true, 0.5f, 80f)]
    [InlineData(true, 1f, 60f)]
    [InlineData(false, 0.25f, 60f)]
    public void RoutedDamage_UsesPlayerDamageMultiplierOnlyForMainAgent(
        bool isMainAgent,
        float damageToPlayerMultiplier,
        float expectedHealth)
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            mock.DamageToPlayerMultiplier = damageToPlayerMultiplier;
            var controller = client.Resolve<CoopBattleController>();
            var registry = client.Resolve<INetworkAgentRegistry>();

            var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(isMainAgent ? AgentControllerType.Player : AgentControllerType.AI));
            if (isMainAgent)
                mock.MainAgent = agent;

            var victimId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("owner", victimId, agent));

            var blow = new Blow(0) { InflictedDamage = 40, DamageType = DamageTypes.Pierce };
            var collisionData = new AttackCollisionData { InflictedDamage = 40 };
            client.Resolve<IMessageBroker>().Publish(
                this,
                new NetworkApplyBattleDamage(victimId, Guid.Empty, blow, collisionData));

            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.Equal(expectedHealth, mirror.Health);
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void RoutedMissileBlow_AppliesToOwner_WithoutMissilesDictionaryThrow()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopBattleController>(); // ctor subscribes to NetworkApplyBattleDamage
            var registry = client.Resolve<INetworkAgentRegistry>();

            BasicCharacterObject character = Game.Current.PlayerTroop;
            var agent = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));

            var victimId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("owner", victimId, agent)); // owner == this client's controller id

            // A missile blow whose source projectile has no matching reconstruction on this client. The owner's
            // handler must neutralize the missile flag before applying,
            // or the modeled Mission.OnAgentHit lookup throws KeyNotFound (swallowed by RunSafe -> no damage).
            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = 999;

            client.Resolve<IMessageBroker>().Publish(this, new NetworkApplyBattleDamage(victimId, Guid.Empty, blow, default));

            var field = typeof(CoopBattleController).GetField("damageRouter", BindingFlags.Instance | BindingFlags.NonPublic);
            var router = Assert.IsAssignableFrom<IBattleDamageRouter>(field?.GetValue(controller));
            for (int i = 0; i < 11; i++)
                router.Tick(0.05f);

            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.Equal(70f, mirror.Health); // 100 - 30: damage landed
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void RegisterBlow_MissileWithUnsyncedProjectile_Throws_ModelingOnAgentHit()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var agent = mock.SpawnAgent(new AgentBuildData(character));

            var blow = new Blow(0) { InflictedDamage = 10, DamageType = DamageTypes.Pierce };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = 999;

            // Documents WHY the owner must clear the missile flag: applying a missile blow whose projectile
            // isn't on this client reproduces the engine's _missilesDictionary KeyNotFound.
            Assert.Throws<KeyNotFoundException>(() => agent.RegisterBlow(blow, default));
        });
    }
}
