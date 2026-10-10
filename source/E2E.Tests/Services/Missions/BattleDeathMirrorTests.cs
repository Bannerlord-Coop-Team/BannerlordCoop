using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Messages;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for replicated death presentation and kill-feed attribution.</summary>
public class BattleDeathMirrorTests : MissionTestEnvironment
{
    public BattleDeathMirrorTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedDeath_ReplicatesAffectorAndDeathAction_WhenLocalDeathGuardClears(bool rejectUntilNextTick)
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");

        string missionInstanceId = Guid.NewGuid().ToString();
        var victimId = Guid.NewGuid();
        var affectorId = Guid.NewGuid();
        Agent ownerVictim = null!, ownerAffector = null!, peerVictim = null!, peerAffector = null!;
        CoopBattleController ownerController = null!, peerController = null!;

        peer.Call(() =>
        {
            var mock = CreateConnectedMission(fixture, peer, missionInstanceId);
            mock.Shell.DisableDying = rejectUntilNextTick;
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
            var mock = CreateConnectedMission(fixture, owner, missionInstanceId);
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
        if (rejectUntilNextTick)
        {
            Assert.True(victimMirror.IsActive);
            Assert.Equal(100f, victimMirror.Health);
            peer.Call(() =>
            {
                Assert.True(peer.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(victimId, out _));
                victimMirror.Mission.DisableDying = false;
                peerController.OnMissionTick(0f);
            });
        }
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
    public void DeathWithMountAffector_AppliesWithoutReplayingAMissingWeaponSlot()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var victimId = Guid.NewGuid();
        var affectorId = Guid.NewGuid();
        Agent peerVictim = null!;
        Agent peerMount = null!;
        CoopBattleController peerController = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            peerController = peer.Resolve<CoopBattleController>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            peerVictim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            peerMount = mock.SpawnMount();
            Assert.True(registry.TryRegisterAgent("owner", victimId, peerVictim));
            Assert.True(registry.TryRegisterAgent("owner", affectorId, peerMount));

            peer.Resolve<IMessageBroker>().Publish(this,
                new NetworkBattleAgentDied(
                    victimId,
                    wounded: true,
                    affectorId,
                    inflictedDamage: 100,
                    victimBodyPart: BoneBodyPartType.Chest,
                    deathAction: 3587));

            Assert.False(registry.TryGetAgentInfo(victimId, out _));
            Assert.True(registry.TryGetAgentInfo(affectorId, out var affectorInfo));
            Assert.Same(peerMount, affectorInfo.Agent);
        });

        Assert.True(AgentMirror.TryGet(peerVictim, out var victimMirror));
        Assert.False(victimMirror.IsActive);
        Assert.False(victimMirror.WasKilled);
        Assert.Equal(3587, victimMirror.DeathAction);

