using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;
internal record MapEventSidesArrayUpdated : GenericArrayEvent<MapEvent, MapEventSide>
{
    public MapEventSidesArrayUpdated(MapEvent instance, MapEventSide value, int index) : base(instance, value, index)
    {
    }
}
