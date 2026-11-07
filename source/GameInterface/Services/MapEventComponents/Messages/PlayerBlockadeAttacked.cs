using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents.Messages;

internal readonly struct PlayerBlockadeAttacked : IEvent
{
    public readonly MapEvent MapEvent;

    public PlayerBlockadeAttacked(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
