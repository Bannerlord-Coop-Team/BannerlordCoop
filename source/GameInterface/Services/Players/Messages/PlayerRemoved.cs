using Common.Messaging;

namespace GameInterface.Services.Players.Messages;

/// <summary>Published after a player's registration and controlled-object markers are removed.</summary>
public readonly struct PlayerRemoved : IEvent
{
    public readonly string ControllerId;
    public readonly string CharacterId;

    public PlayerRemoved(string controllerId, string characterId)
    {
        ControllerId = controllerId;
        CharacterId = characterId;
    }
}
