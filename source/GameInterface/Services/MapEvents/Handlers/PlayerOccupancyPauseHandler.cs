using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Enum;
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
    private long? occupancyPauseToken;

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

        bool allPlayersOccupied = AllPlayersOccupied();
        if (allPlayersOccupied && !ModConfigProvider.ModOptions.AutoPauseEnabled)
            return;

        var appliedMode = UpdateOccupancyTimeControl(allPlayersOccupied);
        if (!appliedMode.HasValue)
            return;

        if (appliedMode.Value == TimeControlEnum.Pause)
        {
            Logger.Information(
                "Pausing campaign because every connected player is occupied: triggerParty={TriggerParty} players={@Players}",
                payload.What.MobileParty?.StringId ?? "<null>",
                DescribeConnectedPlayers());
        }
        else
        {
            Logger.Information(
                "Restoring campaign time after a player became free: triggerParty={TriggerParty} mode={Mode}",
                payload.What.MobileParty?.StringId ?? "<null>",
                appliedMode.Value);
        }
    }

    internal TimeControlEnum? UpdateOccupancyTimeControl(bool allPlayersOccupied)
    {
        if (allPlayersOccupied)
        {
            if (occupancyPauseToken.HasValue)
                return null;

            if (!timeControlInterface.ServerTryCreatePause(
                    out _,
                    out var pauseToken))
                return null;

            occupancyPauseToken = pauseToken;
            return TimeControlEnum.Pause;
        }

        if (!occupancyPauseToken.HasValue)
            return null;

        var result = timeControlInterface.ServerTryRestoreTimeControl(
                occupancyPauseToken.Value,
                out var restoredMode);
        if (result == AutomaticPauseRestoreResult.Blocked)
            return null;

        occupancyPauseToken = null;
        return result == AutomaticPauseRestoreResult.Restored
            ? restoredMode
            : (TimeControlEnum?)null;
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
