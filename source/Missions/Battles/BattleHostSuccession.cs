using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>What a member's departure did to its battle's host assignment.</summary>
internal enum HostDepartureOutcome
{
    /// <summary>No host is elected yet.</summary>
    NoHost,
    /// <summary>The member was neither the host nor in the successor line.</summary>
    Unchanged,
    /// <summary>A successor left the line; the host and its epoch stay.</summary>
    SuccessorDropped,
    /// <summary>The naval host left after its result and stays host until the conclusion.</summary>
    Kept,
    /// <summary>The host left with no mission-ready successor, so the battle has no host until the next election.</summary>
    Hostless,
    /// <summary>The first successor became host in the next hosting generation.</summary>
    Promoted,
}

/// <summary>
/// [Server, game thread] Who hosts each battle: the election, the successor line, what a departure does to them (kept,
/// hostless or promoted, from the host's result) and the epoch watermark. It stores and announces every change it makes.
/// </summary>
internal interface IBattleHostSuccession
{
    /// <summary>Elects the first mission-ready requester as host, in the battle's next hosting generation (BR-010, BR-102).</summary>
    BattleHostAssignment Elect(string mapEventId, string hostControllerId, bool isNaval);

    /// <summary>Appends a mission-ready member to the successor line (BR-013); false when it is the host or already queued.</summary>
    bool TryAppendSuccessor(string mapEventId, string controllerId, out BattleHostAssignment updated);

    /// <summary>Drops a departed member from the battle's hosting: a successor leaves the line, a host is kept or replaced.</summary>
    HostDepartureOutcome MemberDeparted(string mapEventId, string controllerId);

    /// <summary>Records the result the current host reported, and sends it to every client with the assignment.</summary>
    void RecordReportedResult(string mapEventId, BattleState battleState);

    /// <summary>Records that the battle's map event applied its result.</summary>
    void MarkConcluded(string mapEventId);

    /// <summary>The result the battle's host reported, if any.</summary>
    bool TryGetReportedResult(string mapEventId, out BattleState battleState);

    /// <summary>Forgets a battle instance's result, keeping its epoch watermark.</summary>
    void ForgetResult(string mapEventId);

    /// <summary>The network form of an assignment, as every client stores it.</summary>
    NetworkBattleHostAssigned ToMessage(string mapEventId, BattleHostAssignment assignment);
}

