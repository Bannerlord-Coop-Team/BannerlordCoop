using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Client] A battle moved to a new host. Every peer updates registry authority; the promoted
/// host also adopts the previous host's orphaned agents so the battle continues uninterrupted.
/// </summary>
public readonly struct BattleHostMigrated : IEvent
{
    public readonly string MapEventId;
    public readonly string PreviousHostControllerId;
    public readonly string NewHostControllerId;
    /// <summary>The host epoch the change took the battle to; 0 when the publisher didn't say.</summary>
    public readonly int Epoch;

    public BattleHostMigrated(
        string mapEventId,
        string previousHostControllerId,
        string newHostControllerId,
        int epoch = 0)
    {
        MapEventId = mapEventId;
        PreviousHostControllerId = previousHostControllerId;
        NewHostControllerId = newHostControllerId;
        Epoch = epoch;
    }

    public BattleHostMigrated(string mapEventId, string previousHostControllerId)
        : this(mapEventId, previousHostControllerId, null)
    {
    }
}
