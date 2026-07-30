using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Players;
using Serilog;
using System.Linq;

namespace Coop.Core.Server.Services.Players.Handlers;

/// <summary>
/// Pauses game when all players disconnected
/// </summary>
internal sealed class DisconnectedPlayersServerHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisconnectedPlayersServerHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ITimeControlInterface timeControlInterface;

    public DisconnectedPlayersServerHandler(IMessageBroker messageBroker, INetwork network, IPlayerManager playerManager, ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.timeControlInterface = timeControlInterface;

        messageBroker.Subscribe<PlayerDisconnected>(HandlePlayerDisconnected);
        timeControlInterface.AddUnpausePolicy(PlayersConnectedPolicy);
    }

    private void HandlePlayerDisconnected(MessagePayload<PlayerDisconnected> _)
    {
        PauseWhenNoPlayersConnected();
    }

    private void PauseWhenNoPlayersConnected()
    {
        var connectedPlayers = playerManager.Players.Where(playerManager.IsConnected).Count();
        if (connectedPlayers == 0)
        {
            Logger.Information("Pausing campaign because no players are connected");
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        }
    }

    private bool PlayersConnectedPolicy()
    {
        return playerManager.Players.Any(playerManager.IsConnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDisconnected>(HandlePlayerDisconnected);
        timeControlInterface.RemoveUnpausePolicy(PlayersConnectedPolicy);
    }
}
