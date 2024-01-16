using Common.Messaging;
using GameInterface.Services.Players.Data;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Registers a Player in PlayerRegistry
/// </summary>
public class RegisterPlayer : ICommand
{
    public RegisterPlayer(Player player)
    {
        Player = player;
    }

    public Player Player { get; }
}