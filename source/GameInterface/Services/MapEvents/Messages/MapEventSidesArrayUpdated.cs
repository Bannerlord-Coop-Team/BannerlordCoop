using Common.Messaging;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;
internal record MapEventSidesArrayUpdated : GenericArrayEvent<MapEvent, MapEventSide>
{
    public MapEventSidesArrayUpdated(MapEvent instance, MapEventSide value, int index) : base(instance, value, index)
    {
        Instance = instance;
        Value = value;
        Index = index;
    }

    public MapEvent Instance { get; }
    public MapEventSide Value { get; }
    public int Index { get; }
}
