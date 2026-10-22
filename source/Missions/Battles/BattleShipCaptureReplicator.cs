using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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
/// Replicates vanilla's in-mission ship capture. The captor runs vanilla's helm timer and asks the hull's current authority,
/// which decides on the real crew and reserve; every client then replays vanilla's swap and the prize moves to its captor in place.
/// </summary>
public interface IBattleShipCaptureReplicator : IDisposable
{
    /// <summary>[Game thread] Applies confirms that waited for a hull and expires requests no authority answered.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Capture counters, pending requests and held confirms (diagnostics).</summary>
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
    /// <summary>How long a confirm waits for a hull this client has not registered yet before it is dropped.</summary>
    internal const float HeldConfirmLifetimeSeconds = 30f;

    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly IMissionContext missionContext;
    private readonly INavalShipEngine engine;
    private readonly IBattleShipReplicator ships;
    private readonly INavalHullAuthority hullAuthority;

    // [Game thread] This client's requests waiting for the authority, by prize ship id, with when each went out.
    private readonly Dictionary<Guid, float> pendingRequests = new Dictionary<Guid, float>();
    // [Game thread] Confirms for a hull not registered here yet or mid-swap, with when each arrived.
    private readonly List<HeldConfirm> heldConfirms = new List<HeldConfirm>();
    // [Game thread] Prizes this client confirmed as their authority, so a late rival request gets an answer.
    private readonly HashSet<Guid> confirmedHere = new HashSet<Guid>();
    private readonly Dictionary<string, long> rejections = new Dictionary<string, long>();
    private float elapsed;
    private long requestsSent, requestsIgnored, capturesApplied, capturesExpired, confirmsDropped, duplicateConfirms;
    private string lastDrop;
    private bool disposed;

    public BattleShipCaptureReplicator(IBattleNetwork network, INetwork relayNetwork, IMessageBroker messageBroker,
        BattleContext context, INavalShipEngine engine, IBattleShipReplicator ships, INavalHullAuthority hullAuthority)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        session = context.Session;
        missionComponent = context.MissionComponent;
        missionContext = context.MissionContext;
        this.engine = engine;
        this.ships = ships;
        this.hullAuthority = hullAuthority;

