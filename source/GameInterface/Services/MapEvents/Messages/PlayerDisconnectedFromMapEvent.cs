using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>[Server, game thread] A registered player's party remains in this MapEvent after its peer dropped.</summary>
public readonly struct PlayerDisconnectedFromMapEvent : IEvent
{
    public readonly string ControllerId;
    public readonly MapEvent MapEvent;

    public PlayerDisconnectedFromMapEvent(string controllerId, MapEvent mapEvent)
    {
        ControllerId = controllerId;
        MapEvent = mapEvent;
    }
}
