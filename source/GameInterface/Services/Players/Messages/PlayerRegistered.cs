using Common.Messaging;
using GameInterface.Services.Players.Data;

namespace GameInterface.Services.Players.Messages;

public record PlayerRegistered : IEvent
{
    public PlayerRegistered(Player player)
    {
        Player = player;
    }

    public Player Player {  get; }
}
