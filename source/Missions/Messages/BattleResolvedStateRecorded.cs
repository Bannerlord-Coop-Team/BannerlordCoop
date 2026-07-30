using Common.Messaging;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>Server-local signal that the current battle host reported a resolved state.</summary>
public readonly struct BattleResolvedStateRecorded : IEvent
{
    public string MapEventId { get; }
    public BattleState BattleState { get; }

    public BattleResolvedStateRecorded(string mapEventId, BattleState battleState)
    {
        MapEventId = mapEventId;
        BattleState = battleState;
    }
}
