using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server, local] A connected player re-entered the campaign while their party remained in a MapEvent.
/// </summary>
public readonly struct PlayerReconnectedToMapEvent : IEvent
{
    public readonly string ControllerId;
    public readonly MapEvent MapEvent;

    public PlayerReconnectedToMapEvent(string controllerId, MapEvent mapEvent)
    {
        ControllerId = controllerId;
        MapEvent = mapEvent;
    }
}
