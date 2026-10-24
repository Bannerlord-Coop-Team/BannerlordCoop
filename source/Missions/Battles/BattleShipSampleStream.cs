using Common;
using Common.Messaging;
using Missions.Messages;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The 20 Hz hull samples. Own hulls send their frame, body motion, helm input and ropes once per interval; a
/// copy takes its authority's samples in <see cref="AuthorityStamp"/> order as the engine's steering target.
/// </summary>
public interface IBattleShipSampleStream : IDisposable
{
    /// <summary>[Mission tick] Advances the stream's clock, which times the sample lifetime.</summary>
    void Advance(float dt);

    /// <summary>[Mission tick] Sends every own hull's sample and changed condition once per send interval.</summary>
    void SendOwn(float dt);

    /// <summary>[Mission tick] Stops counting a copy's stream as live once no sample came for the sample lifetime.</summary>
    void ExpireStale();

    /// <summary>Validates an owner's sample and takes it as the copy's steering target; one for a ship whose record waits is held.</summary>
    void Accept(NetworkBattleShipSample sample);

    /// <summary>
    /// Takes a departed owner's held sample before its hull moves on; its sender is out of the mission by design, so only the
    /// authority, order and validity checks apply.
    /// </summary>
    void AcceptFromDepartedOwner(NetworkShipInfo ship, NetworkBattleShipSample sample);

    /// <summary>Counts a dropped hull message against the hull's stream (diagnostics).</summary>
    void Reject(Guid shipId, string reason);

    /// <summary>The hull's authority moved: the old owner's target and order no longer describe it.</summary>
    void Restart(Guid shipId);

    /// <summary>Drops a hull that left the mission.</summary>
    void Forget(Guid shipId);

    /// <summary>The hull's stream state, or null before any traffic for it (diagnostics).</summary>
    ShipSampleStream StateOf(Guid shipId);
}

/// <inheritdoc cref="IBattleShipSampleStream"/>
public class BattleShipSampleStream : IBattleShipSampleStream
{
    internal const float SendIntervalSeconds = 0.05f;
    // Measured on this client's mission clock from receipt, since peers' wall clocks are not in step.
    internal const float SampleLifetimeSeconds = 1f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly IMissionContext missionContext;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly INavalShipEngine engine;
    private readonly IBattleShipConditionStream conditions;
    private readonly IHeldShipBuffer heldShips;

    private readonly Dictionary<Guid, ShipSampleStream> streams = new Dictionary<Guid, ShipSampleStream>();
    private float sendElapsed;
    private float elapsed;

    // Subscribes to the hull samples of this battle's mesh.
    public BattleShipSampleStream(IBattleNetwork network, IMessageBroker messageBroker, BattleContext context, INavalShipEngine engine,
        IBattleShipConditionStream conditions, IHeldShipBuffer heldShips)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        session = context.Session;
        missionComponent = context.MissionComponent;
        missionContext = context.MissionContext;
        hostEpochPolicy = context.HostEpochPolicy;
        this.engine = engine;
        this.conditions = conditions;
        this.heldShips = heldShips;

