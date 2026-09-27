using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Save.Messages;
using Coop.Core.Server.Services.Shutdown.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Coop.Core.Server.Services.Shutdown;

/// <summary>
/// Runs a graceful restart: warn players, close joins, disconnect everyone, then save and report
/// the result. It never exits the process.
/// </summary>
public interface IServerShutdownCoordinator : IDisposable
{
    ServerShutdownPhase Phase { get; }

    /// <summary>Schedules a restart in <paramref name="seconds"/>; an earlier one replaces a pending one.</summary>
    bool TrySchedule(int seconds, string saveName, out string result);

    bool TryCancel(out string result);

    string DescribeStatus();
}

/// <inheritdoc cref="IServerShutdownCoordinator"/>
/// <remarks>Runs on the game thread: commands, ticks and the save events all arrive there.</remarks>
public class ServerShutdownCoordinator : IServerShutdownCoordinator
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerShutdownCoordinator>();

    public const int MaxDelaySeconds = 3600;
    internal const string DisconnectReason = "ServerRestarting";
    internal static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan JoinGateLead = TimeSpan.FromSeconds(120);
    internal static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan SaveStartTimeout = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan SaveTimeout = TimeSpan.FromSeconds(120);

    private static readonly int[] NoticeSeconds = { 300, 60, 10 };
    private static readonly Regex SafeSaveName = new Regex("^[A-Za-z0-9_-]{1,64}$");

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IConnectionCollection connections;
    private readonly IJoinPeerTerminator peerTerminator;
    private readonly IServerAdmissionGate admissionGate;
    private readonly ISaveInterface saveInterface;
    private readonly IMissionManager missionManager;
    private readonly Func<DateTime> getUtcNow;
    private readonly Func<Action, IDisposable> startTicker;
    private readonly Action<Action> runOnGameThread;
    private readonly HashSet<NetPeer> disconnectedPeers = new HashSet<NetPeer>(ReferenceComparer<NetPeer>.Instance);

    private IDisposable ticker;
    private string loadedSaveName;
    private string saveName;
    private string failureReason;
    private DateTime deadline;
    private DateTime drainDeadline;
    private DateTime saveQueuedAt;
    private int nextNotice;
    private bool saveStarted;
    private bool gameSaveWritten;
    private bool sessionWritten;
    private bool disposed;

    public ServerShutdownCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IConnectionCollection connections,
        IJoinPeerTerminator peerTerminator,
        IServerAdmissionGate admissionGate,
        ISaveInterface saveInterface,
        IMissionManager missionManager)
        : this(
            messageBroker,
            network,
            connections,
            peerTerminator,
            admissionGate,
            saveInterface,
            missionManager,
            () => DateTime.UtcNow,
            StartTicker,
            action => GameThread.RunSafe(action, context: nameof(RequestServerShutdown)))
    {
    }

    internal ServerShutdownCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IConnectionCollection connections,
        IJoinPeerTerminator peerTerminator,
        IServerAdmissionGate admissionGate,
        ISaveInterface saveInterface,
        IMissionManager missionManager,
        Func<DateTime> getUtcNow,
        Func<Action, IDisposable> startTicker,
        Action<Action> runOnGameThread)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.connections = connections;
        this.peerTerminator = peerTerminator;
        this.admissionGate = admissionGate;
        this.saveInterface = saveInterface;
        this.missionManager = missionManager;
        this.getUtcNow = getUtcNow;
        this.startTicker = startTicker;
        this.runOnGameThread = runOnGameThread;

        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);
        messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Subscribe<CoopSessionWritten>(Handle_CoopSessionWritten);
        messageBroker.Subscribe<GameSaveCompleted>(Handle_GameSaveCompleted);
        messageBroker.Subscribe<RequestServerShutdown>(Handle_RequestServerShutdown);
    }

    public ServerShutdownPhase Phase { get; private set; } = ServerShutdownPhase.Idle;

    public void Dispose()
    {
        disposed = true;
        StopTicker();

        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);
        messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Unsubscribe<CoopSessionWritten>(Handle_CoopSessionWritten);
        messageBroker.Unsubscribe<GameSaveCompleted>(Handle_GameSaveCompleted);
        messageBroker.Unsubscribe<RequestServerShutdown>(Handle_RequestServerShutdown);
    }

    public bool TrySchedule(int seconds, string saveName, out string result)
    {
        if (seconds < 0 || seconds > MaxDelaySeconds)
        {
            result = $"Seconds must be from 0 through {MaxDelaySeconds}.";
            return false;
        }
        if (saveName != null && !SafeSaveName.IsMatch(saveName))
        {
            result = "Save name must contain 1 through 64 letters, digits, underscores, or hyphens.";
            return false;
        }
        if (Phase == ServerShutdownPhase.Draining || Phase == ServerShutdownPhase.Saving)
        {
            result = "The shutdown is already disconnecting players and saving.";
            return false;
        }
        if (!saveInterface.CanQueueSave)
        {
            result = "No campaign is loaded.";
            return false;
        }

        string name = saveName ?? (Phase == ServerShutdownPhase.Countdown ? this.saveName : loadedSaveName);
        if (name == null)
        {
            result = "No save has been loaded, so give a save_name.";
            return false;
        }

        DateTime now = getUtcNow();
        DateTime requestedDeadline = now.AddSeconds(seconds);
        if (Phase == ServerShutdownPhase.Countdown && requestedDeadline >= deadline)
        {
            result = $"A shutdown is already scheduled in {SecondsLeft(now)}s; only an earlier time replaces it.";
            return false;
        }

        Phase = ServerShutdownPhase.Countdown;
        deadline = requestedDeadline;
        this.saveName = name;
        failureReason = null;
        disconnectedPeers.Clear();
        nextNotice = 0;
        while (nextNotice < NoticeSeconds.Length && NoticeSeconds[nextNotice] >= seconds) nextNotice++;

        Logger.Information("Server shutdown scheduled in {Seconds}s, saving {SaveName}", seconds, name);
        if (seconds > 0) Broadcast($"The server will restart in {FormatDelay(seconds)}.");

        if (ticker == null) ticker = startTicker(Tick);
        Tick();

        result = $"Server shutdown scheduled in {seconds}s, saving {name}. {DescribeStatus()}";
        return true;
    }

    public bool TryCancel(out string result)
    {
        switch (Phase)
        {
            case ServerShutdownPhase.Countdown:
                ResetToIdle();
                Logger.Information("Server shutdown cancelled");
                Broadcast("The server restart was cancelled.");
                result = "Server shutdown cancelled; joins are open.";
                return true;
            case ServerShutdownPhase.Failed:
                ResetToIdle();
                Logger.Information("Server shutdown cleared after a failure; joins are open");
                result = "Failed shutdown cleared; joins are open.";
                return true;
            case ServerShutdownPhase.Draining:
            case ServerShutdownPhase.Saving:
                result = "Too late to cancel: players are disconnected and the save is running.";
                return false;
            case ServerShutdownPhase.Completed:
                result = "The shutdown already completed; restart the server.";
                return false;
            default:
                result = "No shutdown is scheduled.";
                return false;
        }
    }

    public string DescribeStatus()
    {
        DateTime now = getUtcNow();
        string joins = admissionGate.IsOpen ? "open" : "closed";
        switch (Phase)
        {
            case ServerShutdownPhase.Countdown:
                return $"phase=Countdown remaining={SecondsLeft(now)}s save={saveName} joins={joins} connections={connections.Count()}";
            case ServerShutdownPhase.Draining:
                return $"phase=Draining save={saveName} joins={joins} connections={connections.Count()} saving={saveInterface.IsSaving}";
            case ServerShutdownPhase.Saving:
                return $"phase=Saving save={saveName} joins={joins} started={saveStarted} saveWritten={gameSaveWritten} sessionWritten={sessionWritten}";
            case ServerShutdownPhase.Completed:
                return $"phase=Completed save={saveName} joins={joins}";
            case ServerShutdownPhase.Failed:
                return $"phase=Failed reason={failureReason} joins={joins}";
            default:
                return $"phase=Idle joins={joins} defaultSave={loadedSaveName ?? "none"}";
        }
    }

    internal void Tick()
    {
        if (disposed) return;

        DateTime now = getUtcNow();
        switch (Phase)
        {
            case ServerShutdownPhase.Countdown:
                TickCountdown(now);
                break;
            case ServerShutdownPhase.Draining:
                TickDraining(now);
                break;
            case ServerShutdownPhase.Saving:
                TickSaving(now);
                break;
        }
    }

    private void TickCountdown(DateTime now)
    {
        TimeSpan remaining = deadline - now;
        if (remaining <= JoinGateLead && admissionGate.IsOpen)
        {
            admissionGate.Close();
            Logger.Information("Server shutdown: joins closed with {Seconds}s left", SecondsLeft(now));
        }

        int dueNotice = 0;
        while (nextNotice < NoticeSeconds.Length && remaining.TotalSeconds <= NoticeSeconds[nextNotice])
        {
            dueNotice = NoticeSeconds[nextNotice];
            nextNotice++;
        }

        // Nobody to warn, so skip the rest of the countdown.
        if (remaining <= TimeSpan.Zero || !connections.Any())
        {
            BeginDrain(now);
            return;
        }

        if (dueNotice > 0) Broadcast($"The server will restart in {FormatDelay(dueNotice)}.");
    }

    private void BeginDrain(DateTime now)
    {
        admissionGate.Close();
        Phase = ServerShutdownPhase.Draining;
        drainDeadline = now + DrainTimeout;

        MissionManagerDiagnostics missions = missionManager.GetDiagnostics();
        int disconnected = DisconnectRemainingPeers();
        Logger.Information(
            "Server shutdown: disconnecting {Connections} connection(s); {Missions} mission instance(s) active, {Concluding} concluding",
            disconnected,
            missions.ActiveInstances,
            missions.ConcludingInstances);

        TickDraining(now);
    }

    private void TickDraining(DateTime now)
    {
        // A request accepted just before the gate closed can still show up here.
        DisconnectRemainingPeers();

        bool saving = saveInterface.IsSaving;
        if (!saving && !connections.Any())
        {
            BeginSave(now);
            return;
        }

        if (now < drainDeadline) return;

        if (saving)
        {
            Fail("an earlier save was still running after 15 seconds");
            return;
        }

        Logger.Warning("Server shutdown: {Connections} connection(s) still listed after 15 seconds; saving anyway",
            connections.Count());
        BeginSave(now);
    }

    private int DisconnectRemainingPeers()
    {
        int disconnected = 0;
        foreach (IConnectionLogic connection in connections.ToArray())
        {
            NetPeer peer = connection.Peer;
            if (peer == null || !disconnectedPeers.Add(peer)) continue;

            peerTerminator.Disconnect(peer, DisconnectReason);
            disconnected++;
        }

        return disconnected;
    }

    private void BeginSave(DateTime now)
    {
        Phase = ServerShutdownPhase.Saving;
        saveQueuedAt = now;
        saveStarted = false;
        gameSaveWritten = false;
        sessionWritten = false;

        if (!saveInterface.TryQueueSave(saveName))
        {
            Fail("no campaign is loaded");
            return;
        }

        Logger.Information("Server shutdown: saving {SaveName}", saveName);
    }

    private void TickSaving(DateTime now)
    {
        TimeSpan elapsed = now - saveQueuedAt;
        if (!saveStarted && elapsed >= SaveStartTimeout)
        {
            Fail("the save did not start within 10 seconds");
        }
        else if (elapsed >= SaveTimeout)
        {
            Fail("the save did not finish within 120 seconds");
        }
    }

    private void Handle_GameLoaded(MessagePayload<GameLoaded> payload)
    {
        loadedSaveName = payload.What.SaveName;
    }

    private void Handle_GameSaved(MessagePayload<GameSaved> payload)
    {
        if (Phase == ServerShutdownPhase.Saving && IsShutdownSave(payload.What.SaveName)) saveStarted = true;
    }

    private void Handle_CoopSessionWritten(MessagePayload<CoopSessionWritten> payload)
    {
        if (Phase != ServerShutdownPhase.Saving || !IsShutdownSave(payload.What.SaveName)) return;

        saveStarted = true;
        if (!payload.What.Success)
        {
            Fail("the co-op session JSON was not written");
            return;
        }

        sessionWritten = true;
        CompleteIfSaved();
    }

    private void Handle_GameSaveCompleted(MessagePayload<GameSaveCompleted> payload)
    {
        if (Phase != ServerShutdownPhase.Saving) return;

        // Vanilla reports a refused save (the save slot limit) with an empty name.
        bool refusedWithoutName = !payload.What.Success && string.IsNullOrEmpty(payload.What.SaveName);
        if (!refusedWithoutName && !IsShutdownSave(payload.What.SaveName)) return;

        saveStarted = true;
        if (!payload.What.Success)
        {
            Fail("the game save failed");
            return;
        }

        gameSaveWritten = true;
        CompleteIfSaved();
    }

    private void Handle_RequestServerShutdown(MessagePayload<RequestServerShutdown> payload)
    {
        // Network messages are published with the sending peer as the source.
        if (payload.Who is NetPeer)
        {
            Logger.Warning("Ignored a server shutdown request that arrived from a network peer");
            return;
        }

        int seconds = payload.What.Seconds;
        runOnGameThread(() =>
        {
            if (!TrySchedule(seconds, null, out string result))
                Logger.Warning("Server shutdown request refused: {Result}", result);
        });
    }

    private void CompleteIfSaved()
    {
        if (!gameSaveWritten || !sessionWritten) return;

        Phase = ServerShutdownPhase.Completed;
        StopTicker();
        loadedSaveName = saveName;

        Logger.Information("Server shutdown complete: saved {SaveName}", saveName);
        messageBroker.Publish(this, new ServerShutdownCompleted(saveName));
    }

    private void Fail(string reason)
    {
        Phase = ServerShutdownPhase.Failed;
        failureReason = reason;
        StopTicker();

        Logger.Error("Server shutdown failed: {Reason}", reason);
        messageBroker.Publish(this, new ServerShutdownFailed(reason));
    }

    private void ResetToIdle()
    {
        Phase = ServerShutdownPhase.Idle;
        StopTicker();
        failureReason = null;
        disconnectedPeers.Clear();
        admissionGate.Open();
    }

    private void StopTicker()
    {
        ticker?.Dispose();
        ticker = null;
    }

    private bool IsShutdownSave(string name) => string.Equals(name, saveName, StringComparison.OrdinalIgnoreCase);

    private void Broadcast(string text)
    {
        var message = new SendInformationMessage(text);
        messageBroker.Publish(this, message);
        network.SendAll(message);
    }

    private int SecondsLeft(DateTime now) => Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds));

    private static string FormatDelay(int seconds)
    {
        if (seconds >= 60 && seconds % 60 == 0)
        {
            int minutes = seconds / 60;
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }

    private static IDisposable StartTicker(Action tick)
    {
        int queued = 0;
        return new Timer(_ =>
        {
            // Skip a tick while the previous one is still waiting for the game thread.
            if (Interlocked.Exchange(ref queued, 1) == 1) return;

            GameThread.RunSafe(() =>
            {
                Volatile.Write(ref queued, 0);
                tick();
            }, context: "ServerShutdownTick");
        }, null, TickInterval, TickInterval);
    }
}

public enum ServerShutdownPhase
{
    Idle,
    Countdown,
    Draining,
    Saving,
    Completed,
    Failed,
}