/// <inheritdoc cref="IBattleHostSuccession"/>
internal class BattleHostSuccession : IBattleHostSuccession
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleHostSuccession>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IBattleHostRetentionPolicy retentionPolicy;

    // Highest host epoch ever issued per battle (BR-102), kept across assignment removal: clients keep their last assignment
    // when a battle is abandoned, so a re-election for the same map event must issue a higher epoch or they would ignore it.
    private readonly Dictionary<string, int> issuedEpochs = new Dictionary<string, int>();

    // The result each battle instance's host reported, and whether its map event applied one; a naval host is kept by either.
    private readonly Dictionary<string, HostResult> results = new Dictionary<string, HostResult>();

    // A battle instance's result as the host decisions read it.
    private sealed class HostResult
    {
        public BattleState Reported;
        public bool Concluded;
    }

    public BattleHostSuccession(
        IMessageBroker messageBroker,
        INetwork network,
        IBattleHostRegistry hostRegistry,
        IBattleHostRetentionPolicy retentionPolicy)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.hostRegistry = hostRegistry;
        this.retentionPolicy = retentionPolicy;
    }

    public BattleHostAssignment Elect(string mapEventId, string hostControllerId, bool isNaval)
    {
        var assignment = new BattleHostAssignment(hostControllerId, Array.Empty<string>(), NextEpoch(mapEventId), isNaval);
        Store(mapEventId, assignment);
        return assignment;
    }

    public bool TryAppendSuccessor(string mapEventId, string controllerId, out BattleHostAssignment updated)
    {
        updated = null;
        if (!hostRegistry.TryGet(mapEventId, out var existing) || existing.HostControllerId == controllerId) return false;
        foreach (var successor in existing.SuccessorControllerIds)
            if (successor == controllerId) return false;

        // The host did not change, so the assignment stays in the same hosting generation (BR-102).
        updated = existing.WithSuccessors(new List<string>(existing.SuccessorControllerIds) { controllerId });
        Store(mapEventId, updated);
        return true;
    }

    public HostDepartureOutcome MemberDeparted(string mapEventId, string controllerId)
    {
        if (!hostRegistry.TryGet(mapEventId, out var assignment)) return HostDepartureOutcome.NoHost;

        var successors = new List<string>(assignment.SuccessorControllerIds);
        if (assignment.HostControllerId != controllerId)
            return DropSuccessor(mapEventId, controllerId, assignment, successors);

        // A promotion after the result would hand the ended naval mission's AI and hulls to a peer (the R32 helm crash), or
        // the host role to a member that never reports (R41a); a land battle still promotes, so its AI keeps moving.
        if (KeepsHostAfterResult(mapEventId))
        {
            Logger.Information("[BattleHost] Host {Host} left resolved battle {MapEventId}; keeping the assignment at epoch {Epoch} without promoting",
                controllerId, mapEventId, assignment.Epoch);
            // Clients already hold this host and epoch, so only the server's copy and its own handlers learn it is kept.
            hostRegistry.Set(mapEventId, assignment.AsKeptAfterResult());
            messageBroker.Publish(this, new BattleHostAssignmentChanged(mapEventId));
            return HostDepartureOutcome.Kept;
        }

        if (successors.Count == 0)
        {
            hostRegistry.Remove(mapEventId);
            return HostDepartureOutcome.Hostless;
        }

        // Promote the earliest-joined successor still present; the host changed, so the epoch advances (BR-102).
        var newHost = successors[0];
        successors.RemoveAt(0);
        var promoted = assignment.PromotedTo(newHost, successors, NextEpoch(mapEventId, assignment.Epoch));
        Store(mapEventId, promoted);
        Logger.Information("[BattleHost] Host {Old} left battle {MapEventId}; promoted {New} at epoch {Epoch} (successors: {Successors})",
            controllerId, mapEventId, newHost, promoted.Epoch, string.Join(", ", successors));
        return HostDepartureOutcome.Promoted;
    }

    // A departed non-host leaves the successor line; the host did not change, so neither does the epoch (BR-102).
    private HostDepartureOutcome DropSuccessor(string mapEventId, string controllerId, BattleHostAssignment assignment,
        List<string> successors)
    {
        if (!successors.Remove(controllerId)) return HostDepartureOutcome.Unchanged;

        Store(mapEventId, assignment.WithSuccessors(successors));
        Logger.Information("[BattleHost] Successor {Controller} left battle {MapEventId}; successor line now: {Successors}",
            controllerId, mapEventId, string.Join(", ", successors));
        return HostDepartureOutcome.SuccessorDropped;
    }

    public void RecordReportedResult(string mapEventId, BattleState battleState)
    {
        if (!BattleResultCommitter.IsSupportedResultState(battleState)) return;

        GetResult(mapEventId).Reported = battleState;
        if (hostRegistry.TryGet(mapEventId, out var assignment))
            BroadcastResult(mapEventId, assignment, battleState);
    }

    public void MarkConcluded(string mapEventId) => GetResult(mapEventId).Concluded = true;

    public bool TryGetReportedResult(string mapEventId, out BattleState battleState)
    {
        battleState = results.TryGetValue(mapEventId, out var result) ? result.Reported : BattleState.None;
        return battleState != BattleState.None;
    }

    public void ForgetResult(string mapEventId) => results.Remove(mapEventId);

    public NetworkBattleHostAssigned ToMessage(string mapEventId, BattleHostAssignment assignment)
    {
        var successors = new string[assignment.SuccessorControllerIds.Count];
        for (int i = 0; i < successors.Length; i++)
            successors[i] = assignment.SuccessorControllerIds[i];

        return new NetworkBattleHostAssigned(mapEventId, assignment.HostControllerId, successors, assignment.Epoch);
    }

    // Whether the host stays after it leaves: it reported its result, or the map event applied one, and the policy keeps it.
    private bool KeepsHostAfterResult(string mapEventId) =>
        results.TryGetValue(mapEventId, out var result)
        && retentionPolicy.KeepsHostAfterResult(mapEventId, result.Concluded || result.Reported != BattleState.None);

    // Stores and broadcasts an assignment; a reported result goes out again behind it, at the new host's epoch.
    private void Store(string mapEventId, BattleHostAssignment assignment)
    {
        hostRegistry.Set(mapEventId, assignment);
        network.SendAll(ToMessage(mapEventId, assignment));
        if (TryGetReportedResult(mapEventId, out var reported))
            BroadcastResult(mapEventId, assignment, reported);

        messageBroker.Publish(this, new BattleHostAssignmentChanged(mapEventId));
    }

    // Sends the battle's result stamped with the assignment's host and epoch, which is the one a client accepts.
    private void BroadcastResult(string mapEventId, BattleHostAssignment assignment, BattleState battleState)
    {
        network.SendAll(new NetworkBattleResultSnapshot(mapEventId, assignment.HostControllerId, assignment.Epoch, battleState));
    }

    // The battle's result record, created on first use.
    private HostResult GetResult(string mapEventId)
    {
        if (!results.TryGetValue(mapEventId, out var result))
            results[mapEventId] = result = new HostResult();
        return result;
    }

    // Issues the battle's next host epoch (BR-102): one past the highest ever issued and past floor, the current epoch when
    // promoting (it may have been seeded out of band), starting at 1.
    private int NextEpoch(string mapEventId, int floor = 0)
    {
        issuedEpochs.TryGetValue(mapEventId, out var last);
        var next = Math.Max(last, floor) + 1;
        issuedEpochs[mapEventId] = next;
        return next;
    }
}
