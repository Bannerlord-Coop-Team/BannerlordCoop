using Common.Messaging;

namespace Missions.Messages;

/// <summary>Server-local signal that a battle host assignment changed.</summary>
public readonly struct BattleHostAssignmentChanged : IEvent
{
    public string MapEventId { get; }

    public BattleHostAssignmentChanged(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
