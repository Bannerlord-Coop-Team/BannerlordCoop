using Common.Messaging;
using GameInterface.Services.Players.Data;

namespace GameInterface.Services.Players.Messages;

public readonly struct PlayerDeleting : IEvent
{
    public readonly Player Player;

    public PlayerDeleting(Player player) => Player = player;
}
