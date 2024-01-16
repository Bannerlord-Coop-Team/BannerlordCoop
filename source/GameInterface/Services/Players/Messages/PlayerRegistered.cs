using Common.Messaging;
using GameInterface.Services.Players.Data;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// When the client recieves a NetworkPlayerRegister this event goes to the
/// GameInterface to notify the PlayerRegistry.
/// </summary>
public record PlayerRegistered : IEvent
{
    public PlayerRegistered(Player player)
    {
        Player = player;
    }

    public Player Player {  get; }
}
