using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for peer-side battle rout application.</summary>
public class BattleRoutMirrorTests : MissionTestEnvironment
{
    public BattleRoutMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RoutBeforeRegistration_AppliesWhenPendingRoutsDrain()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();
        Agent peerAgent = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());

            broker.Publish(this, new NetworkBattleAgentRouted(agentId));
            applier.DrainPendingRouts();

            peerAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, peerAgent));

            applier.DrainPendingRouts();
            Assert.False(registry.TryGetAgentInfo(agentId, out _));
        });

        Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
        Assert.False(agentMirror.IsActive);
        Assert.False(agentMirror.WasKilled);
    }

    [Fact]
    public void FleeingBeforeRegistration_AppliesWhenPendingRoutsDrain()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();
        Agent peerAgent = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());

            broker.Publish(this, new NetworkBattleAgentFleeing(agentId));
            applier.DrainPendingRouts();

            peerAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            peerAgent.Formation = mock.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            Assert.True(registry.TryRegisterAgent("owner", agentId, peerAgent));

            applier.DrainPendingRouts();

            Assert.True(peerAgent.IsRunningAway);
            Assert.Equal(1, mock.AgentFleeingCalls);
            Assert.Same(peerAgent, mock.LastFleeingAgent);
            Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
            Assert.Equal(1, agentMirror.OnFleeingCalls);
            Assert.Null(agentMirror.Formation);
            Assert.True(registry.TryGetAgentInfo(agentId, out _));
        });

        Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
        Assert.True(agentMirror.IsActive);
        Assert.False(agentMirror.WasKilled);
    }

    [Fact]
    public void RoutOfASeatedPuppet_ReleasesItsStationBeforeTheFade()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();
        Agent peerAgent = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());

            peerAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, peerAgent));
            peerAgent.UseGameObject(CreateStandingPoint());
            Assert.NotNull(peerAgent.CurrentlyUsedGameObject);

            broker.Publish(this, new NetworkBattleAgentRouted(agentId));
            applier.DrainPendingRouts();
        });

        Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
        Assert.Null(agentMirror.CurrentlyUsedGameObject);
        Assert.False(agentMirror.IsActive);
        Assert.False(agentMirror.WasKilled);
    }

    [Fact]
    public void OnAgentFleeing_BroadcastsOnlyForLocallyAuthoritativeAgent()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var observer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(observer, "observer");

        string missionInstanceId = Guid.NewGuid().ToString();
        observer.Call(() => CreateConnectedMission(fixture, observer, missionInstanceId));

        owner.Call(() =>
        {
            var mock = CreateConnectedMission(fixture, owner, missionInstanceId);
            var controller = owner.Resolve<CoopBattleController>();
            var registry = owner.Resolve<INetworkAgentRegistry>();

            var ownedAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), ownedAgent));
            controller.OnAgentFleeing(ownedAgent);

            var remoteAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("remote", Guid.NewGuid(), remoteAgent));
            controller.OnAgentFleeing(remoteAgent);
        });

        Assert.Equal(1, observer.InternalMessages.GetMessageCount<NetworkBattleAgentFleeing>());
    }

    // ScriptComponentBehavior's type initializer reads the managed module type catalog the engine normally fills; run
    // first without it, the initializer fails for every later test in the process.
    private static StandingPoint CreateStandingPoint()
    {
        var managed = typeof(TaleWorlds.Engine.ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes");
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }
}