        GC.KeepAlive(peerController);
    }

    [Theory]
    [InlineData(true, Agent.MortalityState.Mortal, MissionMode.Battle, false, 100f)]
    [InlineData(false, Agent.MortalityState.Immortal, MissionMode.Battle, false, 100f)]
    [InlineData(false, Agent.MortalityState.Mortal, MissionMode.Conversation, false, 100f)]
    [InlineData(false, Agent.MortalityState.Mortal, MissionMode.CutScene, false, 100f)]
    [InlineData(true, Agent.MortalityState.Mortal, MissionMode.Battle, true, 100f)]
    [InlineData(false, Agent.MortalityState.Immortal, MissionMode.Battle, true, 100f)]
    [InlineData(false, Agent.MortalityState.Mortal, MissionMode.Conversation, true, 100f)]
    [InlineData(false, Agent.MortalityState.Mortal, MissionMode.CutScene, true, 100f)]
    [InlineData(true, Agent.MortalityState.Mortal, MissionMode.Battle, false, 1f)]
    [InlineData(true, Agent.MortalityState.Mortal, MissionMode.Battle, true, 1f)]
    public void RejectedDeath_RetainsPuppetAndAttributionUntilRetrySucceeds(
        bool disableDying, Agent.MortalityState mortality, MissionMode mode, bool wounded, float health)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            var casualties = new CasualtyAttributionMap();
            var mountRepairer = new Mock<IPuppetMountStateRepairer>();
            using var applier = new PuppetDeathApplier(
                broker, peer.Resolve<ICoopMissionComponent>(), casualties, mountRepairer.Object);
            var victimId = Guid.NewGuid();
            var victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            Assert.True(registry.TryRegisterAgent("owner", victimId, victim));
            casualties.Record(victimId, "party", 7, "troop");
            mirror.Health = health;
            mock.Shell.DisableDying = disableDying;
            mock.Shell._missionMode = mode;
            victim.SetMortalityState(mortality);
            var death = new NetworkBattleAgentDied(
                victimId, wounded, Guid.Empty, 100, BoneBodyPartType.Head, 456);

            int registeredBlows = 0;
            mock.RegisteredBlow = (_, _) => registeredBlows++;
            broker.Publish(this, death);
            applier.DrainPendingDeaths();
            applier.DrainPendingDeaths();
            applier.DrainPendingDeaths();

            Assert.Equal(0, registeredBlows);
            Assert.True(mirror.IsActive);
            Assert.Equal(health, mirror.Health);
            Assert.True(registry.TryGetAgentInfo(victimId, out var info));
            Assert.Same(victim, info.Agent);
            Assert.Equal("party", casualties.GetOrDefault(victimId).MapEventPartyId);
            mountRepairer.Verify(x => x.RepairAfterRiderDeath(It.IsAny<Agent>()), Times.Never);

            mock.Shell.DisableDying = false;
            mock.Shell._missionMode = MissionMode.Battle;
            victim.SetMortalityState(Agent.MortalityState.Mortal);
            applier.DrainPendingDeaths();

            Assert.False(mirror.IsActive);
            Assert.Equal(!wounded, mirror.WasKilled);
            Assert.Equal(456, mirror.DeathAction);
            Assert.False(registry.TryGetAgentInfo(victimId, out _));
            Assert.Null(casualties.GetOrDefault(victimId).MapEventPartyId);

            Assert.Equal(1, registeredBlows);
            broker.Publish(this, death);
            applier.DrainPendingDeaths();
            applier.DrainPendingDeaths();
            Assert.Equal(1, registeredBlows);
            mountRepairer.Verify(x => x.RepairAfterRiderDeath(It.IsAny<Agent>()), Times.Once);
        });
    }

    [Theory]
    [InlineData(false, 0f, false)]
    [InlineData(true, 0f, false)]
    [InlineData(false, 0f, true)]
    [InlineData(true, 0f, true)]
    [InlineData(false, 0.5f, true)]
    [InlineData(true, 0.5f, true)]
    [InlineData(false, 0.999f, true)]
    [InlineData(true, 0.999f, true)]
    public void ActiveBelowOneHealthPuppet_ReceivesTerminalBlowBeforeDeregistration(
        bool wounded, float health, bool guardsEnabled)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetDeathApplier(
                broker, peer.Resolve<ICoopMissionComponent>(), new CasualtyAttributionMap(),
                peer.Resolve<IPuppetMountStateRepairer>());
            var victimId = Guid.NewGuid();
            var victim = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(victim, out var mirror));
            mirror.Health = health;
            mock.Shell.DisableDying = guardsEnabled;
            mock.Shell._missionMode = guardsEnabled ? MissionMode.Conversation : MissionMode.Battle;
            victim.SetMortalityState(guardsEnabled ? Agent.MortalityState.Immortal : Agent.MortalityState.Mortal);
            Assert.True(registry.TryRegisterAgent("owner", victimId, victim));

            broker.Publish(this, new NetworkBattleAgentDied(
                victimId, wounded, Guid.Empty, 0, BoneBodyPartType.Head, 456));

            Assert.False(mirror.IsActive);
            Assert.Equal(!wounded, mirror.WasKilled);
            Assert.Equal(456, mirror.DeathAction);
            Assert.False(registry.TryGetAgentInfo(victimId, out _));
        });
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
                new CasualtyAttributionMap(),
                peer.Resolve<IPuppetMountStateRepairer>());

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
