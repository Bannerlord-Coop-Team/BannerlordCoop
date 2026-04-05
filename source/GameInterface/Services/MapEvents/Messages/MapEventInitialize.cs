using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;

internal record MapEventInitialize : IEvent
{
    public MapEvent MapEvent { get; }
    public MapEvent.BattleTypes BattleType { get; }

    public MapEventInitialize(MapEvent mapEvent, MapEvent.BattleTypes battleType)
    {
        MapEvent = mapEvent;
        BattleType = battleType;
    }
}
