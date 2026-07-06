using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] Pauses the campaign once every player is "occupied" — in a map event OR a settlement — so time stops
/// when nobody is free on the map. Driven by <see cref="PartyOccupancyChanged"/>, which the setter postfixes
/// (<see cref="MobileParties.Patches.PartyOccupancyPatches"/>) raise whenever any party's map-event or settlement
/// membership changes. There is no unpause here (that is left to the players / other policies); it only sends the
/// pause when the occupancy condition becomes true.
/// </summary>
internal class PlayerOccupancyPauseHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ITimeControlInterface timeControlInterface;

    public PlayerOccupancyPauseHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.timeControlInterface = timeControlInterface;

        messageBroker.Subscribe<PartyOccupancyChanged>(Handle_PartyOccupancyChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyOccupancyChanged>(Handle_PartyOccupancyChanged);
    }

    private void Handle_PartyOccupancyChanged(MessagePayload<PartyOccupancyChanged> payload)
    {
        if (ModInformation.IsClient)
            return;

        if (!AllPlayersOccupied())
            return;

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
    }

    // Every player's party is in a map event or a settlement (i.e. none is free on the campaign map). An empty
    // session is not "all occupied", so it never pauses with no players.
    private bool AllPlayersOccupied()
    {
        var connectedPlayers = playerManager.Players.Where(playerManager.IsConnected).ToList();
        return connectedPlayers.Any() && connectedPlayers.All(player =>
        {
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
                return false;

            return IsPlayerOccupied(playerParty);
        });
    }

    private static bool IsPlayerOccupied(MobileParty playerParty)
    {
        var mapEvent = playerParty.MapEvent;
        if (mapEvent != null && mapEvent.IsActiveSlowVillageRaid())
            return false;

        if (playerParty.CurrentSettlement != null)
            return true;

        return mapEvent != null;
    }
}
