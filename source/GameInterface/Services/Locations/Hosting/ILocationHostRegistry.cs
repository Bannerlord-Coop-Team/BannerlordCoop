using System.Collections.Generic;

namespace GameInterface.Services.Locations.Hosting;

/// <summary>
/// Immutable host assignment for a single settlement location mission instance: the authoritative NPC
/// host, the ordered successor list (next-in-line first) used for host migration, and the host epoch
/// (SR-016) — the server-issued generation number that increments on every HOST CHANGE (initial
/// election = 1, each migration promotion = +1; successor-line updates keep it). Receivers use it to
/// order assignment broadcasts and to reject host-authority messages stamped by a former hosting
/// generation. Mirrors <see cref="MapEvents.BattleHostAssignment"/> for battles.
/// </summary>
public class LocationHostAssignment
{
    public string HostControllerId { get; }
    public IReadOnlyList<string> SuccessorControllerIds { get; }
    public int Epoch { get; }

    public LocationHostAssignment(string hostControllerId, IReadOnlyList<string> successorControllerIds, int epoch = 0)
    {
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
        Epoch = epoch;
    }
}

/// <summary>
/// Session-scoped store of location-host assignments, keyed by location instance id
/// ("{settlementId}|{locationId}", see <c>LocationInstanceId</c>). Populated on the server when it
/// elects a host and on clients when they receive the assignment; queried by the NPC spawn path (is
/// this client the host?) and by host migration. Lives on both client and server.
/// <para>
/// Deliberately a SEPARATE store from <see cref="MapEvents.IBattleHostRegistry"/>: both handlers
/// receive every mission instance's departures and discriminate by "is this id in MY registry", so
/// sharing keys would route settlement migrations through battle map-event/reserve code.
/// </para>
/// </summary>
public interface ILocationHostRegistry
{
    void Set(string instanceId, LocationHostAssignment assignment);
    bool TryGet(string instanceId, out LocationHostAssignment assignment);

    /// <summary>True if this client is the elected NPC host for the given location instance.</summary>
    bool IsHost(string instanceId);

    void Remove(string instanceId);
}
