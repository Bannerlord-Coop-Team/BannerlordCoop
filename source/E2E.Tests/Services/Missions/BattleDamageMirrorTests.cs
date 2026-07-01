using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase B: the routed-damage application path against the mock engine. Drives the real
/// <see cref="CoopBattleController"/> damage handler so its missile handling (clearing the projectile flag
/// before re-applying a blow) is verified headlessly — the regression guard for the live
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

            // A missile blow whose projectile index is NOT in this client's mission (unsynced) — the exact
            // condition that crashed live. The owner's handler must neutralize the missile flag before applying,
            // or the modeled Mission.OnAgentHit lookup throws KeyNotFound (swallowed by RunSafe -> no damage).
            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            blow.WeaponRecord._isMissile = true;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = 999;

            client.Resolve<IMessageBroker>().Publish(this, new NetworkApplyBattleDamage(victimId, Guid.Empty, blow, default));

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
