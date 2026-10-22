using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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
/// Applies in-mission ship captures on this client in each hull's capture order: the authority's own decision, every
/// confirm from the mesh, and a capture a later spawn record shows this client missed.
/// </summary>
public interface IShipCaptureApplier : IDisposable
{
    /// <summary>
    /// [Game thread] The prize's authority decided the capture: it applies it here first, then sends every peer the confirm,
    /// numbered by both hulls' capture counts.
    /// </summary>
    void ApplyDecided(NetworkShipInfo prize, NetworkShipInfo captorShip, string captorControllerId);

    /// <summary>[Game thread] Raised once a capture applied here, with the prize's ship id and its captor.</summary>
    event Action<Guid, string> CaptureApplied;

    /// <summary>[Game thread] Applies confirms and records whose hulls are ready now.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Applied, held, dropped and conflicting captures (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IShipCaptureApplier"/>
public class ShipCaptureApplier : IShipCaptureApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<ShipCaptureApplier>();

    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly IMissionContext missionContext;
    private readonly INavalShipEngine engine;
    private readonly INavalShipCaptureEngine captureEngine;
    private readonly IBattleShipReplicator ships;
    private readonly INavalHullAuthority hullAuthority;

    // [Game thread] Confirms waiting for a hull this client hasn't registered yet, for a swap, or for an earlier capture.
    private readonly List<NetworkShipCaptured> heldConfirms = new List<NetworkShipCaptured>();
    // [Game thread] Records of missed captures waiting for a hull swap here to finish.
    private readonly List<BattleShipSpawnData> heldRecords = new List<BattleShipSpawnData>();
    // [Game thread] The capture applied at each place in each hull's order, to tell a late repeat from a conflict.
    private readonly Dictionary<(Guid ShipId, int Sequence), AppliedCapture> appliedAt = new Dictionary<(Guid, int), AppliedCapture>();
    private long capturesApplied, recordsApplied, confirmsDropped, duplicateConfirms, captureConflicts, swapsBlocked;
    private string lastDrop;
    private bool disposed;

    public ShipCaptureApplier(IBattleNetwork network, INetwork relayNetwork, IMessageBroker messageBroker, BattleContext context,
        INavalShipEngine engine, INavalShipCaptureEngine captureEngine, IBattleShipReplicator ships, INavalHullAuthority hullAuthority)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        session = context.Session;
        missionComponent = context.MissionComponent;
        missionContext = context.MissionContext;
        this.engine = engine;
        this.captureEngine = captureEngine;
        this.ships = ships;
        this.hullAuthority = hullAuthority;

