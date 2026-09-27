using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Save.Messages;
using Coop.Core.Server.Services.Shutdown;
using Coop.Core.Server.Services.Shutdown.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Messages;
using LiteNetLib;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Shutdown;

public class ServerShutdownCoordinatorTests : IDisposable
{
    private const string LoadedSave = "MP";
    private const string Restarting = "ServerRestarting";

    private readonly TestMessageBroker broker = new TestMessageBroker();
    private readonly Mock<INetwork> network = new Mock<INetwork>();
    private readonly Mock<IConnectionCollection> connections = new Mock<IConnectionCollection>();
    private readonly Mock<IJoinPeerTerminator> terminator = new Mock<IJoinPeerTerminator>();
    private readonly ServerAdmissionGate gate = new ServerAdmissionGate();
    private readonly Mock<ISaveInterface> saves = new Mock<ISaveInterface>();
    private readonly Mock<IMissionManager> missions = new Mock<IMissionManager>();
    private readonly List<IConnectionLogic> connectionList = new List<IConnectionLogic>();
    private readonly List<(NetPeer Peer, string Reason)> disconnects = new List<(NetPeer, string)>();
    private readonly List<string> queuedSaves = new List<string>();
    private readonly List<Action> gameThreadActions = new List<Action>();
    private readonly ServerShutdownCoordinator coordinator;

    private DateTime now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private bool isSaving;
    private bool peersLeaveOnDisconnect = true;
    private int tickersStarted;
    private int tickersStopped;

    public ServerShutdownCoordinatorTests()
    {
        connections.Setup(collection => collection.GetEnumerator())
            .Returns(() => connectionList.ToList().GetEnumerator());
        terminator.Setup(value => value.Disconnect(It.IsAny<NetPeer>(), It.IsAny<string>()))
            .Callback<NetPeer, string>((peer, reason) =>
            {
                disconnects.Add((peer, reason));
                if (peersLeaveOnDisconnect) connectionList.RemoveAll(connection => connection.Peer == peer);
            });
        saves.SetupGet(value => value.CanQueueSave).Returns(true);
        saves.SetupGet(value => value.IsSaving).Returns(() => isSaving);
        saves.Setup(value => value.TryQueueSave(It.IsAny<string>()))
            .Callback<string>(queuedSaves.Add)
            .Returns(true);
        missions.Setup(value => value.GetDiagnostics())
            .Returns(new MissionManagerDiagnostics(2, 2, 0, 0, 1, 0, 0, 0));

        coordinator = new ServerShutdownCoordinator(
            broker,
            network.Object,
            connections.Object,
            terminator.Object,
            gate,
            saves.Object,
            missions.Object,
            () => now,
            _ =>
            {
                tickersStarted++;
                return new Stopper(() => tickersStopped++);
            },
            gameThreadActions.Add);

        broker.Publish(new object(), new GameLoaded(LoadedSave));
    }

    public void Dispose() => coordinator.Dispose();

    [Theory]
    [InlineData(75, new[] { "75 seconds", "1 minute", "10 seconds" })]
    [InlineData(300, new[] { "5 minutes", "1 minute", "10 seconds" })]
    [InlineData(400, new[] { "400 seconds", "5 minutes", "1 minute", "10 seconds" })]
    [InlineData(10, new[] { "10 seconds" })]
    public void Countdown_AnnouncesAtSchedulingAnd300And60And10SecondsOnce(int seconds, string[] expected)
    {
        AddPlayer();

        Schedule(seconds);
        TickFor(seconds - 1);

        Assert.Equal(expected.Select(delay => $"The server will restart in {delay}."), Notices());
        Assert.Equal(ServerShutdownPhase.Countdown, coordinator.Phase);
        network.Verify(value => value.SendAll(It.IsAny<SendInformationMessage>()), Times.Exactly(expected.Length));
    }

    [Fact]
    public void Countdown_SkipsNoticesThatAStalledGameThreadMissed()
    {
        AddPlayer();
        Schedule(400);

        Advance(395);

        // 300, 60 and 10 were all crossed in one tick; only the nearest one is shown.
        Assert.Equal(new[] { "The server will restart in 400 seconds.", "The server will restart in 10 seconds." }, Notices());
    }

    [Fact]
    public void JoinGate_ClosesWith120SecondsLeft()
    {
        AddPlayer();
        Schedule(200);

        TickFor(79);
        Assert.True(gate.IsOpen);

        Advance(1);
        Assert.False(gate.IsOpen);
    }

