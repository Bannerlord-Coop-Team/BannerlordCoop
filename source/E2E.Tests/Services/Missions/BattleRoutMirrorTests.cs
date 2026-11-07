using System;
using System.Linq;
using Common;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
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
            peerAgent.UseGameObject(MissionTestObjects.CreateStandingPoint());
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

    // BattleEndLogic reads this flag, and a team AI retreat sets it without Mission.OnAgentFleeing, so only the scan can see it.
    [Fact]
    public void RetreatStateScan_MirrorsARetreatAndARally_WithoutFleeingSideEffects()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var observer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(observer, "observer");

        string missionInstanceId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        Agent puppet = null!;
        MockMission observerMission = null!;
        CoopBattleController observerController = null!;
        observer.Call(() =>
        {
            observerMission = CreateConnectedMission(fixture, observer, missionInstanceId);
            observerController = observer.Resolve<CoopBattleController>();
            puppet = observerMission.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            puppet.Formation = observerMission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            Assert.True(observer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", agentId, puppet));
        });

        Agent ownedAgent = null!;
        CoopBattleController ownerController = null!;
        owner.Call(() =>
        {
            var mock = CreateConnectedMission(fixture, owner, missionInstanceId);
            ownerController = owner.Resolve<CoopBattleController>();
            ownerController.Session.TryBegin(missionInstanceId);
            ownedAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            Assert.True(owner.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", agentId, ownedAgent));

            ownedAgent.IsRunningAway = true;
            ownerController.OnMissionTick(1f);
            ownerController.OnMissionTick(1f);
        });

        Assert.Equal(1, observer.InternalMessages.GetMessageCount<NetworkBattleAgentRetreatState>());
        observer.Call(() =>
        {
            GameThread.Run(() => { }, blocking: true);
            Assert.True(puppet.IsRunningAway);
            Assert.Equal(0, observerMission.AgentFleeingCalls);
            Assert.True(AgentMirror.TryGet(puppet, out var agentMirror));
            Assert.NotNull(agentMirror.Formation);
        });

        owner.Call(() =>
        {
            ownedAgent.IsRunningAway = false;
            ownerController.OnMissionTick(1f);
        });

        Assert.Equal(2, observer.InternalMessages.GetMessageCount<NetworkBattleAgentRetreatState>());
        observer.Call(() =>
        {
            GameThread.Run(() => { }, blocking: true);
            Assert.False(puppet.IsRunningAway);
        });

        GC.KeepAlive(ownerController);
        GC.KeepAlive(observerController);
    }

    [Fact]
    public void RetreatStateScan_DoesNotRepeatAFleeingAlreadyAnnounced()
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
            controller.Session.TryBegin(missionInstanceId);
            var agent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            Assert.True(owner.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", Guid.NewGuid(), agent));

            agent.IsRunningAway = true;
            controller.OnAgentFleeing(agent);
            controller.OnMissionTick(1f);
        });

        Assert.Equal(1, observer.InternalMessages.GetMessageCount<NetworkBattleAgentFleeing>());
        Assert.Equal(0, observer.InternalMessages.GetMessageCount<NetworkBattleAgentRetreatState>());
    }

    [Fact]
    public void RetreatStateBeforeRegistration_AppliesTheLatestWhenPendingRoutsDrain()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

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

            var runningAwayId = Guid.NewGuid();
            var ralliedId = Guid.NewGuid();
            broker.Publish(this, new NetworkBattleAgentRetreatState(runningAwayId, true));
            broker.Publish(this, new NetworkBattleAgentRetreatState(ralliedId, true));
            broker.Publish(this, new NetworkBattleAgentRetreatState(ralliedId, false));
            GameThread.Run(() => { }, blocking: true);

            var runningAway = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            var rallied = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", runningAwayId, runningAway));
            Assert.True(registry.TryRegisterAgent("owner", ralliedId, rallied));

            applier.DrainPendingRouts();

            Assert.True(runningAway.IsRunningAway);
            Assert.False(rallied.IsRunningAway);
        });
    }
}
