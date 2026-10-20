using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Immutable host assignment for a single battle map event: the authoritative host, the ordered
/// successor list (next-in-line first) used for host migration, and the host epoch (BR-102) — the
/// server-issued generation number that increments on every HOST CHANGE (initial election = 1, each
/// migration promotion = +1; successor-line updates keep it). Receivers use it to order assignment
/// broadcasts and to reject host-authority messages stamped by a former hosting generation.
/// </summary>
public class BattleHostAssignment
{
    public string HostControllerId { get; }
    public IReadOnlyList<string> SuccessorControllerIds { get; }
    public int Epoch { get; }
    /// <summary>[Server] A battle at sea, recorded at the election and kept by every later assignment; false on a client.</summary>
    public bool IsNaval { get; }
    /// <summary>[Server] The naval host left after its result and stays host until the conclusion; false on a client.</summary>
    public bool KeptAfterResult { get; }

    public BattleHostAssignment(string hostControllerId, IReadOnlyList<string> successorControllerIds, int epoch = 0,
        bool isNaval = false, bool keptAfterResult = false)
    {
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
        Epoch = epoch;
        IsNaval = isNaval;
        KeptAfterResult = keptAfterResult;
    }
}

/// <summary>
/// Session-scoped store of battle-host assignments, keyed by map-event id. Populated on the server when it
/// elects a host and on clients when they receive the assignment; queried by the spawn path (is this
/// client the host?) and, later, by host migration. Lives on both client and server.
/// </summary>
public interface IBattleHostRegistry
{
    void Set(string mapEventId, BattleHostAssignment assignment);
    bool TryGet(string mapEventId, out BattleHostAssignment assignment);

    /// <summary>True if this instance is the elected host for the given battle.</summary>
    bool IsHost(string mapEventId);

    void Remove(string mapEventId);
}
