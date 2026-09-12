using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] Pauses the campaign once every player is "occupied" in a map event or settlement, then restores the
/// speed this handler paused when a player becomes free on the campaign map.
/// </summary>
internal class PlayerOccupancyPauseHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerOccupancyPauseHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ITimeControlInterface timeControlInterface;
    private IAutomaticPauseLease occupancyPauseLease;

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
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyOccupancyChanged>(Handle_PartyOccupancyChanged);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
    }

    private void Handle_PartyOccupancyChanged(MessagePayload<PartyOccupancyChanged> payload)
    {
        if (ModInformation.IsClient)
            return;

        ReevaluateOccupancy(payload.What.MobileParty?.StringId ?? "<null>");
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> payload)
    {
        if (ModInformation.IsClient)
            return;

        GameThread.RunSafe(
            () => ReevaluateOccupancy("player connection state changed"),
            context: nameof(PlayerOccupancyPauseHandler));
    }

    private void ReevaluateOccupancy(string trigger)
    {
        bool allPlayersOccupied = AllPlayersOccupied();
        if (allPlayersOccupied && !ModConfigProvider.ModOptions.AutoPauseEnabled)
            return;

        if (!UpdateOccupancyTimeControl(allPlayersOccupied))
            return;

        Logger.Information(
            "Occupancy pause ownership {State}: trigger={Trigger} mode={Mode} players={@Players}",
            allPlayersOccupied ? "acquired" : "released",
            trigger,
            timeControlInterface.GetTimeControl(),
            DescribeConnectedPlayers());
    }

    internal bool UpdateOccupancyTimeControl(bool allPlayersOccupied)
    {
        if (allPlayersOccupied)
        {
            if (occupancyPauseLease != null)
                return false;

            occupancyPauseLease = timeControlInterface.ServerAcquireAutomaticPause();
            return true;
        }

        if (occupancyPauseLease == null)
            return false;

        if (!occupancyPauseLease.TryRelease())
            return false;

        occupancyPauseLease = null;
        return true;
    }

    private string[] DescribeConnectedPlayers()
    {
        return playerManager.Players
            .Where(playerManager.IsConnected)
            .Select(player =>
            {
                if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                    return $"{player.ControllerId}:party={player.MobilePartyId},state=unresolved";

                return $"{player.ControllerId}:party={player.MobilePartyId}," +
                    $"settlement={party.CurrentSettlement?.StringId ?? "none"}," +
                    $"mapEvent={party.MapEvent?.StringId ?? "none"}";
            })
            .ToArray();
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
