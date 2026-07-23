using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Client, local] This client just became the host of a battle through migration — the previous host
/// departed and the server promoted us. The battle controller adopts the previous host's orphaned agents
/// (the AI/enemy it was running, plus its own troops) so the battle continues uninterrupted under us.
/// </summary>
public readonly struct BattleHostMigrated : IEvent
{
    public readonly string MapEventId;
    public readonly string PreviousHostControllerId;

    public BattleHostMigrated(string mapEventId, string previousHostControllerId)
    {
        MapEventId = mapEventId;
        PreviousHostControllerId = previousHostControllerId;
    }
}
