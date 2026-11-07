using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Common;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.DotNet;
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

    // Goes through the real native entry point, since a team AI retreat reaches the flag no other way.
    [Fact]
    public void NativeRetreat_ReachesPeers_FlagOnly_AndStopRetreatingClearsIt()
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

            InvokeNativeRetreatCallback(ownedAgent, true);
            Assert.True(ownedAgent.IsRunningAway);
            InvokeNativeRetreatCallback(ownedAgent, true);
            FlushGameThread(ownerController);
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

        // StopRetreating's native half can't run here, so it is disabled; the patches on it still run.
        owner.Call(() =>
        {
            InCoopBattle(ownedAgent.StopRetreating);
            FlushGameThread(ownerController);
        }, new[] { AccessTools.Method(typeof(Agent), nameof(Agent.StopRetreating)) });

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
    public void RallyRightAfterAuthorityTransfer_ReachesPeers()
    {
        using var fixture = new MissionEngineFixture();
        var adopter = Clients.First();
        var observer = Clients.Skip(1).First();
        SetControllerId(adopter, "adopter");
        SetControllerId(observer, "observer");

        string missionInstanceId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        CoopBattleController observerController = null!;
        observer.Call(() =>
        {
            CreateConnectedMission(fixture, observer, missionInstanceId);
            observerController = observer.Resolve<CoopBattleController>();
        });

        adopter.Call(() =>
        {
            var mock = CreateConnectedMission(fixture, adopter, missionInstanceId);
            var controller = adopter.Resolve<CoopBattleController>();
            var registry = adopter.Resolve<INetworkAgentRegistry>();
            controller.Session.TryBegin(missionInstanceId);

            // A fleeing puppet of the previous owner, as every peer last heard it.
            var agent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("previous-owner", agentId, agent));
            adopter.Resolve<IMessageBroker>().Publish(this, new NetworkBattleAgentRetreatState(agentId, true));
            GameThread.Run(() => { }, blocking: true);
            Assert.True(agent.IsRunningAway);
            Assert.True(registry.TryTransferAuthority("adopter", agentId));

            InvokeNativeRetreatCallback(agent, false);
            FlushGameThread(controller);
            GC.KeepAlive(controller);
        });

        var state = Assert.Single(observer.InternalMessages.GetMessages<NetworkBattleAgentRetreatState>());
        Assert.Equal(agentId, state.AgentId);
        Assert.False(state.IsRunningAway);
        GC.KeepAlive(observerController);
    }

    [Fact]
    public void AuthorityRefresh_AppliesTheNewOwnersFlag_WithoutFleeingSideEffects()
    {
        using var fixture = new MissionEngineFixture();
        var observer = Clients.First();
        SetControllerId(observer, "observer");

        var agentId = Guid.NewGuid();
        observer.Call(() =>
        {
            var mock = CreateConnectedMission(fixture, observer, Guid.NewGuid().ToString());
            var controller = observer.Resolve<CoopBattleController>();
            var broker = observer.Resolve<IMessageBroker>();
            var puppet = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(observer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("previous-owner", agentId, puppet));
            broker.Publish(this, new NetworkBattleAgentRetreatState(agentId, true));
            GameThread.Run(() => { }, blocking: true);
            Assert.True(puppet.IsRunningAway);

            var refresh = new BattleAgentSpawnData(
                agentId,
                "character",
                default,
                BattleSideEnum.Defender,
                100f,
                "adopter",
                "party",
                1,
                new Equipment(),
                new BodyProperties(),
                new MissionEquipmentData(new()),
                originalOwnerControllerId: "previous-owner",
                movementScopeId: "previous-owner",
                isRunningAway: false,
                authorityRevision: 1);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { refresh }));
            GameThread.Run(() => { }, blocking: true);

            Assert.False(puppet.IsRunningAway);
            Assert.Equal(0, mock.AgentFleeingCalls);
            GC.KeepAlive(controller);
        });
    }

    // A panic the owner announced after a rally has to win, whichever order the two buffers happen to drain in.
    [Fact]
    public void BufferedRallyThenPanic_LeavesThePuppetRunningAway()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());

            var agentId = Guid.NewGuid();
            broker.Publish(this, new NetworkBattleAgentRetreatState(agentId, false));
            broker.Publish(this, new NetworkBattleAgentFleeing(agentId));
            GameThread.Run(() => { }, blocking: true);

            var puppet = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", agentId, puppet));
            applier.DrainPendingRouts();

            Assert.True(puppet.IsRunningAway);
            Assert.Equal(1, mock.AgentFleeingCalls);
        });
    }

    // The puppet registers between the two messages, so the newer one applies directly while the older is still buffered.
    [Fact]
    public void DirectlyAppliedPanic_RetiresAnOlderBufferedRally()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());

            var agentId = Guid.NewGuid();
            broker.Publish(this, new NetworkBattleAgentRetreatState(agentId, false));
            GameThread.Run(() => { }, blocking: true);

            var puppet = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", agentId, puppet));
            broker.Publish(this, new NetworkBattleAgentFleeing(agentId));
            GameThread.Run(() => { }, blocking: true);
            applier.DrainPendingRouts();

            Assert.True(puppet.IsRunningAway);
        });
    }

    [Fact]
    public void PanicAfterTeamAiRetreat_RunsItsSideEffectsOnce_AndAgainAfterARally()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap(),
                new BattleAgentFader());

            var agentId = Guid.NewGuid();
            var puppet = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            puppet.Formation = mock.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", agentId, puppet));

            broker.Publish(this, new NetworkBattleAgentRetreatState(agentId, true));
            GameThread.Run(() => { }, blocking: true);
            Assert.Equal(0, mock.AgentFleeingCalls);
            Assert.True(AgentMirror.TryGet(puppet, out var agentMirror));
            Assert.NotNull(agentMirror.Formation);

            broker.Publish(this, new NetworkBattleAgentFleeing(agentId));
            broker.Publish(this, new NetworkBattleAgentFleeing(agentId));
            GameThread.Run(() => { }, blocking: true);
            Assert.Equal(1, mock.AgentFleeingCalls);
            Assert.Null(agentMirror.Formation);

            broker.Publish(this, new NetworkBattleAgentRetreatState(agentId, false));
            broker.Publish(this, new NetworkBattleAgentFleeing(agentId));
            GameThread.Run(() => { }, blocking: true);
            Assert.Equal(2, mock.AgentFleeingCalls);
            Assert.True(puppet.IsRunningAway);
        });
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

    // Calls the generated dispatcher native reaches, which resolves the agent from the engine's managed object table.
    private static void InvokeNativeRetreatCallback(Agent agent, bool isRetreating)
    {
        int objectId = RegisterNativeObject(agent);
        var dispatcher = Assembly.Load("TaleWorlds.MountAndBlade.AutoGenerated")
            .GetType("ManagedCallbacks.CoreCallbacksGenerated")
            .GetMethod("Agent_SetAgentAIPerformingRetreatBehavior", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        // Harmony compiles the patched dispatcher before this fixture shims Agent.Mission, so it reads the real property.
        AccessTools.Property(typeof(Agent), nameof(Agent.Mission)).SetValue(agent, Mission.Current);
        InCoopBattle(() => dispatcher.Invoke(null, new object[] { objectId, isRetreating }));
    }

    // The retreat patches only publish while a coop battle is active.
    private static void InCoopBattle(Action action)
    {
        BattleSpawnGate.BeginBattle("rout-mirror-test");
        try
        {
            action();
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    private static int RegisterNativeObject(Agent agent)
    {
        var objectIdField = AccessTools.Field(typeof(DotNetObject), "_objectId");
        var references = (IDictionary)AccessTools.Field(typeof(DotNetObject), "DotnetObjectReferences").GetValue(null);
        int objectId = (int)objectIdField.GetValue(agent);
        if (objectId != 0 && references.Contains(objectId)) return objectId;

        objectId = int.MaxValue - Interlocked.Increment(ref nextNativeObjectOffset);
        objectIdField.SetValue(agent, objectId);
        var counterType = AccessTools.Inner(typeof(DotNetObject), "DotNetObjectReferenceCounter");
        object counter = Activator.CreateInstance(counterType);
        AccessTools.Field(counterType, "DotNetObject").SetValue(counter, agent);
        references[objectId] = counter;
        return objectId;
    }

    // Runs what the patches marshalled, then a tick, so a send that needs either one is counted.
    private static void FlushGameThread(CoopBattleController controller)
    {
        GameThread.Run(() => { }, blocking: true);
        controller.OnMissionTick(1f);
    }

    private static int nextNativeObjectOffset;
}
