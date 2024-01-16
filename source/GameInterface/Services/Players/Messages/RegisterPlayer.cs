using Common.Messaging;
using GameInterface.Services.Players.Data;

namespace GameInterface.Services.Players.Messages;

public class RegisterPlayer : ICommand
{
    public RegisterPlayer(Player player)
    {
        Player = player;
    }

    public Player Player { get; }
}