        messageBroker.Subscribe<NetworkShipCaptured>(Handle_NetworkShipCaptured);
        // Subscribed after the ship replicator's, so a record that spawns a hull is spawned before it is read here.
        messageBroker.Subscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
    }

    public event Action<Guid, string> CaptureApplied;

    // Drops the subscriptions and every confirm still waiting.
    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<NetworkShipCaptured>(Handle_NetworkShipCaptured);
        messageBroker.Unsubscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        heldConfirms.Clear();
        heldRecords.Clear();
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public void Tick(float dt)
    {
        if (disposed) return;

        RetryHeldConfirms();
        RetryHeldRecords();
    }

    // The confirm goes out first, so peers take the capture through it, not through the records a captor's apply sends. A swap
    // that can't run here goes nowhere, so no peer applies a capture its decider didn't.
    public void ApplyDecided(NetworkShipInfo prize, NetworkShipInfo captorShip, string captorControllerId)
    {
        if (SwapBlocked(prize, captorShip, captorControllerId) != null) return;

        var confirm = new NetworkShipCaptured(prize.ShipId, captorControllerId, captorShip.ShipId, session.OwnControllerId,
            session.HostEpoch, prize.CaptureSequence, captorShip.CaptureSequence);
        network.SendAll(confirm);
        // A live mesh hands the captor's answering records to a later frame, but a mesh that delivers at once can bring them
        // back first and the capture already applied here through them; either way it applies once.
        if (prize.CaptureSequence != confirm.PrizeSequence || captorShip.CaptureSequence != confirm.CaptorSequence) return;

        Apply(prize, captorShip, captorControllerId, prize.CaptureSequence + 1);
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

    /// <summary>[Game thread] Applies a confirm in its hulls' capture order, holds it until they are ready, or drops it with the reason.</summary>
    internal void AcceptConfirm(NetworkShipCaptured confirm)
    {
        if (disposed || confirm == null) return;

        if (TryApplyConfirm(confirm) == ConfirmOutcome.Hold) heldConfirms.Add(confirm);
    }

    // A confirm is exempt from the membership gate, since mesh routes aren't ordered against the server's departure and
    // migration events; each hull's capture order decides instead, and only the hull's authority decides a place in it.
    private ConfirmOutcome TryApplyConfirm(NetworkShipCaptured confirm)
    {
        if (!Registry.TryGet(confirm.ShipId, out var prize))
            return Registry.IsGone(confirm.ShipId) ? Drop(confirm, "prize_gone") : ConfirmOutcome.Hold;

        if (!Registry.TryGet(confirm.CaptorShipId, out var captorShip))
            return Registry.IsGone(confirm.CaptorShipId) ? LeavesWithDepartedCaptor(prize, confirm) : ConfirmOutcome.Hold;

        if (hullAuthority.IsReplacing(prize.ShipId) || hullAuthority.IsReplacing(captorShip.ShipId)) return ConfirmOutcome.Hold;

        var order = OrderOf(prize.CaptureSequence, captorShip.CaptureSequence, confirm.PrizeSequence, confirm.CaptorSequence);
        if (order == CaptureOrder.Ahead) return ConfirmOutcome.Hold;
        if (order == CaptureOrder.Behind) return Stale(prize, confirm);

        var rejection = ValidateSender(prize, confirm.PreviousAuthority);
        if (rejection != null) return Drop(confirm, rejection);

        var blocker = SwapBlocked(prize, captorShip, confirm.CaptorControllerId);
        if (blocker != null) return Drop(confirm, "swap_blocked:" + blocker);

        Apply(prize, captorShip, confirm.CaptorControllerId, confirm.PrizeSequence + 1);
        return ConfirmOutcome.Done;
    }

    // Why vanilla's swap can't run on the two hulls here, logged with the capture, or null. Checked before anything changes.
    private string SwapBlocked(NetworkShipInfo prize, NetworkShipInfo captorShip, string captor)
    {
        var blocker = captureEngine.CheckCapture(prize.Hull, captorShip.Hull);
        if (blocker == null) return null;

        swapsBlocked++;
        Logger.Warning("[NavalSync] Not applying the capture of hull {ShipId} by {Captor} with hull {CaptorShipId}: {Reason}",
            prize.ShipId, captor, captorShip.ShipId, blocker);
        return blocker;
    }

    /// <summary>
    /// Where a confirm stands against this client's capture counts of its two hulls: next in line, still waiting for an earlier
    /// capture of either, or behind a capture already applied here.
    /// </summary>
    internal static CaptureOrder OrderOf(int localPrize, int localCaptor, int confirmPrize, int confirmCaptor)
    {
        if (localPrize > confirmPrize || localCaptor > confirmCaptor) return CaptureOrder.Behind;
        if (localPrize < confirmPrize || localCaptor < confirmCaptor) return CaptureOrder.Ahead;
        return CaptureOrder.Next;
    }

    /// <summary>
    /// Why a confirm's sender may not decide the prize's capture, or null: a hull that doesn't follow the battle host changes
    /// authority only through captures, so at its place in the order the sender must be its current authority. An AI hull's
    /// authority also moves with host changes, which aren't ordered against confirms, so the order alone guards it.
    /// </summary>
    internal static string ValidateSender(NetworkShipInfo prize, string sender) =>
        prize.FollowsBattleHost || sender == prize.CurrentAuthority ? null : "not_authority";

    // A late repeat of a capture applied here changes nothing. It is a conflict only when this client applied a different
    // capture at the confirm's place in either hull's order, which one decider per place should make impossible, so it is logged.
    private ConfirmOutcome Stale(NetworkShipInfo prize, NetworkShipCaptured confirm)
    {
        var capture = new AppliedCapture(confirm.ShipId, confirm.CaptorShipId, confirm.CaptorControllerId);
        if (!Conflicts(AppliedAt(confirm.ShipId, confirm.PrizeSequence + 1), AppliedAt(confirm.CaptorShipId, confirm.CaptorSequence + 1),
                capture))
        {
            duplicateConfirms++;
            return ConfirmOutcome.Done;
        }

        captureConflicts++;
        Logger.Warning("[NavalSync] Capture of hull {ShipId} by {Captor} from {Sender} at sequence {Sequence} conflicts with the one " +
            "applied here (captor {Applied}, sequence {Local}); the first applied stands",
            confirm.ShipId, confirm.CaptorControllerId, confirm.PreviousAuthority, confirm.PrizeSequence, prize.CaptorControllerId,
            prize.CaptureSequence);
        return ConfirmOutcome.Done;
    }

    // The capture this client applied at a hull's place, or null for a place it never saw applied (a joiner's record).
    private AppliedCapture? AppliedAt(Guid shipId, int sequence) =>
        appliedAt.TryGetValue((shipId, sequence), out var applied) ? applied : (AppliedCapture?)null;

    /// <summary>
    /// Whether a late confirm conflicts with what a client applied at its places: a different capture there does, a repeat or
    /// a place the client never saw applied doesn't.
    /// </summary>
    internal static bool Conflicts(AppliedCapture? appliedAtPrizePlace, AppliedCapture? appliedAtCaptorPlace, AppliedCapture confirm) =>
        (appliedAtPrizePlace.HasValue && !appliedAtPrizePlace.Value.Equals(confirm))
        || (appliedAtCaptorPlace.HasValue && !appliedAtCaptorPlace.Value.Equals(confirm));

    // The captor's hull left this client's battle before its capture arrived: elsewhere the prize left with it, so it goes too.
    private ConfirmOutcome LeavesWithDepartedCaptor(NetworkShipInfo prize, NetworkShipCaptured confirm)
    {
        if (missionContext.ControllersInMission.Contains(confirm.CaptorControllerId)) return Drop(confirm, "captor_hull_gone");
        if (prize.CaptureSequence != confirm.PrizeSequence) return Stale(prize, confirm);

        var rejection = ValidateSender(prize, confirm.PreviousAuthority);
        if (rejection != null) return Drop(confirm, rejection);

        Logger.Information("[NavalSync] Capture of hull {ShipId} by {Captor} arrived after the captor left; the hull leaves with it",
            prize.ShipId, confirm.CaptorControllerId);
        // Its crew are still the old owner's, so nobody withdraws with it.
        ships.RemoveHulls(new[] { prize }, ShipRemovalReason.Leave, DepartingCrew.None);
        return ConfirmOutcome.Done;
    }

    // [Game thread] One capture on this client, in order: the authority moves in place, vanilla's swap runs on the local hulls,
    // the registry follows it and the prize counts as gone from its old side. The captor tells the server and its peers.
    private void Apply(NetworkShipInfo prize, NetworkShipInfo captorShip, string captor, int prizeSequence)
    {
        var prizeSide = engine.SideOf(prize.Hull);
        var captorSide = engine.SideOf(captorShip.Hull);
        var captorFormation = captorShip.Formation;
        string previousAuthority = prize.CurrentAuthority;

        int captorSequence = captorShip.CaptureSequence + 1;
        ships.TransferHull(prize, captor, inPlace: true);
        captureEngine.ApplyCapture(prize.Hull, captorShip.Hull);
        Registry.TryMarkCaptured(prize.ShipId, new HullCapture(captor, captorShip.ShipId, prizeSide), captorFormation, prizeSequence);
        Registry.TryMarkOrphaned(captorShip.ShipId, captorSide, captorSequence);
        var applied = new AppliedCapture(prize.ShipId, captorShip.ShipId, captor);
        appliedAt[(prize.ShipId, prizeSequence)] = applied;
        appliedAt[(captorShip.ShipId, captorSequence)] = applied;
        if (IsBattleSide(prizeSide) && prizeSide != captorSide) ships.CountCapturedFrom(prizeSide);

        capturesApplied++;
        Logger.Information("[NavalSync] Hull {ShipId} of {Previous} captured by {Captor} into hull {CaptorShipId}'s formation; that hull is orphaned",
            prize.ShipId, previousAuthority, captor, captorShip.ShipId);
        // The victim's formation record now names the captor's orphan, so a later reinforcement of it would target a copy.
        if (session.IsOwn(previousAuthority) && !prize.IsNpcParty && !session.IsOwn(captor))
            Logger.Warning("[NavalSync] This client's hull {ShipId} was captured by {Captor}; its formation has no hull of its own left",
                prize.ShipId, captor);

        CaptureApplied?.Invoke(prize.ShipId, captor);
        if (!session.IsOwn(captor)) return;

        ReportCaptureToServer(prize, prizeSequence);
        // A peer that took the prize out with its departed old authority before the confirm arrived spawns it from these.
        ships.AnnounceHulls(new[] { prize, captorShip });
    }

    // On the relay channel this client's fates use, so the server knows the captor before the prize's first fate from it.
    private void ReportCaptureToServer(NetworkShipInfo prize, int sequence)
    {
        var campaignShipId = engine.CampaignShipIdOf(prize.Hull);
        if (string.IsNullOrEmpty(campaignShipId) || !session.HasInstance) return;

        relayNetwork.SendAll(new NetworkBattleShipCaptured(session.InstanceId, campaignShipId, sequence));
    }

    // [Poller thread] Reads the records on the game thread, after the ship replicator spawned what they bring.
    private void Handle_NetworkSpawnBattleShips(MessagePayload<NetworkSpawnBattleShips> payload)
    {
        var records = payload.What.Ships ?? Array.Empty<BattleShipSpawnData>();
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptRecords(records);
        }, context: nameof(Handle_NetworkSpawnBattleShips));
    }

    /// <summary>
    /// [Game thread] A record from a hull's current authority showing captures this client missed (it never got their
    /// confirms) brings a registered hull up to date: a prize is captured through the swap, an orphan loses its formation.
    /// </summary>
    internal void AcceptRecords(IEnumerable<BattleShipSpawnData> records)
    {
        if (disposed) return;

        foreach (var record in records.Where(record => record != null && record.CaptureSequence > 0))
        {
            if (!Registry.TryGet(record.ShipId, out var ship) || record.CaptureSequence <= ship.CaptureSequence) continue;
            if (record.OwnerControllerId != ship.CurrentAuthority && record.OwnerControllerId != record.CaptorControllerId) continue;

            if (record.Orphaned) OrphanFromRecord(ship, record);
            else if (!string.IsNullOrEmpty(record.CaptorControllerId) && CaptureFromRecord(ship, record) == ConfirmOutcome.Hold)
                heldRecords.Add(record);
        }
    }

    // The swap already orphaned a hull when its prize's record came first; otherwise the record does it.
    private void OrphanFromRecord(NetworkShipInfo ship, BattleShipSpawnData record)
    {
        if (!ship.IsOrphaned) captureEngine.OrphanHull(ship.Hull);
        Registry.TryMarkOrphaned(ship.ShipId, record.Side, record.CaptureSequence);
        recordsApplied++;
    }

    // Replays the missed capture against the captor's hull the record names, once neither hull is mid-swap here. A captor's
    // hull that is missing or already orphaned can't take it (respawning the prize from the record is left out).
    private ConfirmOutcome CaptureFromRecord(NetworkShipInfo prize, BattleShipSpawnData record)
    {
        if (!Registry.TryGet(record.CapturedWithShipId, out var captorShip) || captorShip.IsOrphaned)
        {
            Logger.Warning("[NavalSync] Record of hull {ShipId} shows a capture by {Captor} this client missed, but the captor's hull " +
                "{CaptorShipId} can't take it here", prize.ShipId, record.CaptorControllerId, record.CapturedWithShipId);
            return ConfirmOutcome.Done;
        }

        if (hullAuthority.IsReplacing(prize.ShipId) || hullAuthority.IsReplacing(captorShip.ShipId)) return ConfirmOutcome.Hold;

        if (SwapBlocked(prize, captorShip, record.CaptorControllerId) != null) return ConfirmOutcome.Done;

        Logger.Information("[NavalSync] Record of hull {ShipId} shows a capture by {Captor} this client missed; applying it",
            prize.ShipId, record.CaptorControllerId);
        Apply(prize, captorShip, record.CaptorControllerId, record.CaptureSequence);
        recordsApplied++;
        return ConfirmOutcome.Done;
    }

    // A record waits only for a swap here; it is dropped once its hull is gone or caught up by a confirm.
    private void RetryHeldRecords()
    {
        if (heldRecords.Count == 0) return;

        foreach (var record in heldRecords.ToArray())
        {
            heldRecords.Remove(record);
            if (!Registry.TryGet(record.ShipId, out var prize) || record.CaptureSequence <= prize.CaptureSequence) continue;
            if (CaptureFromRecord(prize, record) == ConfirmOutcome.Hold) heldRecords.Add(record);
        }
    }

    // In arrival order; a confirm waits until both hulls registered and earlier captures applied, or a hull is gone.
    private void RetryHeldConfirms()
    {
        if (heldConfirms.Count == 0) return;

        foreach (var confirm in heldConfirms.ToArray())
        {
            heldConfirms.Remove(confirm);
            if (TryApplyConfirm(confirm) == ConfirmOutcome.Hold) heldConfirms.Add(confirm);
        }
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

    // An orphan's side reads as none; only a real side counts for the retreat rule.
    private static bool IsBattleSide(BattleSideEnum side) => side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender;

    // Applied, from records, dropped, repeated and conflicting captures, and the confirms still waiting.
    public object Inspect() => new
    {
        capturesApplied,
        recordsApplied,
        confirmsDropped,
        duplicateConfirms,
        captureConflicts,
        swapsBlocked,
        lastDrop,
        heldConfirms = heldConfirms.Select(confirm => confirm.ShipId).ToArray(),
        heldRecords = heldRecords.Select(record => record.ShipId).ToArray(),
    };

    private enum ConfirmOutcome
    {
        Done,
        Hold,
    }
}

