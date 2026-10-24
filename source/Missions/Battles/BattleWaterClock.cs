using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Utils;
using Missions.Messages;
using Serilog;
using System;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Makes every client of one naval battle read the battle host's water time, so the waves (and the copies
/// floating on them) match. Does nothing when the native water clock patch is not installed.
/// </summary>
public interface IBattleWaterClock : IDisposable
{
    /// <summary>The offset estimate behind the written time, for the debug command.</summary>
    IBattleWaterTimeEstimator Estimator { get; }

    /// <summary>Whether this client is the battle host, whose water time the others adopt.</summary>
    bool IsHost { get; }

    /// <summary>Host samples accepted so far.</summary>
    int SamplesAccepted { get; }

    /// <summary>[Game thread] Points the engine at the battle's water time; called while the loading screen still hides the scene.</summary>
    void Start();

    /// <summary>[Game thread] Writes this frame's water time and, on the host, broadcasts it every second.</summary>
    void Tick(float dt);
}

/// <inheritdoc cref="IBattleWaterClock"/>
public class BattleWaterClock : IBattleWaterClock
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleWaterClock>();

    internal const float BroadcastIntervalSeconds = 1f;
    // Every client starts the battle's waves here rather than at its own accumulated clock, so clients that load together
    // already agree to within their load-time skew; 0 is also where vanilla's first mission after launch starts.
    internal const float BattleStartWaterTime = 0f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly IWaterClock waterClock;
    private float sinceBroadcast = BroadcastIntervalSeconds;
    private bool started;
    private bool redirected;
    // A client's report that reached this client before the assignment making it the host; applied on its first host tick.
    private EarlyReport? earlyReport;

    public BattleWaterClock(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session,
        IHostEpochPolicy hostEpochPolicy, IWaterClock waterClock, IBattleWaterTimeEstimator estimator)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.hostEpochPolicy = hostEpochPolicy;
        this.waterClock = waterClock;
        Estimator = estimator;
        messageBroker.Subscribe<NetworkBattleWaterTime>(Handle_NetworkBattleWaterTime);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public IBattleWaterTimeEstimator Estimator { get; }

    public bool IsHost => session.IsLocalHost;

    public int SamplesAccepted { get; private set; }

    public void Start()
    {
        if (started) return;
        started = true;
        if (!waterClock.IsInstalled || !waterClock.TryReadVanillaMissionTime(out var local)) return;

        float waterTime = Estimator.Begin(local, BattleStartWaterTime);
        // A failed redirect has been logged; this battle keeps vanilla waves rather than retrying every frame.
        redirected = waterClock.TryRedirect(waterTime);
        if (redirected)
            Logger.Information("[NavalSync] Battle water clock on at {WaterTime} (local {Local}, host time {HostTime})",
                waterTime, local, Estimator.HasHostTime);
    }

    public void Tick(float dt)
    {
        // AfterStart normally starts the clock; a battle that skipped it starts on its first tick instead.
        Start();
        if (!redirected || !waterClock.TryReadVanillaMissionTime(out var local)) return;

        bool isHost = session.IsLocalHost;
        if (isHost) ApplyEarlyReport();
        float waterTime = Estimator.Advance(local, isHost);
        waterClock.SetWaterTime(waterTime);
        if (isHost) BroadcastEvery(dt, waterTime);
    }

    // [Game thread] Published from the host assignment. A host change can promote a member whose water lagged, so a client
    // re-arms a forward jump and tells the new host its time.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        var migrated = payload.What;
        if (migrated.MapEventId != session.InstanceId) return;
        if (!redirected || session.IsLocalHost) return;

        var newHost = migrated.NewHostControllerId ?? session.HostControllerId;
        if (newHost == null) return;

        Estimator.RearmAdoption();
        int epoch = migrated.Epoch > 0 ? migrated.Epoch : session.HostEpoch;
        network.Send(newHost, new NetworkBattleWaterTime(waterClock.WrittenTime, epoch, fromHost: false));
    }

    // Applies the report held from before this client became the host, if it was sent for this host epoch.
    private void ApplyEarlyReport()
    {
        if (earlyReport is not EarlyReport report) return;
        if (report.HostEpoch > session.HostEpoch) return;

        earlyReport = null;
        if (report.HostEpoch == session.HostEpoch) AdoptReport(report.WaterTime, report.LocalTime);
    }

    // Sends the host's water time once per interval.
    private void BroadcastEvery(float dt, float waterTime)
    {
        sinceBroadcast += dt;
        if (sinceBroadcast < BroadcastIntervalSeconds) return;

        sinceBroadcast = 0f;
        network.SendAll(new NetworkBattleWaterTime(waterTime, session.HostEpoch));
    }

    // [Poller thread] Read and applied on the game thread, where the local clock and the session are read.
    private void Handle_NetworkBattleWaterTime(MessagePayload<NetworkBattleWaterTime> payload)
    {
        if (!waterClock.IsInstalled) return;

        var sample = payload.What;
        GameThread.RunSafe(() => AcceptWaterTime(sample), context: nameof(Handle_NetworkBattleWaterTime));
    }

    /// <summary>[Game thread] Takes the current host's water time as this client's target, or on the host a client's report.</summary>
    internal void AcceptWaterTime(NetworkBattleWaterTime sample)
    {
        if (!waterClock.IsInstalled) return;
        if (float.IsNaN(sample.WaterTime) || float.IsInfinity(sample.WaterTime)) return;
        if (!sample.FromHost)
        {
            AcceptPeerTime(sample);
            return;
        }
        if (session.IsLocalHost) return;
        if (hostEpochPolicy.IsStale(sample.HostEpoch, session.HostEpoch)) return;
        if (!waterClock.TryReadVanillaMissionTime(out var local)) return;

        if (!Estimator.HasHostTime)
            Logger.Information("[NavalSync] Adopting the battle host's water time {HostTime} (local {Local})", sample.WaterTime, local);
        Estimator.AddHostSample(sample.WaterTime, local);
        SamplesAccepted++;
    }

    // [Game thread] On the host: a client's time far ahead means this host lagged when promoted, so it jumps forward to it. A
    // report for a newer epoch beat this client's own assignment (another channel) and waits for its first host tick.
    private void AcceptPeerTime(NetworkBattleWaterTime report)
    {
        if (!redirected || !waterClock.TryReadVanillaMissionTime(out var local)) return;
        if (session.IsLocalHost)
        {
            AdoptReport(report.WaterTime, local);
            return;
        }
        if (report.HostEpoch <= session.HostEpoch) return;

        HoldEarlyReport(new EarlyReport(report.WaterTime, local, report.HostEpoch));
    }

    // Keeps the report of the newest epoch that is furthest ahead of the local clock.
    private void HoldEarlyReport(EarlyReport report)
    {
        if (earlyReport is EarlyReport held && (held.HostEpoch > report.HostEpoch
            || (held.HostEpoch == report.HostEpoch && held.Offset >= report.Offset)))
            return;

        earlyReport = report;
    }

    // Jumps the host forward to a client's time when it is far enough ahead.
    private void AdoptReport(float waterTime, float local)
    {
        if (Estimator.AdoptIfAhead(waterTime, local))
            Logger.Information("[NavalSync] Promoted host jumped forward to a client's water time {WaterTime}", waterTime);
    }

    // [Poller thread] A peer entering the battle gets the host's time at once instead of up to a second later.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        if (!waterClock.IsInstalled) return;

        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() => SendTimeTo(entered.ControllerId), context: nameof(Handle_PeerEntered));
    }

    /// <summary>[Game thread] Sends the host's current water time to one peer.</summary>
    internal void SendTimeTo(string controllerId)
    {
        if (!redirected || !session.IsLocalHost || session.IsOwn(controllerId)) return;

        network.Send(controllerId, new NetworkBattleWaterTime(waterClock.WrittenTime, session.HostEpoch));
    }

    // Drops the subscriptions and gives the engine its own clock back for the next scene.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleWaterTime>(Handle_NetworkBattleWaterTime);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        if (!redirected) return;

        redirected = false;
        waterClock.Restore();
        Logger.Information("[NavalSync] Battle water clock off at {WaterTime}", waterClock.WrittenTime);
    }

    /// <summary>A client's water time report held until this client's own host assignment arrives, with the local clock then.</summary>
    private readonly struct EarlyReport
    {
        public readonly float WaterTime;
        public readonly float LocalTime;
        public readonly int HostEpoch;

        public EarlyReport(float waterTime, float localTime, int hostEpoch)
        {
            WaterTime = waterTime;
            LocalTime = localTime;
            HostEpoch = hostEpoch;
        }

        /// <summary>How far the report is ahead of the local clock.</summary>
        public double Offset => (double)WaterTime - LocalTime;
    }
}