        messageBroker.Subscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    // Drops the subscription.
    public void Dispose() => messageBroker.Unsubscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);

    // Applies a sample from the game-thread drain; a closure queued before the mission ended finds no mission.
    private void Handle_NetworkBattleShipSample(MessagePayload<NetworkBattleShipSample> payload)
    {
        var sample = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            Accept(sample);
        }, context: nameof(Handle_NetworkBattleShipSample));
    }

    public void Advance(float dt) => elapsed += Math.Max(0f, dt);

    public void SendOwn(float dt)
    {
        sendElapsed += dt;
        if (sendElapsed < SendIntervalSeconds) return;
        sendElapsed = CarryAfterSend(sendElapsed);

        foreach (var ship in Registry.Ships.Where(IsOwnHull))
        {
            var stream = StreamOf(ship.ShipId);
            // The sunk condition already went out after the hull's last sample; peers keep that frame.
            if (conditions.ConditionOf(ship.ShipId)?.SinkingState == BattleShipCondition.Sunk) continue;

            stream.Sent++;
            network.SendAll(new NetworkBattleShipSample(ship.ShipId, session.OwnControllerId, stream.Sent,
                NetworkBattleShipSample.FromFrame(engine.GetFrame(ship.Hull)), engine.ReadInput(ship.Hull),
                engine.CaptureRopes(ship.Hull, ShipIdOf), session.HostEpoch, engine.ReadMotion(ship.Hull)));
            conditions.SendIfChanged(ship);
        }
    }

    /// <summary>
    /// The time a send carries into the next interval, so heavy frames keep 20 Hz; at most one interval, so a long frame is
    /// followed by one catch-up send instead of a burst.
    /// </summary>
    internal static float CarryAfterSend(float elapsed) => Math.Min(elapsed - SendIntervalSeconds, SendIntervalSeconds);

    // The engine's steering ages a target on its own; this only stops reporting the stream as live.
    public void ExpireStale()
    {
        foreach (var stream in streams.Values)
        {
            if (stream.Target == null || elapsed - stream.TargetReceivedAt <= SampleLifetimeSeconds) continue;

            stream.Target = null;
            stream.Reject("expired");
        }
    }

    public void Accept(NetworkBattleShipSample sample)
    {
        if (!Registry.TryGet(sample.ShipId, out var ship))
        {
            if (heldShips.IsAwaitingRecord(sample.ShipId)) heldShips.Of(sample.ShipId).HoldSample(sample);
            else StreamOf(sample.ShipId).Reject("unknown_ship");
            return;
        }

        var stream = StreamOf(sample.ShipId);
        var rejection = ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, stream.AcceptedEpoch,
            missionContext.ControllersInMission);
        // BR-102: an AI hull follows the host, so a superseded hosting generation is dropped like siege state.
        if (rejection == null && ship.FollowsBattleHost && hostEpochPolicy.IsStale(sample.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        if (rejection != null)
        {
            stream.Reject(rejection);
            return;
        }

        Take(ship, stream, sample);
    }

    public void AcceptFromDepartedOwner(NetworkShipInfo ship, NetworkBattleShipSample sample)
    {
        var stream = StreamOf(ship.ShipId);
        if (ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, stream.AcceptedEpoch, null) == null)
            Take(ship, stream, sample);
    }

    /// <summary>Why a sample must be dropped, or null to accept it.</summary>
    internal static string ValidateSample(NetworkShipInfo ship, string ownControllerId, NetworkBattleShipSample sample,
        long acceptedSequence, int acceptedEpoch, IReadOnlyCollection<string> controllersInMission)
    {
        // A new host numbers an AI hull's samples from its own stream, so a newer epoch restarts the sequence.
        var rejection = sample.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, acceptedEpoch, acceptedSequence), followsHostEpoch: ship.FollowsBattleHost,
            controllersInMission);
        if (rejection != null) return rejection;
        if (!sample.HasValidFrame) return "invalid_frame";
        if (!sample.Motion.IsValid) return "invalid_motion";
        if (!sample.Input.IsValid) return "invalid_input";
        if (!BattleRopeState.AreValid(sample.Ropes)) return "invalid_ropes";
        return null;
    }

    // Hands an accepted sample's input, ropes, frame and motion to the copy.
    private void Take(NetworkShipInfo ship, ShipSampleStream stream, NetworkBattleShipSample sample)
    {
        if (ship.FollowsBattleHost) stream.AcceptedEpoch = sample.HostEpoch;

        engine.ApplyInput(ship.Hull, sample.Input);
        engine.ApplyRopes(ship.Hull, sample.Ropes, HullOf, final: false);
        engine.ApplyForeignSample(ship.Hull, NetworkBattleShipSample.ToFrame(sample.Frame), sample.Motion);
        stream.Target = sample;
        stream.TargetReceivedAt = elapsed;
        stream.Accepted = sample.Sequence;
    }

    public void Reject(Guid shipId, string reason) => StreamOf(shipId).Reject(reason);

    public void Restart(Guid shipId)
    {
        var stream = StreamOf(shipId);
        stream.Target = null;
        stream.Accepted = 0;
        stream.AcceptedEpoch = 0;
    }

    public void Forget(Guid shipId) => streams.Remove(shipId);

    public ShipSampleStream StateOf(Guid shipId) => streams.TryGetValue(shipId, out var stream) ? stream : null;

    // A hull this client simulates.
    private bool IsOwnHull(NetworkShipInfo ship) => ship.CurrentAuthority == session.OwnControllerId;

    // The ship id a rope end names for a local hull, or empty.
    private Guid ShipIdOf(MissionObject hull) => Registry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;

    // The local hull a rope end's ship id names, or null.
    private MissionObject HullOf(Guid shipId) => Registry.TryGet(shipId, out var ship) ? ship.Hull : null;

    // The hull's stream state, created on first use.
    private ShipSampleStream StreamOf(Guid shipId)
    {
        if (!streams.TryGetValue(shipId, out var stream))
        {
            stream = new ShipSampleStream();
            streams.Add(shipId, stream);
        }

        return stream;
    }
}

/// <summary>One hull's sample stream: its steering target and when it came, and the sent, accepted and rejected counts.</summary>
public sealed class ShipSampleStream
{
    public NetworkBattleShipSample Target { get; internal set; }
    /// <summary>This client's mission clock when <see cref="Target"/> arrived.</summary>
    public float TargetReceivedAt { get; internal set; }
    public long Sent { get; internal set; }
    public long Accepted { get; internal set; }
    public int AcceptedEpoch { get; internal set; }
    public long Rejects { get; private set; }
    public string LastReject { get; private set; }

    /// <summary>Counts a dropped message and keeps why.</summary>
    internal void Reject(string reason)
    {
        Rejects++;
        LastReject = reason;
    }
}
