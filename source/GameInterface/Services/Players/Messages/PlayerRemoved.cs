using Common.Messaging;

namespace GameInterface.Services.Players.Messages;

/// <summary>Published on the server once a player's registration is deleted, not when an heir takes it over.</summary>
public readonly struct PlayerRemoved : IEvent
{
    public readonly string ControllerId;

    public PlayerRemoved(string controllerId)
    {
        ControllerId = controllerId;
    }
}
