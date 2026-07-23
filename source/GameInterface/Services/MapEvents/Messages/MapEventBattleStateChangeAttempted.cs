using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct MapEventBattleStateChangeAttempted : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly BattleState BattleState;

    public MapEventBattleStateChangeAttempted(MapEvent mapEvent, BattleState battleState)
    {
        MapEvent = mapEvent;
        BattleState = battleState;
    }
}
