using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The request and decision of an in-mission ship capture: the captor runs vanilla's helm timer and asks the hull's current
/// authority, which decides on the real crew and reserve; <see cref="IShipCaptureApplier"/> applies what it confirms everywhere.
/// </summary>
public interface IBattleShipCaptureReplicator : IDisposable
{
    /// <summary>[Game thread] Notices a promotion to battle host and expires requests no authority answered.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Request and refusal counters, pending requests and the applier's state (diagnostics).</summary>
    object Inspect();

    /// <summary>[Game thread] How long this client's request for <paramref name="shipId"/> has waited, or null when none waits.</summary>
    float? PendingRequestAge(Guid shipId);
}

/// <inheritdoc cref="IBattleShipCaptureReplicator"/>
public class BattleShipCaptureReplicator : IBattleShipCaptureReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipCaptureReplicator>();

    /// <summary>How long a captor waits for the authority's answer before it tells the player nobody answered.</summary>
    internal const float RequestTimeoutSeconds = 5f;

    /// <summary>
    /// How long a promoted host refuses to capture the AI hulls it inherited, so a capture the old host confirmed just before it
    /// left reaches it first over the mesh, and every capture of a hull keeps one decider per place in its order.
    /// </summary>
    internal const float PromotionSettleSeconds = 2f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly IMissionContext missionContext;
    private readonly INavalShipEngine engine;
    private readonly INavalShipCaptureEngine captureEngine;
    private readonly INavalHullAuthority hullAuthority;
    private readonly IShipCaptureApplier applier;

    // [Game thread] This client's requests waiting for the authority, by prize ship id, with when each went out and the own
    // hull whose formation would take the prize.
    private readonly Dictionary<Guid, PendingRequest> pendingRequests = new Dictionary<Guid, PendingRequest>();
    private readonly Dictionary<ShipCaptureRefusal, long> refusals = new Dictionary<ShipCaptureRefusal, long>();
    private float elapsed;
    private int seenEpoch;
    private float? promotedAt;
    private long captureRequests, captureExpired;
    private bool disposed;

    public BattleShipCaptureReplicator(IBattleNetwork network, IMessageBroker messageBroker, BattleContext context, INavalShipEngine engine,
        INavalShipCaptureEngine captureEngine, INavalHullAuthority hullAuthority, IShipCaptureApplier applier)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        session = context.Session;
        missionComponent = context.MissionComponent;
        missionContext = context.MissionContext;
        this.engine = engine;
        this.captureEngine = captureEngine;
        this.hullAuthority = hullAuthority;
        this.applier = applier;

        messageBroker.Subscribe<ShipCaptureTimerElapsed>(Handle_ShipCaptureTimerElapsed);
        messageBroker.Subscribe<NetworkShipCaptureRequest>(Handle_NetworkShipCaptureRequest);
        messageBroker.Subscribe<NetworkShipCaptureRejected>(Handle_NetworkShipCaptureRejected);
        applier.CaptureApplied += OnCaptureApplied;
    }

    // Drops every subscription and whatever still waits.
    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<ShipCaptureTimerElapsed>(Handle_ShipCaptureTimerElapsed);
        messageBroker.Unsubscribe<NetworkShipCaptureRequest>(Handle_NetworkShipCaptureRequest);
        messageBroker.Unsubscribe<NetworkShipCaptureRejected>(Handle_NetworkShipCaptureRejected);
        applier.CaptureApplied -= OnCaptureApplied;
        pendingRequests.Clear();
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public void Tick(float dt)
    {
        if (disposed) return;

        elapsed += Math.Max(0f, dt);
        NoticePromotion();
        ExpirePendingRequests();
    }

    public float? PendingRequestAge(Guid shipId) =>
        pendingRequests.TryGetValue(shipId, out var pending) ? elapsed - pending.SentAt : (float?)null;

    // [Game thread] Published synchronously by the capture prefix once vanilla's timer ran out and took the pilot off the helm.
    private void Handle_ShipCaptureTimerElapsed(MessagePayload<ShipCaptureTimerElapsed> payload)
    {
        if (disposed) return;

        var timer = payload.What;
        if (!Registry.TryGetByHull(timer.Hull, out var prize))
        {
            Logger.Warning("[NavalSync] A capture timer ran out on an unregistered hull; nothing to capture");
            return;
        }

        var captorHull = engine.CrewHullOf(timer.CaptorAgent);
        if (captorHull == null || !Registry.TryGetByHull(captorHull, out var captorShip))
        {
            Refuse(prize.ShipId, session.OwnControllerId, ShipCaptureRefusal.CaptorHullInvalid);
            return;
        }

        RequestCapture(prize, captorShip);
    }

    /// <summary>[Game thread] Decides a capture of a hull this client simulates, or asks its authority once.</summary>
    internal void RequestCapture(NetworkShipInfo prize, NetworkShipInfo captorShip)
    {
        var request = new NetworkShipCaptureRequest(prize.ShipId, session.OwnControllerId, captorShip.ShipId);
        if (IsOwn(prize))
        {
            DecideRequest(prize, request);
            return;
        }

        if (pendingRequests.ContainsKey(prize.ShipId)) return;

        pendingRequests[prize.ShipId] = new PendingRequest(elapsed, captorShip.ShipId);
        network.Send(prize.CurrentAuthority, request);
        captureRequests++;
        Logger.Information("[NavalSync] Asked {Authority} to confirm capturing hull {ShipId} with hull {CaptorShipId}",
            prize.CurrentAuthority, prize.ShipId, captorShip.ShipId);
    }

    // [Poller thread] Every hull lookup waits for the game thread, behind the registrations queued before it.
    private void Handle_NetworkShipCaptureRequest(MessagePayload<NetworkShipCaptureRequest> payload)
    {
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptRequest(request);
        }, context: nameof(Handle_NetworkShipCaptureRequest));
    }

    /// <summary>[Game thread] Decides a request sent to this client; a hull it doesn't simulate (any more) is refused as such.</summary>
    internal void AcceptRequest(NetworkShipCaptureRequest request)
    {
        if (disposed || request == null) return;

        if (!Registry.TryGet(request.ShipId, out var prize))
        {
            Refuse(request.ShipId, request.CaptorControllerId, ShipCaptureRefusal.NotAuthority);
            return;
        }

        DecideRequest(prize, request);
    }

    // [Game thread] The authority's decision: the applier applies and broadcasts the capture, or the captor hears why not.
    private void DecideRequest(NetworkShipInfo prize, NetworkShipCaptureRequest request)
    {
        Registry.TryGet(request.CaptorShipId, out var captorShip);
        var refusal = Decide(FactsOf(prize, request.CaptorControllerId, captorShip));
        if (refusal != null)
        {
            Refuse(prize.ShipId, request.CaptorControllerId, refusal.Value);
            return;
        }

        applier.ApplyDecided(prize, captorShip, request.CaptorControllerId);
    }

    // What the authority knows about a capture request; a missing captor hull reads as invalid.
    private CaptureFacts FactsOf(NetworkShipInfo prize, string captor, NetworkShipInfo captorShip)
    {
        NoticePromotion();
        bool captorHullValid = captorShip != null && captorShip != prize && captorShip.CurrentAuthority == captor
            && !captorShip.IsOrphaned && !IsGoing(captorShip);
        var prizeSide = engine.SideOf(prize.Hull);
        var captorSide = captorShip == null ? BattleSideEnum.None : engine.SideOf(captorShip.Hull);
        // An orphan has no team, so only the captor that left it may take it back (vanilla: its own formationless hull).
        bool isEnemy = prize.IsOrphaned
            ? prize.CurrentAuthority == captor
            : IsBattleSide(prizeSide) && IsBattleSide(captorSide) && prizeSide != captorSide;

        return new CaptureFacts(
            isAuthority: IsOwn(prize),
            alreadyCaptured: captorShip != null && captorShip.IsOrphaned && prize.CaptorControllerId == captor,
            captorInMission: session.IsOwn(captor) || missionContext.ControllersInMission.Contains(captor),
            captorHullValid: captorHullValid,
            isEnemy: isEnemy,
            busy: IsGoing(prize) || hullAuthority.IsReplacing(prize.ShipId) || IsSettlingAfterPromotion(prize)
                || IsCaptorHullOfAPendingRequest(prize.ShipId),
            canBeTakenOver: captureEngine.CanBeTakenOver(prize.Hull),
            isSinking: engine.IsSinking(prize.Hull),
            isVacant: captureEngine.IsVacantForCapture(prize.Hull),
            swapReady: captorShip != null && captureEngine.CheckCapture(prize.Hull, captorShip.Hull) == null);
    }

    /// <summary>Why the hull's authority refuses a capture, or null to confirm it; the first failing check names it.</summary>
    internal static ShipCaptureRefusal? Decide(CaptureFacts facts)
    {
        if (!facts.IsAuthority) return ShipCaptureRefusal.NotAuthority;
        if (facts.AlreadyCaptured) return ShipCaptureRefusal.AlreadyCaptured;
        if (!facts.CaptorInMission) return ShipCaptureRefusal.CaptorNotInMission;
        if (!facts.CaptorHullValid) return ShipCaptureRefusal.CaptorHullInvalid;
        if (!facts.IsEnemy) return ShipCaptureRefusal.NotEnemy;
        if (facts.Busy) return ShipCaptureRefusal.Busy;
        if (!facts.CanBeTakenOver) return ShipCaptureRefusal.CannotBeTakenOver;
        if (facts.IsSinking) return ShipCaptureRefusal.Sinking;
        if (!facts.IsVacant) return ShipCaptureRefusal.NotVacant;
        if (!facts.SwapReady) return ShipCaptureRefusal.SwapUnavailable;
        return null;
    }

    // A refusal goes to the captor alone, or straight to the player when this client is the captor.
    private void Refuse(Guid shipId, string captor, ShipCaptureRefusal reason)
    {
        refusals.TryGetValue(reason, out long count);
        refusals[reason] = count + 1;
        Logger.Information("[NavalSync] Refused capturing hull {ShipId} for {Captor}: {Reason}", shipId, captor, reason);
        if (session.IsOwn(captor))
        {
            TellPlayer(reason);
            return;
        }

        network.Send(captor, new NetworkShipCaptureRejected(shipId, captor, reason));
    }

    // [Poller thread] Marshals a refusal onto the game thread, where the captor's pending requests live.
    private void Handle_NetworkShipCaptureRejected(MessagePayload<NetworkShipCaptureRejected> payload)
    {
        var rejection = payload.What;
        GameThread.RunSafe(() => AcceptRejection(rejection), context: nameof(Handle_NetworkShipCaptureRejected));
    }

    /// <summary>[Game thread] The captor's answer to a refusal: the player hears it once, for a request still waiting.</summary>
    internal void AcceptRejection(NetworkShipCaptureRejected rejection)
    {
        if (disposed || rejection == null || !session.IsOwn(rejection.CaptorControllerId)) return;
        if (!pendingRequests.Remove(rejection.ShipId)) return;

        Logger.Information("[NavalSync] Capturing hull {ShipId} was refused: {Reason}", rejection.ShipId, rejection.Reason);
        TellPlayer(rejection.Reason);
    }

    // A capture applied here ends this client's request for the prize; when someone else's won, the player hears it lost.
    private void OnCaptureApplied(Guid prizeId, string captor)
    {
        if (pendingRequests.Remove(prizeId) && !session.IsOwn(captor)) TellPlayer(ShipCaptureRefusal.AlreadyCaptured);
    }

    // Nobody answered: the authority left before it decided, so the player can try again at the successor.
    private void ExpirePendingRequests()
    {
        foreach (var entry in pendingRequests.Where(entry => elapsed - entry.Value.SentAt > RequestTimeoutSeconds).ToArray())
        {
            pendingRequests.Remove(entry.Key);
            captureExpired++;
            Logger.Information("[NavalSync] Capture request for hull {ShipId} got no answer in {Seconds} s", entry.Key,
                RequestTimeoutSeconds);
            TellPlayer(ShipCaptureRefusal.NoAnswer);
        }
    }

    // Starts the settle window when this client became the battle host of a newer epoch than the first one it saw.
    private void NoticePromotion()
    {
        if (session.HostEpoch == seenEpoch) return;

        bool promoted = seenEpoch != 0 && session.IsLocalHost;
        seenEpoch = session.HostEpoch;
        if (promoted) promotedAt = elapsed;
    }

    // This client asked to capture another hull with this one: that hull's authority may confirm a capture at this hull's next
    // place in its order, so deciding a capture of this hull too would give that place two deciders.
    private bool IsCaptorHullOfAPendingRequest(Guid shipId) => pendingRequests.Values.Any(pending => pending.CaptorShipId == shipId);

    // An inherited AI hull may still have a capture the old host confirmed on its way here.
    private bool IsSettlingAfterPromotion(NetworkShipInfo prize) =>
        IsSettling(prize.FollowsBattleHost, promotedAt.HasValue, elapsed - (promotedAt ?? 0f));

    /// <summary>Whether a promoted host still refuses an AI hull's capture as busy, inside the settle window.</summary>
    internal static bool IsSettling(bool followsHost, bool promoted, float sincePromotion) =>
        followsHost && promoted && sincePromotion < PromotionSettleSeconds;

    // Vanilla has no failed capture, so the player gets one line naming why.
    private void TellPlayer(ShipCaptureRefusal reason) =>
        messageBroker.Publish(this, new SendInformationMessage("The ship could not be captured: " + DescribeReason(reason) + "."));

    /// <summary>The player-facing words for a capture refusal.</summary>
    internal static string DescribeReason(ShipCaptureRefusal reason)
    {
        switch (reason)
        {
            case ShipCaptureRefusal.NotVacant: return "its crew still holds it";
            case ShipCaptureRefusal.Sinking: return "it is sinking";
            case ShipCaptureRefusal.Busy: return "it is changing hands, try again";
            case ShipCaptureRefusal.AlreadyCaptured: return "someone captured it first";
            case ShipCaptureRefusal.NotEnemy: return "it is not an enemy ship";
            case ShipCaptureRefusal.CannotBeTakenOver: return "it can't be taken over";
            case ShipCaptureRefusal.CaptorHullInvalid: return "your own ship can't take it";
            case ShipCaptureRefusal.NoAnswer: return "nobody answered, try again";
            case ShipCaptureRefusal.SwapUnavailable: return "the ships can't change hands here";
            case ShipCaptureRefusal.NotAuthority: return "it changed hands, try again";
            default: return "you are not in the battle";
        }
    }

    // Whether this client simulates the hull now.
    private bool IsOwn(NetworkShipInfo ship) => session.IsOwn(ship.CurrentAuthority);

    // A hull queued to leave or already gone can't take part in a capture.
    private bool IsGoing(NetworkShipInfo ship) =>
        engine.IsHullRemoved(ship.Hull) || hullAuthority.HullsBeingRemoved.Contains(ship.Hull);

    // An orphan's side reads as none; only a real side counts.
    private static bool IsBattleSide(BattleSideEnum side) => side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender;

    // Requests, refusals by reason, expiries, waiting requests with their age, and the applier's counters.
    public object Inspect() => new
    {
        captureRequests,
        captureExpired,
        captureRejections = refusals.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
        pendingRequests = pendingRequests.Select(entry => new
        {
            shipId = entry.Key,
            captorShipId = entry.Value.CaptorShipId,
            waitedSeconds = elapsed - entry.Value.SentAt,
        }).ToArray(),
        applied = applier.Inspect(),
    };
}