/// <summary>One capture as a client applied it at a place in a hull's order: the prize, the captor's hull and the captor.</summary>
public readonly struct AppliedCapture : IEquatable<AppliedCapture>
{
    public AppliedCapture(Guid prizeId, Guid captorShipId, string captorControllerId)
    {
        PrizeId = prizeId;
        CaptorShipId = captorShipId;
        CaptorControllerId = captorControllerId;
    }

    public Guid PrizeId { get; }
    public Guid CaptorShipId { get; }
    public string CaptorControllerId { get; }

    // The same capture: same prize, same captor's hull, same captor.
    public bool Equals(AppliedCapture other) =>
        PrizeId == other.PrizeId && CaptorShipId == other.CaptorShipId && CaptorControllerId == other.CaptorControllerId;

    // Value equality, as Equals(AppliedCapture).
    public override bool Equals(object obj) => obj is AppliedCapture other && Equals(other);

    // Combines the three fields.
    public override int GetHashCode() => (PrizeId, CaptorShipId, CaptorControllerId).GetHashCode();
}

/// <summary>Where a capture confirm stands in its hulls' capture order on this client.</summary>
public enum CaptureOrder
{
    /// <summary>Both hulls' counts match: it applies now.</summary>
    Next,

    /// <summary>An earlier capture of either hull hasn't applied here yet: it waits.</summary>
    Ahead,

    /// <summary>A capture at its place in the order already applied here.</summary>
    Behind,
}
