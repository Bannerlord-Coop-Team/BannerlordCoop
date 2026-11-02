using System;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>The AI hulls a take-back left, with the client to take them back and the host change they moved at.</summary>
public sealed class NpcHullHandover
{
    /// <summary>Nothing left: every hull was taken back here.</summary>
    public static readonly NpcHullHandover None = new NpcHullHandover(Array.Empty<Guid>(), null, 0);

    // Hulls left for the holder, which may be this client when it waits for a hull swap or a host change to apply.
    public NpcHullHandover(IReadOnlyList<Guid> shipIds, string holder, int hostEpoch)
    {
        ShipIds = shipIds;
        Holder = holder;
        HostEpoch = hostEpoch;
    }

    public IReadOnlyList<Guid> ShipIds { get; }
    public string Holder { get; }
    public int HostEpoch { get; }
}
