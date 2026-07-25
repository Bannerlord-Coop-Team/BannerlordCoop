using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server, local] A connected player re-entered the campaign while their party remained in a MapEvent.
/// </summary>
public readonly struct PlayerReconnectedToMapEvent : IEvent
{
}
