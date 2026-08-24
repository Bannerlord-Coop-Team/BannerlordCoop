using System;
using System.Linq;
using System.Threading;
using Common;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase E (mid-battle join): a client that enters an ALREADY-ACTIVATED battle must be told the battle is live.
/// <c>NetworkBattleActivated</c> is a one-shot broadcast sent the moment the battle goes live, so a client that
/// joins afterward never hears it and would otherwise sit at <c>activated=false</c> — its NPC puppets stay frozen
/// through its own deployment (they are spawned un-paused, but the gate state is still wrong) and a later promotion
/// to host wouldn't release the NPCs it adopts (<c>OnPromotedToHost</c> gates on activation). The fix re-tells the
/// joiner over the mesh from <c>CoopBattleController.SendJoinInfo</c>.
/// <para>
/// Drives two clients over the mesh: the host activates BEFORE the joiner's controller exists (so the joiner
/// genuinely misses the broadcast — the real mid-battle-join ordering), then the join handshake catches it up.
/// </para>
/// </summary>
public class BattleActivationJoinTests : MissionTestEnvironment
{
    public BattleActivationJoinTests(ITestOutputHelper output) : base(output) { }

    // The gate state lives in the controller's deployment coordinator, exposed for exactly this kind of
    // assertion — we assert the propagated state, not a side effect.
    private static bool IsActivated(CoopBattleController controller) => controller.Deployment.IsActivated;

    // What the server fans out to existing instance members when a new controller enters — it drives the host's
    // CoopMissionController.SendJoinInfo to the joiner over the mesh (join info + the activation catch-up).
    private static void TriggerJoinHandshake(EnvironmentInstance host, string joinerControllerId, string instanceId)
    {
        host.Call(() => host.Resolve<IMessageBroker>().Publish(host, new NetworkMissionPeerEntered(joinerControllerId, instanceId)));
    }

