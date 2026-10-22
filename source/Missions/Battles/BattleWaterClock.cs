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

    /// <summary>[Game thread] Writes this frame's water time and, on the host, broadcasts it every second.</summary>
    void Tick(float dt);
}

/// <inheritdoc cref="IBattleWaterClock"/>
public class BattleWaterClock : IBattleWaterClock
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleWaterClock>();

    internal const float BroadcastIntervalSeconds = 1f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly IWaterClock waterClock;
    private float sinceBroadcast = BroadcastIntervalSeconds;
    private bool overriding;
    private bool overrideFailed;

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
    }

    public IBattleWaterTimeEstimator Estimator { get; }

    public bool IsHost => session.IsLocalHost;

    public int SamplesAccepted { get; private set; }

    public void Tick(float dt)
    {
        if (!waterClock.IsInstalled || overrideFailed) return;
        if (!waterClock.TryReadVanillaTime(WaterClock.MissionTimeType, out var local)) return;

        bool isHost = session.IsLocalHost;
        float waterTime = Estimator.Advance(local, isHost);
        if (!overriding)
        {
            // The first frame starts from this client's own clock (or the host's, if its time came during loading).
            // A failed redirect has been logged; this battle keeps vanilla waves rather than retrying every frame.
            overrideFailed = !waterClock.TryBeginOverride(waterTime);
            if (overrideFailed) return;
            overriding = true;
            Logger.Information("[NavalSync] Battle water clock on at {WaterTime} (local {Local}, host {IsHost})", waterTime, local, isHost);
        }

        waterClock.SetWaterTime(waterTime);
        if (isHost) BroadcastEvery(dt, waterTime);
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
        GameThread.RunSafe(() => AcceptHostTime(sample), context: nameof(Handle_NetworkBattleWaterTime));
    }

    /// <summary>[Game thread] Takes the current host's water time as this client's target.</summary>
    internal void AcceptHostTime(NetworkBattleWaterTime sample)
    {
        if (!waterClock.IsInstalled || session.IsLocalHost) return;
        if (float.IsNaN(sample.WaterTime) || float.IsInfinity(sample.WaterTime)) return;
        if (hostEpochPolicy.IsStale(sample.HostEpoch, session.HostEpoch)) return;
        if (!waterClock.TryReadVanillaTime(WaterClock.MissionTimeType, out var local)) return;

        if (!Estimator.HasHostTime)
            Logger.Information("[NavalSync] Adopting the battle host's water time {HostTime} (local {Local})", sample.WaterTime, local);
        Estimator.AddHostSample(sample.WaterTime, local);
        SamplesAccepted++;
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
        if (!overriding || !session.IsLocalHost || session.IsOwn(controllerId)) return;

        network.Send(controllerId, new NetworkBattleWaterTime(waterClock.WrittenTime, session.HostEpoch));
    }

    // Drops the subscriptions and gives the engine its own clock back for the next scene.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleWaterTime>(Handle_NetworkBattleWaterTime);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        if (!overriding) return;

        overriding = false;
        waterClock.EndOverride();
        Logger.Information("[NavalSync] Battle water clock off at {WaterTime}", waterClock.WrittenTime);
    }
}
