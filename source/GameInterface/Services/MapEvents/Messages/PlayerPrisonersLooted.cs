using Common.Messaging;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct PlayerPrisonersLooted : IEvent
{
    public readonly Player Player;
    public readonly TroopRosterData Prisoners;

    public PlayerPrisonersLooted(Player player, TroopRosterData prisoners)
    {
        Player = player;
        Prisoners = prisoners;
    }
}