    [Fact]
    public void LateJoiner_IntoActivatedBattle_IsToldTheBattleIsLive()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        CoopBattleController hostController = null;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);                          // elect "host" + set the controller's instance id
        host.Call(() => hostController.OnDeploymentFinished()); // host commits -> battle activated (+ a broadcast the joiner misses)
        Assert.True(IsActivated(hostController));

        // The joiner's controller comes alive only NOW — AFTER activation — so it never saw the broadcast.
        CoopBattleController joinerController = null;
        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            joinerController = joiner.Resolve<CoopBattleController>();
            joinerController.Session.TryBegin(mapEventId);
        });
        Assert.False(IsActivated(joinerController), "precondition: a late joiner misses the one-shot activation broadcast");

        // The join handshake catches it up: the activated host re-sends NetworkBattleActivated to the joiner.
        TriggerJoinHandshake(host, "joiner", mapEventId);

        Assert.True(IsActivated(joinerController), "the join handshake should tell a late joiner the battle is already live");

        GC.KeepAlive(hostController);
        GC.KeepAlive(joinerController);
    }

    [Fact]
    public void LateJoiner_ReceivesOwnedAgentAndDeploymentSnapshotsBeforeBattleActivation()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();
        string characterId = CreateRegisteredObject<CharacterObject>();

        CoopBattleController hostController = null!;
        Agent ownedAgent = null!;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
        });
        EnterBattle(host, mapEventId);
        host.Call(() =>
        {
            Assert.True(host.ObjectManager.TryGetObject(characterId, out CharacterObject character));
            ownedAgent = Mission.Current.SpawnAgent(
                new AgentBuildData(character).Controller(AgentControllerType.AI));
            host.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(ownedAgent));
            hostController.OnDeploymentFinished();
        });

        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            joiner.Resolve<CoopBattleController>().Session.TryBegin(mapEventId);
        });
        host.Call(() =>
        {
            Assert.True(ownedAgent.IsActive());
            Assert.Single(host.Resolve<INetworkAgentRegistry>().GetAgents("host"));
        });
        var hostBattleNetwork = host.Resolve<MockBattleNetwork>();
        hostBattleNetwork.NetworkSentMessages.Clear();
        hostBattleNetwork.RouteMessages = false;

        host.Call(() =>
        {
            Exception failure = null!;
            bool finished = false;
            var thread = new Thread(() =>
            {
                try
                {
                    host.Resolve<IMessageBroker>().Publish(
                        host,
                        // This probe only inspects the sender's capture. Mesh routing is disabled above because
                        // switching to the in-process joiner would contend on the static game-instance lock.
                        new NetworkMissionPeerEntered("unrouted-joiner", mapEventId));
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    Volatile.Write(ref finished, true);
                }
            });
            thread.Start();
            while (!Volatile.Read(ref finished))
            {
                GameThread.Instance.Update(TimeSpan.Zero);
                Thread.Yield();
            }
            thread.Join();
            if (failure != null) throw failure;
        });

        int spawnIndex = hostBattleNetwork.NetworkSentMessages.Messages.FindIndex(
            message => message is NetworkSpawnBattleAgents);
        int deploymentIndex = hostBattleNetwork.NetworkSentMessages.Messages.FindIndex(
            message => message is NetworkBattleDeploymentFinished);
        int activationIndex = hostBattleNetwork.NetworkSentMessages.Messages.FindIndex(
            message => message is NetworkBattleActivated);
        Assert.InRange(spawnIndex, 0, int.MaxValue);
        var catchUp = Assert.IsType<NetworkSpawnBattleAgents>(
            hostBattleNetwork.NetworkSentMessages.Messages[spawnIndex]);
        Assert.Equal(SpawnBatchPurpose.CatchUp, catchUp.Purpose);
        Assert.InRange(deploymentIndex, 0, int.MaxValue);
        Assert.True(spawnIndex < activationIndex);
        Assert.True(deploymentIndex < activationIndex);

        GC.KeepAlive(hostController);
    }

    [Fact]
    public void InitialJoinBeforeActivation_DoesNotWaitForGameThreadPump()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();

        CoopBattleController hostController = null!;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
            hostController.Session.TryBegin(mapEventId);
        });

        Exception failure = null!;
        using var started = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            started.Set();
            try
            {
                host.Resolve<IMessageBroker>().Publish(
                    host,
                    new NetworkMissionPeerEntered("joiner", mapEventId));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.Start();
        started.Wait();
        bool completedWithoutPump = thread.Join(TimeSpan.FromSeconds(1));
        host.Call(() => GameThread.Instance.Update(TimeSpan.Zero));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "the join handler should finish after queued work drains");
        if (failure != null) throw failure;

        Assert.True(completedWithoutPump, "a pre-activation join must not block on the game-thread queue");
        GC.KeepAlive(hostController);
    }

    [Fact]
    public void Joiner_IntoNotYetActivatedBattle_IsNotToldActive()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        CoopBattleController hostController = null;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);                          // elected host, but deployment NOT committed -> not activated
        Assert.False(IsActivated(hostController));

        CoopBattleController joinerController = null;
        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            joinerController = joiner.Resolve<CoopBattleController>();
            joinerController.Session.TryBegin(mapEventId);
        });

        TriggerJoinHandshake(host, "joiner", mapEventId);

        Assert.False(IsActivated(joinerController), "a joiner into a not-yet-live battle must not be told it is active");

        GC.KeepAlive(hostController);
        GC.KeepAlive(joinerController);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LateJoiner_ResultSnapshot_ReportsAcrossHandshakeAndDeploymentOrder(
        bool resultBeforeHandshake,
        bool deploymentFirst)
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        CoopBattleController hostController = null;
        host.Call(() =>
        {
            fixture.CreateMission(host);
            hostController = host.Resolve<CoopBattleController>();
        });
        EnterBattle(host, mapEventId);

        var result = new MissionResult(
            BattleState.AttackerVictory,
            playerVictory: true,
            playerDefeated: false,
            enemyRetreated: false);
        if (resultBeforeHandshake)
            host.Call(() => hostController.ResultCommitter.ReportResolvedResult(result));

        CoopBattleController joinerController = null;
        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            joinerController = joiner.Resolve<CoopBattleController>();
            joinerController.Session.TryBegin(mapEventId);
            joiner.NetworkSentMessages.Clear();

            joiner.Resolve<IMessageBroker>().Publish(joiner, new NetworkBattleResultSnapshot(
                mapEventId,
                "joiner",
                joinerController.Session.HostEpoch,
                BattleState.DefenderVictory));
            joiner.Resolve<IMessageBroker>().Publish(host, new NetworkBattleResultSnapshot(
                mapEventId,
                "host",
                joinerController.Session.HostEpoch - 1,
                BattleState.DefenderVictory));
            GameThread.Run(() => { }, blocking: true);
            Assert.False(joinerController.ResultCommitter.TryGetResolvedState(out _));
        });

        if (deploymentFirst)
        {
            joiner.Call(() => joinerController.OnDeploymentFinished());
            Assert.Empty(joiner.NetworkSentMessages.GetMessages<NetworkBattleResultReady>());
        }

        TriggerJoinHandshake(host, "joiner", mapEventId);
        if (!resultBeforeHandshake)
            host.Call(() => hostController.ResultCommitter.ReportResolvedResult(result));

        joiner.Call(() => GameThread.Run(() => { }, blocking: true));

        if (!deploymentFirst)
        {
            Assert.Empty(joiner.NetworkSentMessages.GetMessages<NetworkBattleResultReady>());
            joiner.Call(() => joinerController.OnDeploymentFinished());
        }

        var report = Assert.Single(joiner.NetworkSentMessages.GetMessages<NetworkBattleResultReady>());
        Assert.Equal(BattleState.AttackerVictory, report.BattleState);

        GC.KeepAlive(hostController);
        GC.KeepAlive(joinerController);
    }

    [Fact]
    public void ResultSnapshot_AheadOfHostAssignment_IsAppliedAfterAssignment()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        host.Call(() => fixture.CreateMission(host));
        EnterBattle(host, mapEventId);

        joiner.Call(() =>
        {
            fixture.CreateMission(joiner);
            var controller = joiner.Resolve<CoopBattleController>();
            controller.Session.TryBegin(mapEventId);
            int nextEpoch = controller.Session.HostEpoch + 1;
            var broker = joiner.Resolve<IMessageBroker>();

            broker.Publish(host, new NetworkBattleResultSnapshot(
                mapEventId,
                "host",
                nextEpoch,
                BattleState.DefenderVictory));
            GameThread.Run(() => { }, blocking: true);
            Assert.False(controller.ResultCommitter.TryGetResolvedState(out _));

            broker.Publish(host, new NetworkBattleResultSnapshot(
                mapEventId,
                "host",
                controller.Session.HostEpoch,
                BattleState.AttackerVictory));
            GameThread.Run(() => { }, blocking: true);
            Assert.True(controller.ResultCommitter.TryGetResolvedState(out var currentState));
            Assert.Equal(BattleState.AttackerVictory, currentState);

            broker.Publish(host, new NetworkBattleHostAssigned(
                mapEventId,
                "host",
                Array.Empty<string>(),
                nextEpoch));
            GameThread.Run(() => { }, blocking: true);

            Assert.True(controller.ResultCommitter.TryGetResolvedState(out var state));
            Assert.Equal(BattleState.DefenderVictory, state);
        });
    }
}