    [Fact]
    public void JoinGate_ShortCountdown_ClosesAtOnce()
    {
        AddPlayer();

        Schedule(120);

        Assert.False(gate.IsOpen);
        Assert.Equal(ServerShutdownPhase.Countdown, coordinator.Phase);
    }

    [Fact]
    public void ZeroPlayers_SkipsTheCountdownAndSaves()
    {
        Schedule(300);

        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);
        Assert.Equal(new[] { LoadedSave }, queuedSaves);
        Assert.False(gate.IsOpen);
    }

    [Fact]
    public void LastPlayerLeaving_SkipsTheRestOfTheCountdown()
    {
        var connection = AddPlayer();
        Schedule(600);

        connectionList.Remove(connection);
        Advance(1);

        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);
        Assert.Equal(new[] { LoadedSave }, queuedSaves);
    }

    [Fact]
    public void TimeUp_DisconnectsEveryConnectionIncludingJoinersWithTheRestartingCode()
    {
        NetPeer playing = AddPlayer().Peer;
        NetPeer joining = AddPlayer(isLoading: true).Peer;
        Schedule(30);

        TickFor(30);

        Assert.Equal(new[] { (playing, Restarting), (joining, Restarting) }, disconnects);
        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);
        missions.Verify(value => value.GetDiagnostics(), Times.Once);
    }

    [Fact]
    public void Draining_WaitsForConnectionsToLeaveAndAnEarlierSaveToEnd()
    {
        peersLeaveOnDisconnect = false;
        var connection = AddPlayer();
        isSaving = true;
        Schedule(0);

        Assert.Equal(ServerShutdownPhase.Draining, coordinator.Phase);
        Advance(1);
        Assert.Empty(queuedSaves);

        connectionList.Remove(connection);
        Advance(1);
        Assert.Empty(queuedSaves);

        isSaving = false;
        Advance(1);
        Assert.Equal(new[] { LoadedSave }, queuedSaves);
        Assert.Single(disconnects);
    }

    [Fact]
    public void Draining_EarlierSaveStillRunningAfter15Seconds_Fails()
    {
        isSaving = true;
        Schedule(0);

        TickFor(14);
        Assert.Equal(ServerShutdownPhase.Draining, coordinator.Phase);

        Advance(1);
        AssertFailed("an earlier save was still running after 15 seconds");
        Assert.Empty(queuedSaves);
    }

    [Fact]
    public void Draining_ConnectionStillListedAfter15Seconds_SavesAnyway()
    {
        peersLeaveOnDisconnect = false;
        AddPlayer();
        Schedule(0);

        TickFor(14);
        Assert.Empty(queuedSaves);

        Advance(1);
        Assert.Equal(new[] { LoadedSave }, queuedSaves);
    }

    [Fact]
    public void Draining_ConnectionAcceptedJustBeforeTheGateClosed_IsDisconnectedToo()
    {
        peersLeaveOnDisconnect = false;
        var first = AddPlayer();
        Schedule(0);

        var racer = AddPlayer();
        Advance(1);

        Assert.Equal(new[] { first.Peer, racer.Peer }, disconnects.Select(entry => entry.Peer));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Saving_CompletesOnlyOnceBothResultsArrive(bool sessionFirst)
    {
        Schedule(0);

        broker.Publish(new object(), new GameSaved(LoadedSave));
        if (sessionFirst) broker.Publish(new object(), new CoopSessionWritten(LoadedSave, true));
        else broker.Publish(new object(), new GameSaveCompleted(LoadedSave, true));
        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);

        if (sessionFirst) broker.Publish(new object(), new GameSaveCompleted(LoadedSave, true));
        else broker.Publish(new object(), new CoopSessionWritten(LoadedSave, true));

        Assert.Equal(ServerShutdownPhase.Completed, coordinator.Phase);
        var completed = Assert.Single(broker.Messages.GetMessages<ServerShutdownCompleted>());
        Assert.Equal(LoadedSave, completed.SaveName);
        Assert.Empty(broker.Messages.GetMessages<ServerShutdownFailed>());
        Assert.Equal(tickersStarted, tickersStopped);
        Assert.False(gate.IsOpen);
    }

    [Fact]
    public void Saving_GameSaveFailed_Fails()
    {
        Schedule(0);
        broker.Publish(new object(), new CoopSessionWritten(LoadedSave, true));

        broker.Publish(new object(), new GameSaveCompleted(LoadedSave, false));

        AssertFailed("the game save failed");
    }

    [Fact]
    public void Saving_SessionWriteFailed_Fails()
    {
        Schedule(0);

        broker.Publish(new object(), new CoopSessionWritten(LoadedSave, false));
        broker.Publish(new object(), new GameSaveCompleted(LoadedSave, true));

        AssertFailed("the co-op session JSON was not written");
    }

    [Fact]
    public void Saving_RefusedByTheSaveSlotLimit_Fails()
    {
        Schedule(0);

        // Vanilla completes a refused save with an empty name and never calls Game.Save.
        broker.Publish(new object(), new GameSaveCompleted(string.Empty, false));

        AssertFailed("the game save failed");
    }

    [Fact]
    public void Saving_NeverStarts_FailsAfter10Seconds()
    {
        Schedule(0);

        TickFor(9);
        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);

        Advance(1);
        AssertFailed("the save did not start within 10 seconds");
    }

    [Fact]
    public void Saving_NeverFinishes_FailsAfter120Seconds()
    {
        Schedule(0);
        broker.Publish(new object(), new GameSaved(LoadedSave));
        broker.Publish(new object(), new CoopSessionWritten(LoadedSave, true));

        TickFor(119);
        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);

        Advance(1);
        AssertFailed("the save did not finish within 120 seconds");
    }

    [Fact]
    public void Saving_IgnoresResultsForOtherSaves()
    {
        Schedule(0);

        foreach (string other in new[] { "TransferSave", "autosave_1" })
        {
            broker.Publish(new object(), new GameSaved(other));
            broker.Publish(new object(), new CoopSessionWritten(other, false));
            broker.Publish(new object(), new GameSaveCompleted(other, false));
        }

        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);
        Assert.Empty(broker.Messages.GetMessages<ServerShutdownFailed>());
    }

    [Fact]
    public void Cancel_DuringCountdown_ReopensJoinsAndAnnounces()
    {
        AddPlayer();
        Schedule(100);

        Assert.True(coordinator.TryCancel(out _));

        Assert.Equal(ServerShutdownPhase.Idle, coordinator.Phase);
        Assert.True(gate.IsOpen);
        Assert.Equal("The server restart was cancelled.", Notices().Last());
        Assert.Equal(1, tickersStopped);
        TickFor(200);
        Assert.Empty(disconnects);
    }

    [Fact]
    public void Cancel_AfterTimeUp_IsRefused()
    {
        peersLeaveOnDisconnect = false;
        AddPlayer();
        Schedule(0);
        Assert.Equal(ServerShutdownPhase.Draining, coordinator.Phase);
        Assert.False(coordinator.TryCancel(out _));

        connectionList.Clear();
        Advance(1);
        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);
        Assert.False(coordinator.TryCancel(out _));
        Assert.False(gate.IsOpen);
    }

    [Fact]
    public void Cancel_AfterAFailure_ReopensJoins()
    {
        Schedule(0);
        broker.Publish(new object(), new GameSaveCompleted(LoadedSave, false));

        Assert.True(coordinator.TryCancel(out _));

        Assert.Equal(ServerShutdownPhase.Idle, coordinator.Phase);
        Assert.True(gate.IsOpen);
    }

    [Fact]
    public void Cancel_WhenIdleOrCompleted_IsRefused()
    {
        Assert.False(coordinator.TryCancel(out _));

        Schedule(0);
        broker.Publish(new object(), new CoopSessionWritten(LoadedSave, true));
        broker.Publish(new object(), new GameSaveCompleted(LoadedSave, true));

        Assert.False(coordinator.TryCancel(out _));
        Assert.False(gate.IsOpen);
    }

    [Fact]
    public void Reschedule_EarlierDeadlineReplacesAndLaterIsRefused()
    {
        AddPlayer();
        Schedule(600);

        Assert.False(coordinator.TrySchedule(900, null!, out _));
        Assert.False(coordinator.TrySchedule(600, null!, out _));
        Assert.True(coordinator.TrySchedule(30, null!, out _));

        TickFor(29);
        Assert.Equal(ServerShutdownPhase.Countdown, coordinator.Phase);
        Advance(1);
        Assert.Equal(ServerShutdownPhase.Saving, coordinator.Phase);
        Assert.Equal(1, tickersStarted);
    }

    [Fact]
    public void Schedule_WhileSaving_IsRefused()
    {
        Schedule(0);

        Assert.False(coordinator.TrySchedule(0, null!, out _));
        Assert.Equal(new[] { LoadedSave }, queuedSaves);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(3601, null)]
    [InlineData(60, "")]
    [InlineData(60, "bad name")]
    [InlineData(60, "../MP")]
    public void Schedule_BadArguments_ChangeNothing(int seconds, string? saveName)
    {
        AddPlayer();

        Assert.False(coordinator.TrySchedule(seconds, saveName!, out _));

        Assert.Equal(ServerShutdownPhase.Idle, coordinator.Phase);
        Assert.True(gate.IsOpen);
        Assert.Empty(Notices());
        Assert.Equal(0, tickersStarted);
    }

    [Fact]
    public void Schedule_NoCampaign_IsRefused()
    {
        saves.SetupGet(value => value.CanQueueSave).Returns(false);

        Assert.False(coordinator.TrySchedule(60, null!, out string result));

        Assert.Equal("No campaign is loaded.", result);
        Assert.Equal(ServerShutdownPhase.Idle, coordinator.Phase);
    }

    [Fact]
    public void Schedule_NoLoadedSave_NeedsASaveName()
    {
        using var fresh = CreateCoordinatorWithoutLoadedSave();

        Assert.False(fresh.TrySchedule(0, null!, out _));
        Assert.True(fresh.TrySchedule(0, "NewCampaign", out _));

        Assert.Equal(new[] { "NewCampaign" }, queuedSaves);
    }

    [Fact]
    public void Schedule_GivenSaveName_IsTheSaveWritten()
    {
        Schedule(0, "Maintenance_1");

        Assert.Equal(new[] { "Maintenance_1" }, queuedSaves);
    }

    [Fact]
    public void RequestFromBroker_SchedulesOnTheGameThread()
    {
        broker.Publish(new object(), new RequestServerShutdown(0));

        Assert.Equal(ServerShutdownPhase.Idle, coordinator.Phase);
        Assert.Single(gameThreadActions).Invoke();
        Assert.Equal(new[] { LoadedSave }, queuedSaves);
    }

    [Fact]
    public void RequestFromANetworkPeer_IsIgnored()
    {
        broker.Publish(CreatePeer(), new RequestServerShutdown(0));

        Assert.Empty(gameThreadActions);
        Assert.Equal(ServerShutdownPhase.Idle, coordinator.Phase);
        Assert.Empty(queuedSaves);
    }

    [Theory]
    [InlineData(typeof(RequestServerShutdown))]
    [InlineData(typeof(ServerShutdownCompleted))]
    [InlineData(typeof(ServerShutdownFailed))]
    public void ShutdownMessages_StayOffTheNetwork(Type messageType)
    {
        // The network serializer only maps ProtoContract types.
        Assert.False(messageType.IsDefined(typeof(ProtoContractAttribute), inherit: false));
    }

    [Fact]
    public void Status_ReportsPhaseSaveAndJoins()
    {
        AddPlayer();
        Schedule(200);
        Advance(10);

        Assert.Equal("phase=Countdown remaining=190s save=MP joins=open connections=1", coordinator.DescribeStatus());
    }

    private ServerShutdownCoordinator CreateCoordinatorWithoutLoadedSave() => new ServerShutdownCoordinator(
        new TestMessageBroker(),
        network.Object,
        connections.Object,
        terminator.Object,
        new ServerAdmissionGate(),
        saves.Object,
        missions.Object,
        () => now,
        _ => new Stopper(() => { }),
        action => action());

    private void Schedule(int seconds, string? saveName = null)
    {
        Assert.True(coordinator.TrySchedule(seconds, saveName!, out string result), result);
    }

    private void Advance(int seconds)
    {
        now = now.AddSeconds(seconds);
        coordinator.Tick();
    }

    private void TickFor(int seconds)
    {
        for (int i = 0; i < seconds; i++) Advance(1);
    }

    private List<string> Notices() => broker.Messages.GetMessages<SendInformationMessage>().Select(message => message.Text).ToList();

    private void AssertFailed(string reason)
    {
        Assert.Equal(ServerShutdownPhase.Failed, coordinator.Phase);
        Assert.Equal(reason, Assert.Single(broker.Messages.GetMessages<ServerShutdownFailed>()).Reason);
        Assert.Empty(broker.Messages.GetMessages<ServerShutdownCompleted>());
        Assert.False(gate.IsOpen);
        Assert.Equal(tickersStarted, tickersStopped);
    }

    private IConnectionLogic AddPlayer(bool isLoading = false)
    {
        var connection = new Mock<IConnectionLogic>();
        connection.SetupGet(value => value.Peer).Returns(CreatePeer());
        connection.SetupGet(value => value.IsLoading).Returns(isLoading);
        connectionList.Add(connection.Object);
        return connection.Object;
    }

    private readonly TestNetwork peers = new TestNetwork();

    private NetPeer CreatePeer() => peers.CreatePeer();

    private sealed class Stopper : IDisposable
    {
        private readonly Action onDispose;

        public Stopper(Action onDispose) => this.onDispose = onDispose;

        public void Dispose() => onDispose();
    }
}
