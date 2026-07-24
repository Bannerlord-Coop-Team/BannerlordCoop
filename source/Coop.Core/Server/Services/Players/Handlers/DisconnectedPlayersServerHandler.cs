using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Players;
using System.Linq;

namespace Coop.Core.Server.Services.Players.Handlers;

/// <summary>
/// Pauses game when all players disconnected
/// </summary>
internal sealed class DisconnectedPlayersServerHandler : IHandler
{
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
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDisconnected>(HandlePlayerDisconnected);
    }
}