        messageBroker.Subscribe<ShipCaptureTimerElapsed>(Handle_ShipCaptureTimerElapsed);
        messageBroker.Subscribe<NetworkShipCaptureRequest>(Handle_NetworkShipCaptureRequest);
        messageBroker.Subscribe<NetworkShipCaptured>(Handle_NetworkShipCaptured);
        messageBroker.Subscribe<NetworkShipCaptureRejected>(Handle_NetworkShipCaptureRejected);
    }

    // Drops every subscription and whatever still waits.
    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<ShipCaptureTimerElapsed>(Handle_ShipCaptureTimerElapsed);
        messageBroker.Unsubscribe<NetworkShipCaptureRequest>(Handle_NetworkShipCaptureRequest);
        messageBroker.Unsubscribe<NetworkShipCaptured>(Handle_NetworkShipCaptured);
        messageBroker.Unsubscribe<NetworkShipCaptureRejected>(Handle_NetworkShipCaptureRejected);
        pendingRequests.Clear();
        heldConfirms.Clear();
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    // Retries held confirms first, so one that waited for this tick's spawn applies before a request could expire on it.
    public void Tick(float dt)
    {
        if (disposed) return;

        elapsed += Math.Max(0f, dt);
        RetryHeldConfirms();
        ExpirePendingRequests();
    }

    public float? PendingRequestAge(Guid shipId) =>
        pendingRequests.TryGetValue(shipId, out float sentAt) ? elapsed - sentAt : (float?)null;

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
            Refuse(prize.ShipId, session.OwnControllerId, "captor_hull_invalid");
            return;
        }

        RequestCapture(prize, captorShip);
    }

    /// <summary>[Game thread] Decides a capture of a hull this client simulates, or asks its authority once.</summary>
    internal void RequestCapture(NetworkShipInfo prize, NetworkShipInfo captorShip)
    {
        var request = new NetworkShipCaptureRequest(prize.ShipId, session.OwnControllerId, captorShip.ShipId, session.HostEpoch);
        if (IsOwn(prize))
        {
            DecideRequest(prize, request);
            return;
        }

        if (pendingRequests.ContainsKey(prize.ShipId)) return;

        pendingRequests[prize.ShipId] = elapsed;
        network.SendAll(request);
        requestsSent++;
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

    /// <summary>
    /// [Game thread] Only the prize's current authority decides a request; one that already gave the prize to another captor
    /// tells a late rival so, and everyone else ignores it.
    /// </summary>
    internal void AcceptRequest(NetworkShipCaptureRequest request)
    {
        if (disposed || request == null || session.IsOwn(request.CaptorControllerId)) return;

        if (!Registry.TryGet(request.ShipId, out var prize) || !IsOwn(prize))
        {
            if (prize != null && confirmedHere.Contains(prize.ShipId) && prize.CaptorControllerId != request.CaptorControllerId)
            {
                Refuse(request.ShipId, request.CaptorControllerId, "already_captured");
                return;
            }

            requestsIgnored++;
            return;
        }

        DecideRequest(prize, request);
    }

    // [Game thread] The authority's decision: apply and broadcast the capture, or refuse it.
    private void DecideRequest(NetworkShipInfo prize, NetworkShipCaptureRequest request)
    {
        Registry.TryGet(request.CaptorShipId, out var captorShip);
        var reason = Decide(FactsOf(prize, request.CaptorControllerId, captorShip));
        if (reason != null)
        {
            Refuse(prize.ShipId, request.CaptorControllerId, reason);
            return;
        }

        var confirm = new NetworkShipCaptured(prize.ShipId, request.CaptorControllerId, captorShip.ShipId, session.OwnControllerId,
            session.HostEpoch);
        confirmedHere.Add(prize.ShipId);
        Apply(prize, captorShip, request.CaptorControllerId);
        network.SendAll(confirm);
    }

    // What the authority knows about a capture request; a missing captor hull reads as invalid.
    private CaptureFacts FactsOf(NetworkShipInfo prize, string captor, NetworkShipInfo captorShip)
    {
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
            busy: IsGoing(prize) || hullAuthority.IsReplacing(prize.ShipId),
            canBeTakenOver: engine.CanBeTakenOver(prize.Hull),
            isSinking: engine.IsSinking(prize.Hull),
            isVacant: engine.IsVacantForCapture(prize.Hull));
    }

    /// <summary>Why the hull's authority refuses a capture, or null to confirm it; the first failing check names it.</summary>
    internal static string Decide(CaptureFacts facts)
    {
        if (!facts.IsAuthority) return "not_authority";
        if (facts.AlreadyCaptured) return "already_captured";
        if (!facts.CaptorInMission) return "captor_not_in_mission";
        if (!facts.CaptorHullValid) return "captor_hull_invalid";
        if (!facts.IsEnemy) return "not_enemy";
        if (facts.Busy) return "busy";
        if (!facts.CanBeTakenOver) return "cannot_be_taken_over";
        if (facts.IsSinking) return "sinking";
        if (!facts.IsVacant) return "not_vacant";
        return null;
    }

    // A refusal goes to the captor, or straight to the player when this client is the captor.
    private void Refuse(Guid shipId, string captor, string reason)
    {
        rejections.TryGetValue(reason, out long count);
        rejections[reason] = count + 1;
        Logger.Information("[NavalSync] Refused capturing hull {ShipId} for {Captor}: {Reason}", shipId, captor, reason);
        if (session.IsOwn(captor))
        {
            TellPlayer(reason);
            return;
        }

        network.SendAll(new NetworkShipCaptureRejected(shipId, captor, reason));
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

    // [Poller thread] Marshals a capture onto the game thread, behind the spawn records and samples queued before it.
    private void Handle_NetworkShipCaptured(MessagePayload<NetworkShipCaptured> payload)
    {
        var confirm = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptConfirm(confirm);
        }, context: nameof(Handle_NetworkShipCaptured));
    }

    /// <summary>[Game thread] Applies an authority's capture, holds it for a hull not ready here, or drops it with the reason.</summary>
    internal void AcceptConfirm(NetworkShipCaptured confirm)
    {
        if (disposed || confirm == null) return;

        var outcome = TryApplyConfirm(confirm);
        if (outcome == ConfirmOutcome.Hold) heldConfirms.Add(new HeldConfirm(confirm, elapsed));
    }

    // Applies the confirm when both hulls are here and settled; a confirm is exempt from the membership gate, since mesh routes
    // aren't ordered against the server's departure and migration events.
    private ConfirmOutcome TryApplyConfirm(NetworkShipCaptured confirm)
    {
        if (!Registry.TryGet(confirm.ShipId, out var prize))
            return Registry.IsGone(confirm.ShipId) ? Drop(confirm, "prize_gone") : ConfirmOutcome.Hold;

        if (!Registry.TryGet(confirm.CaptorShipId, out var captorShip))
        {
            if (!Registry.IsGone(confirm.CaptorShipId)) return ConfirmOutcome.Hold;

            return LeavesWithDepartedCaptor(prize, confirm);
        }

        if (hullAuthority.IsReplacing(prize.ShipId) || hullAuthority.IsReplacing(captorShip.ShipId)) return ConfirmOutcome.Hold;

        var rejection = ValidateConfirm(prize, captorShip, confirm);
        if (rejection == AlreadyApplied)
        {
            duplicateConfirms++;
            return ConfirmOutcome.Done;
        }

        if (rejection != null) return Drop(confirm, rejection);

        Apply(prize, captorShip, confirm.CaptorControllerId);
        return ConfirmOutcome.Done;
    }

    internal const string AlreadyApplied = "already_applied";

    /// <summary>
    /// Why a capture confirm must be dropped, <see cref="AlreadyApplied"/> for a repeat of one applied here (a successor
    /// confirming what the old host already did), or null to apply it. Its sender must have simulated the prize at some time.
    /// </summary>
    internal static string ValidateConfirm(NetworkShipInfo prize, NetworkShipInfo captorShip, NetworkShipCaptured confirm)
    {
        if (captorShip.IsOrphaned && prize.CaptorControllerId == confirm.CaptorControllerId) return AlreadyApplied;
        if (captorShip.IsOrphaned || captorShip == prize || captorShip.CurrentAuthority != confirm.CaptorControllerId)
            return "captor_hull_invalid";
        if (!prize.HasBeenAuthority(confirm.PreviousAuthority)) return "not_authority";
        return null;
    }

    // The captor's hull left this client's battle before its capture arrived: elsewhere the prize left with it, so it goes too.
    private ConfirmOutcome LeavesWithDepartedCaptor(NetworkShipInfo prize, NetworkShipCaptured confirm)
    {
        if (missionContext.ControllersInMission.Contains(confirm.CaptorControllerId) || !prize.HasBeenAuthority(confirm.PreviousAuthority))
            return Drop(confirm, "captor_hull_invalid");

        Logger.Information("[NavalSync] Capture of hull {ShipId} by {Captor} arrived after the captor left; the hull leaves with it",
            prize.ShipId, confirm.CaptorControllerId);
        ships.RemoveHullOfDepartedCaptor(prize);
        return ConfirmOutcome.Done;
    }

    // [Game thread] One capture on this client, in order: the authority moves in place, vanilla's swap runs on the local hulls,
    // the registry follows it, and the prize counts as gone from its old side for the retreat rule.
    private void Apply(NetworkShipInfo prize, NetworkShipInfo captorShip, string captor)
    {
        var prizeSide = engine.SideOf(prize.Hull);
        var captorSide = engine.SideOf(captorShip.Hull);
        string previousAuthority = prize.CurrentAuthority;

        ships.TransferCapturedHull(prize, captor);
        engine.ApplyCapture(prize.Hull, captorShip.Hull);
        Registry.TryRecordCapture(prize.ShipId, captorShip.ShipId, captor, captorSide);
        if (IsBattleSide(prizeSide) && prizeSide != captorSide) ships.CountCapturedFrom(prizeSide);

        // This client's own request for the prize lost the race, which its player hears about here.
        if (pendingRequests.Remove(prize.ShipId) && !session.IsOwn(captor)) TellPlayer("already_captured");
        capturesApplied++;
        Logger.Information("[NavalSync] Hull {ShipId} of {Previous} captured by {Captor} into hull {CaptorShipId}'s formation; that hull is orphaned",
            prize.ShipId, previousAuthority, captor, captorShip.ShipId);
        // The victim's formation record now names the captor's orphan, so a later reinforcement of it would target a copy.
        if (session.IsOwn(previousAuthority) && !prize.IsNpcParty && !session.IsOwn(captor))
            Logger.Warning("[NavalSync] This client's hull {ShipId} was captured by {Captor}; its formation has no hull of its own left",
                prize.ShipId, captor);
        if (session.IsOwn(captor)) ReportCaptureToServer(prize);
    }

    // On the relay channel this client's fates use, so the server knows the captor before the prize's first fate from it.
    private void ReportCaptureToServer(NetworkShipInfo prize)
    {
        var campaignShipId = engine.CampaignShipIdOf(prize.Hull);
        if (string.IsNullOrEmpty(campaignShipId) || !session.HasInstance) return;

        relayNetwork.SendAll(new NetworkBattleShipCaptured(session.InstanceId, campaignShipId));
    }

    // Counts and logs a confirm this client won't apply.
    private ConfirmOutcome Drop(NetworkShipCaptured confirm, string reason)
    {
        confirmsDropped++;
        lastDrop = reason;
        Logger.Information("[NavalSync] Dropped the capture of hull {ShipId} by {Captor} from {Sender}: {Reason}",
            confirm.ShipId, confirm.CaptorControllerId, confirm.PreviousAuthority, reason);
        return ConfirmOutcome.Done;
    }

    // In arrival order; a confirm still waiting past its lifetime is dropped, as its hull never came.
    private void RetryHeldConfirms()
    {
        if (heldConfirms.Count == 0) return;

        foreach (var held in heldConfirms.ToArray())
        {
            heldConfirms.Remove(held);
            if (TryApplyConfirm(held.Confirm) != ConfirmOutcome.Hold) continue;

            if (elapsed - held.ArrivedAt > HeldConfirmLifetimeSeconds) Drop(held.Confirm, "hull_never_registered");
            else heldConfirms.Add(held);
        }
    }

    // Nobody answered: the authority left before it decided, so the player can try again at the successor.
    private void ExpirePendingRequests()
    {
        foreach (var entry in pendingRequests.Where(entry => elapsed - entry.Value > RequestTimeoutSeconds).ToArray())
        {
            pendingRequests.Remove(entry.Key);
            capturesExpired++;
            Logger.Information("[NavalSync] Capture request for hull {ShipId} got no answer in {Seconds} s", entry.Key,
                RequestTimeoutSeconds);
            TellPlayer("no_answer");
        }
    }

    // Vanilla has no failed capture, so the player gets one line naming why.
    private void TellPlayer(string reason) =>
        messageBroker.Publish(this, new SendInformationMessage("The ship could not be captured: " + DescribeReason(reason) + "."));

    /// <summary>The player-facing words for a capture refusal reason.</summary>
    internal static string DescribeReason(string reason)
    {
        switch (reason)
        {
            case "not_vacant": return "its crew still holds it";
            case "sinking": return "it is sinking";
            case "busy": return "it is changing hands, try again";
            case "already_captured": return "someone captured it first";
            case "not_enemy": return "it is not an enemy ship";
            case "cannot_be_taken_over": return "it can't be taken over";
            case "captor_hull_invalid": return "your own ship can't take it";
            case "no_answer": return "nobody answered, try again";
            default: return reason;
        }
    }

    // Whether this client simulates the hull now.
    private bool IsOwn(NetworkShipInfo ship) => session.IsOwn(ship.CurrentAuthority);

    // A hull queued to leave or already gone can't take part in a capture.
    private bool IsGoing(NetworkShipInfo ship) =>
        engine.IsHullRemoved(ship.Hull) || hullAuthority.HullsBeingRemoved.Contains(ship.Hull);

    // An orphan's side reads as none; only a real side counts for the retreat rule.
    private static bool IsBattleSide(BattleSideEnum side) => side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender;

    // Counters, rejections by reason, waiting requests with their age and held confirms.
    public object Inspect() => new
    {
        requestsSent,
        requestsIgnored,
        capturesApplied,
        capturesExpired,
        confirmsDropped,
        duplicateConfirms,
        lastDrop,
        captureRejections = rejections.ToDictionary(entry => entry.Key, entry => entry.Value),
        pendingRequests = pendingRequests.Select(entry => new { shipId = entry.Key, waitedSeconds = elapsed - entry.Value }).ToArray(),
        heldConfirms = heldConfirms.Select(held => held.Confirm.ShipId).ToArray(),
    };

    private enum ConfirmOutcome
    {
        Done,
        Hold,
    }

    // A confirm waiting for its hulls, with when it arrived.
    private sealed class HeldConfirm
    {
        public HeldConfirm(NetworkShipCaptured confirm, float arrivedAt)
        {
            Confirm = confirm;
            ArrivedAt = arrivedAt;
        }

        public NetworkShipCaptured Confirm { get; }
        public float ArrivedAt { get; }
    }
}

/// <summary>What a captured hull's authority checks before it confirms a capture.</summary>
public readonly struct CaptureFacts
{
    public CaptureFacts(bool isAuthority, bool alreadyCaptured, bool captorInMission, bool captorHullValid, bool isEnemy, bool busy,
        bool canBeTakenOver, bool isSinking, bool isVacant)
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
    /// <summary>The prize is queued to leave or mid-swap here.</summary>
    public bool Busy { get; }
    public bool CanBeTakenOver { get; }
    public bool IsSinking { get; }
    /// <summary>Vanilla's vacancy on the real hull: its formation all swimming or gone and nothing queued.</summary>
    public bool IsVacant { get; }
}
