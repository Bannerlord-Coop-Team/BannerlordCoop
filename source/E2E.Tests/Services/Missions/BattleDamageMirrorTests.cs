using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Missiles.Message;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

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
    public void RoutedMissileBlow_DamageBeforeReconstruction_WaitsForPresentationAndPreservesHitOrder()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopBattleController>();
            var registry = client.Resolve<INetworkAgentRegistry>();

            BasicCharacterObject character = Game.Current.PlayerTroop;
            var victim = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var victimId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();
            const int sourceMissileIndex = 42;
            const long shotSequence = 73;
            Assert.True(registry.TryRegisterAgent("owner", victimId, victim));

            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = sourceMissileIndex;

            var broker = client.Resolve<IMessageBroker>();
            // A stale reconstruction using the same recycled native index must not satisfy this new shot.
            broker.Publish(this, new MissileReconstructed(attackerId, sourceMissileIndex, shotSequence - 1,
                new TaleWorlds.Library.Vec3(-3f, 0f, 0f), new TaleWorlds.Library.Vec3(1f, 0f, 0f), 60f, 60f));
            broker.Publish(this, new NetworkApplyBattleDamage(victimId, attackerId, blow, default,
                missileShotSequence: shotSequence));

            // A later hit to the same victim must queue behind the presentation-gated missile hit instead of
            // overtaking it and potentially changing which blow becomes lethal.
            var laterBlow = new Blow(0) { InflictedDamage = 20, DamageType = DamageTypes.Cut };
            broker.Publish(this, new NetworkApplyBattleDamage(victimId, attackerId, laterBlow, default));

            Assert.True(AgentMirror.TryGet(victim, out var beforeTick));
            Assert.Equal(100f, beforeTick.Health);

            var field = typeof(CoopBattleController).GetField("damageRouter", BindingFlags.Instance | BindingFlags.NonPublic);
            var router = Assert.IsAssignableFrom<IBattleDamageRouter>(field?.GetValue(controller));

            router.Tick(0.05f);
            Assert.True(AgentMirror.TryGet(victim, out var afterFirstTick));
            Assert.Equal(100f, afterFirstTick.Health);

            // The shot arrived after its damage. Its successful reconstruction moves the presentation deadline
            // forward; the next display tick must still hold both hits.
            broker.Publish(this, new MissileReconstructed(attackerId, sourceMissileIndex, shotSequence,
                new TaleWorlds.Library.Vec3(-3f, 0f, 0f), new TaleWorlds.Library.Vec3(1f, 0f, 0f), 60f, 60f));
            router.Tick(0.05f);
            Assert.True(AgentMirror.TryGet(victim, out var afterSecondTick));
            Assert.Equal(100f, afterSecondTick.Health);

            router.Tick(0.05f);
            Assert.True(AgentMirror.TryGet(victim, out var afterThirdTick));
            Assert.Equal(50f, afterThirdTick.Health);
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
