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

    /// <summary>[Game thread] Remembers the battle host of the current epoch and applies confirms whose hulls are ready now.</summary>
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
    // [Game thread] The battle host of every epoch this client saw, which decides the captures of AI hulls in that epoch.
    private readonly Dictionary<int, string> hostsByEpoch = new Dictionary<int, string>();
    private long capturesApplied, recordsApplied, confirmsDropped, duplicateConfirms, captureConflicts;
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
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public void Tick(float dt)
    {
        if (disposed) return;

        RememberHost();
        RetryHeldConfirms();
    }

    public void ApplyDecided(NetworkShipInfo prize, NetworkShipInfo captorShip, string captorControllerId)
    {
        var confirm = new NetworkShipCaptured(prize.ShipId, captorControllerId, captorShip.ShipId, session.OwnControllerId,
            session.HostEpoch, prize.CaptureSequence, captorShip.CaptureSequence);
        Apply(prize, captorShip, captorControllerId, prize.CaptureSequence + 1);
        network.SendAll(confirm);
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
    // migration events; the capture order and the sender's authority at the confirm's epoch decide instead.
    private ConfirmOutcome TryApplyConfirm(NetworkShipCaptured confirm)
    {
        RememberHost();
        if (!Registry.TryGet(confirm.ShipId, out var prize))
            return Registry.IsGone(confirm.ShipId) ? Drop(confirm, "prize_gone") : ConfirmOutcome.Hold;

        if (!Registry.TryGet(confirm.CaptorShipId, out var captorShip))
            return Registry.IsGone(confirm.CaptorShipId) ? LeavesWithDepartedCaptor(prize, confirm) : ConfirmOutcome.Hold;

        if (hullAuthority.IsReplacing(prize.ShipId) || hullAuthority.IsReplacing(captorShip.ShipId)) return ConfirmOutcome.Hold;

        var order = OrderOf(prize.CaptureSequence, captorShip.CaptureSequence, confirm.PrizeSequence, confirm.CaptorSequence);
        if (order == CaptureOrder.Ahead) return ConfirmOutcome.Hold;
        if (order == CaptureOrder.Behind) return Stale(prize, confirm);

        var rejection = ValidateSender(prize, confirm.PreviousAuthority, HostAt(confirm.HostEpoch));
        if (rejection != null) return Drop(confirm, rejection);

        Apply(prize, captorShip, confirm.CaptorControllerId, confirm.PrizeSequence + 1);
        return ConfirmOutcome.Done;
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
    /// Why a confirm's sender may not decide the prize's capture, or null: an AI hull's captures are the battle host's of the
    /// confirm's epoch (when this client saw it; else its current or fielding authority), any other hull's its current authority.
    /// </summary>
    internal static string ValidateSender(NetworkShipInfo prize, string sender, string hostAtEpoch)
    {
        if (!prize.FollowsBattleHost) return sender == prize.CurrentAuthority ? null : "not_authority";
        if (hostAtEpoch != null) return sender == hostAtEpoch ? null : "not_host_at_epoch";
        return sender == prize.CurrentAuthority || sender == prize.OriginalOwner ? null : "not_authority";
    }

    // A repeat of the capture applied here at that place in the order changes nothing; a different capture there is a conflict
    // the single writer per place should have made impossible, so it is logged.
    private ConfirmOutcome Stale(NetworkShipInfo prize, NetworkShipCaptured confirm)
    {
        if (prize.CaptureSequence == confirm.PrizeSequence + 1 && prize.CaptorControllerId == confirm.CaptorControllerId)
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

    // The captor's hull left this client's battle before its capture arrived: elsewhere the prize left with it, so it goes too.
    private ConfirmOutcome LeavesWithDepartedCaptor(NetworkShipInfo prize, NetworkShipCaptured confirm)
    {
        if (missionContext.ControllersInMission.Contains(confirm.CaptorControllerId)) return Drop(confirm, "captor_hull_gone");
        if (prize.CaptureSequence != confirm.PrizeSequence) return Stale(prize, confirm);

        var rejection = ValidateSender(prize, confirm.PreviousAuthority, HostAt(confirm.HostEpoch));
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

        ships.TransferHull(prize, captor, inPlace: true);
        captureEngine.ApplyCapture(prize.Hull, captorShip.Hull);
        Registry.TryMarkCaptured(prize.ShipId, new HullCapture(captor, captorShip.ShipId, prizeSide), captorFormation, prizeSequence);
        Registry.TryMarkOrphaned(captorShip.ShipId, captorSide, captorShip.CaptureSequence + 1);
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

        ReportCaptureToServer(prize);
        // A peer that took the prize out with its departed old authority before the confirm arrived spawns it from these.
        ships.AnnounceHulls(new[] { prize, captorShip });
    }

    // On the relay channel this client's fates use, so the server knows the captor before the prize's first fate from it.
    private void ReportCaptureToServer(NetworkShipInfo prize)
    {
        var campaignShipId = engine.CampaignShipIdOf(prize.Hull);
        if (string.IsNullOrEmpty(campaignShipId) || !session.HasInstance) return;

        relayNetwork.SendAll(new NetworkBattleShipCaptured(session.InstanceId, campaignShipId));
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
            else if (!string.IsNullOrEmpty(record.CaptorControllerId)) CaptureFromRecord(ship, record);
        }
    }

    // The swap already orphaned a hull when its prize's record came first; otherwise the record does it.
    private void OrphanFromRecord(NetworkShipInfo ship, BattleShipSpawnData record)
    {
        if (!ship.IsOrphaned) captureEngine.OrphanHull(ship.Hull);
        Registry.TryMarkOrphaned(ship.ShipId, record.Side, record.CaptureSequence);
        recordsApplied++;
    }

    // Replays the missed capture against the captor's hull the record names; without that hull here the record can't place it.
    private void CaptureFromRecord(NetworkShipInfo prize, BattleShipSpawnData record)
    {
        if (!Registry.TryGet(record.CapturedWithShipId, out var captorShip) || captorShip.IsOrphaned
            || hullAuthority.IsReplacing(prize.ShipId) || hullAuthority.IsReplacing(captorShip.ShipId))
        {
            Logger.Warning("[NavalSync] Record of hull {ShipId} shows a capture by {Captor} this client missed, but the captor's hull " +
                "{CaptorShipId} can't take it here", prize.ShipId, record.CaptorControllerId, record.CapturedWithShipId);
            return;
        }

        Logger.Information("[NavalSync] Record of hull {ShipId} shows a capture by {Captor} this client missed; applying it",
            prize.ShipId, record.CaptorControllerId);
        Apply(prize, captorShip, record.CaptorControllerId, record.CaptureSequence);
        recordsApplied++;
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

    // The host this client sees for the current epoch; once seen, an epoch's host never changes.
    private void RememberHost()
    {
        var host = session.HostControllerId;
        if (host != null && !hostsByEpoch.ContainsKey(session.HostEpoch)) hostsByEpoch[session.HostEpoch] = host;
    }

    // The host of an epoch this client saw, or null for one it didn't (it joined later).
    private string HostAt(int epoch) => hostsByEpoch.TryGetValue(epoch, out var host) ? host : null;

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
        lastDrop,
        heldConfirms = heldConfirms.Select(confirm => confirm.ShipId).ToArray(),
    };

    private enum ConfirmOutcome
    {
        Done,
        Hold,
    }
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
