using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Immutable host assignment for a single battle map event: the authoritative host and the ordered
/// successor list (next-in-line first) used for host migration.
/// </summary>
public class BattleHostAssignment
{
    public string HostControllerId { get; }
    public IReadOnlyList<string> SuccessorControllerIds { get; }

    public BattleHostAssignment(string hostControllerId, IReadOnlyList<string> successorControllerIds)
    {
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
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

    /// <summary>True while the controller is still present in an elected battle mission.</summary>
    bool IsControllerAssigned(string controllerId);

    void Remove(string mapEventId);
}
