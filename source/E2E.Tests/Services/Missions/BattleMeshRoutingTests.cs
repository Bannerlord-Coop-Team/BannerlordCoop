using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Battles;
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

            // What BattleBlowInterceptPatch publishes when a local troop hits a puppet: route the blow to the
            // puppet's owner over the mesh. The handler runs synchronously and delivers to the owner inline.
            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            attacker.Resolve<IMessageBroker>().Publish(this, new BattlePuppetHit(victimPuppetOnAttacker, null, blow, default));
        });

        Assert.True(AgentMirror.TryGet(victimOnOwner, out var ownerMirror));
        Assert.Equal(70f, ownerMirror.Health); // routed over the mesh and applied by the owner

        Assert.True(AgentMirror.TryGet(victimPuppetOnAttacker, out var puppetMirror));
        Assert.Equal(100f, puppetMirror.Health); // never applied on the attacker — the puppet's life is the owner's

        GC.KeepAlive(ownerController);
        GC.KeepAlive(attackerController);
    }
}
