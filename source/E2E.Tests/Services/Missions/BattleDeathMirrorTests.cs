using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for replicated death presentation and kill-feed attribution.</summary>
public class BattleDeathMirrorTests : MissionTestEnvironment
{
    public BattleDeathMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OwnedDeath_ReplicatesAffectorAndDeathAction_WhilePreservingKilledState()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");

        var victimId = Guid.NewGuid();
        var affectorId = Guid.NewGuid();
        Agent ownerVictim = null!, ownerAffector = null!, peerVictim = null!, peerAffector = null!;
        CoopBattleController ownerController = null!, peerController = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            peerController = peer.Resolve<CoopBattleController>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            peerVictim = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            peerAffector = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            Assert.True(registry.TryRegisterAgent("owner", victimId, peerVictim));
            Assert.True(registry.TryRegisterAgent("peer", affectorId, peerAffector));
        });

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            ownerController = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            BasicCharacterObject character = Game.Current.PlayerTroop;
            ownerVictim = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            ownerAffector = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", victimId, ownerVictim));
            Assert.True(registry.TryRegisterAgent("peer", affectorId, ownerAffector));

            var blow = new Blow(ownerAffector.Index)
            {
                InflictedDamage = 87,
                DamageType = DamageTypes.Pierce,
                VictimBodyPart = BoneBodyPartType.Head,
            };
            owner.Resolve<IMessageBroker>().Publish(this,
                new BattleAgentDied(
                    ownerVictim,
                    ownerAffector,
                    wounded: false,
                    blow.InflictedDamage,
                    blow.VictimBodyPart,
                    deathAction: 321));
        });

        var message = Assert.Single(peer.InternalMessages.GetMessages<NetworkBattleAgentDied>());
        Assert.Equal(victimId, message.AgentId);
        Assert.Equal(affectorId, message.AffectorAgentId);
        Assert.Equal(321, message.DeathAction);
        Assert.Equal(87, message.InflictedDamage);
        Assert.Equal(BoneBodyPartType.Head, message.VictimBodyPart);

        Assert.True(AgentMirror.TryGet(peerVictim, out var victimMirror));
        Assert.False(victimMirror.IsActive);
        Assert.True(victimMirror.WasKilled);
        Assert.Equal(321, victimMirror.DeathAction);

        peer.Call(() =>
        {
            var registry = peer.Resolve<INetworkAgentRegistry>();
            Assert.False(registry.TryGetAgentInfo(victimId, out _));
            Assert.True(registry.TryGetAgentInfo(affectorId, out var affectorInfo));
            Assert.Same(peerAffector, affectorInfo.Agent);
        });

        GC.KeepAlive(ownerController);
        GC.KeepAlive(peerController);
    }

    [Fact]
    public void DeathWithMissingAffector_StillAppliesAndDeregistersThePuppet()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var victimId = Guid.NewGuid();
        Agent peerVictim = null!;
        CoopBattleController peerController = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            peerController = peer.Resolve<CoopBattleController>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            peerVictim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", victimId, peerVictim));

            peer.Resolve<IMessageBroker>().Publish(this,
                new NetworkBattleAgentDied(
                    victimId,
                    wounded: true,
                    Guid.NewGuid(),
                    inflictedDamage: 100,
                    victimBodyPart: BoneBodyPartType.Neck,
                    deathAction: 222));

            Assert.False(registry.TryGetAgentInfo(victimId, out _));
        });

        Assert.True(AgentMirror.TryGet(peerVictim, out var victimMirror));
        Assert.False(victimMirror.IsActive);
        Assert.False(victimMirror.WasKilled);
        Assert.Equal(222, victimMirror.DeathAction);

        GC.KeepAlive(peerController);
    }

    [Fact]
    public void DeathBeforeRegistration_AppliesWhenPendingDeathsDrain()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var victimId = Guid.NewGuid();
        Agent peerVictim = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetDeathApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap());

            broker.Publish(this,
                new NetworkBattleAgentDied(
                    victimId,
                    wounded: false,
                    Guid.Empty,
                    inflictedDamage: 100,
                    victimBodyPart: BoneBodyPartType.Head,
                    deathAction: 456));

            applier.DrainPendingDeaths();

            peerVictim = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", victimId, peerVictim));

            applier.DrainPendingDeaths();
            Assert.False(registry.TryGetAgentInfo(victimId, out _));
        });

        Assert.True(AgentMirror.TryGet(peerVictim, out var victimMirror));
        Assert.False(victimMirror.IsActive);
        Assert.True(victimMirror.WasKilled);
        Assert.Equal(456, victimMirror.DeathAction);
    }
}
