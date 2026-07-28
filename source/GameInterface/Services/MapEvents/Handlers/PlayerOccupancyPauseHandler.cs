using Common;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] Pauses the campaign once every player is occupied by a map event, settlement, or conversation, so time
/// stops when nobody is free on the map. There is no unpause here (that is left to the players / other policies); it
/// only sends the pause when the occupancy condition becomes true.
/// </summary>
internal class PlayerOccupancyPauseHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly ConversationPartyTracker conversationPartyTracker;

    public PlayerOccupancyPauseHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ITimeControlInterface timeControlInterface,
        ConversationPartyTracker conversationPartyTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.timeControlInterface = timeControlInterface;
        this.conversationPartyTracker = conversationPartyTracker;

        messageBroker.Subscribe<PartyOccupancyChanged>(Handle_PartyOccupancyChanged);
        messageBroker.Subscribe<PlayerConversationChanged>(Handle_PlayerConversationChanged);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyOccupancyChanged>(Handle_PartyOccupancyChanged);
        messageBroker.Unsubscribe<PlayerConversationChanged>(Handle_PlayerConversationChanged);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    private void Handle_PartyOccupancyChanged(MessagePayload<PartyOccupancyChanged> payload)
    {
        PauseIfAllPlayersOccupied();
    }

    private void Handle_PlayerConversationChanged(MessagePayload<PlayerConversationChanged> payload)
    {
        GameThread.RunSafe(
            PauseIfAllPlayersOccupied,
            context: nameof(PlayerOccupancyPauseHandler));
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        GameThread.RunSafe(
            PauseIfAllPlayersOccupied,
            context: nameof(PlayerOccupancyPauseHandler));
    }

    private void PauseIfAllPlayersOccupied()
    {
        if (ModInformation.IsClient)
            return;

        if (!AllPlayersOccupied())
            return;

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
    }

    // An empty session is not "all occupied", so it never pauses with no players.
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

    private bool IsPlayerOccupied(MobileParty playerParty)
    {
        var mapEvent = playerParty.MapEvent;
        if (mapEvent != null && mapEvent.IsActiveSlowVillageRaid())
            return false;

        if (objectManager.TryGetId(playerParty.Party, out var partyId) &&
            conversationPartyTracker.IsPlayerPartyEngaged(partyId))
            return true;

        if (playerParty.CurrentSettlement != null)
            return true;

        return mapEvent != null;
    }
}