/// <summary>A request of this client waiting for the prize's authority: when it went out and the own hull it would capture with.</summary>
internal readonly struct PendingRequest
{
    public PendingRequest(float sentAt, Guid captorShipId)
    {
        SentAt = sentAt;
        CaptorShipId = captorShipId;
    }

    public float SentAt { get; }
    public Guid CaptorShipId { get; }
}

/// <summary>What a captured hull's authority checks before it confirms a capture.</summary>
public readonly struct CaptureFacts
{
    public CaptureFacts(bool isAuthority, bool alreadyCaptured, bool captorInMission, bool captorHullValid, bool isEnemy, bool busy,
        bool canBeTakenOver, bool isSinking, bool isVacant, bool swapReady)
    {
        IsAuthority = isAuthority;
        AlreadyCaptured = alreadyCaptured;
        CaptorInMission = captorInMission;
        CaptorHullValid = captorHullValid;
        IsEnemy = isEnemy;
        Busy = busy;
        CanBeTakenOver = canBeTakenOver;
        IsSinking = isSinking;
        IsVacant = isVacant;
        SwapReady = swapReady;
    }

    /// <summary>This client simulates the prize now.</summary>
    public bool IsAuthority { get; }
    /// <summary>The captor already holds the prize from this same capture.</summary>
    public bool AlreadyCaptured { get; }
    public bool CaptorInMission { get; }
    /// <summary>The captor's hull is registered to it, still has its formation and isn't leaving.</summary>
    public bool CaptorHullValid { get; }
    /// <summary>The prize is on the other side from the captor's hull, or is the captor's own orphan.</summary>
    public bool IsEnemy { get; }
    /// <summary>The prize is leaving, mid-swap, an inherited AI hull inside a promoted host's settle window, or this client's own hull a request of it waits on.</summary>
    public bool Busy { get; }
    public bool CanBeTakenOver { get; }
    public bool IsSinking { get; }
    /// <summary>Vanilla's vacancy on the real hull: its formation all swimming or gone and nothing queued.</summary>
    public bool IsVacant { get; }
    /// <summary>Vanilla's swap can run on the two hulls here (INavalShipCaptureEngine.CheckCapture).</summary>
    public bool SwapReady { get; }
}
