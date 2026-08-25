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

    public BattleHostMigrated(
        string mapEventId,
        string previousHostControllerId,
        string newHostControllerId)
    {
        MapEventId = mapEventId;
        PreviousHostControllerId = previousHostControllerId;
        NewHostControllerId = newHostControllerId;
    }

    public BattleHostMigrated(string mapEventId, string previousHostControllerId)
        : this(mapEventId, previousHostControllerId, null)
    {
    }
}
