using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameDebug.Messages;

namespace Coop.Core.Server.Services.Connection.Handlers;

/// <summary>
/// Sends player-facing messages when players start or finish joining the campaign.
/// </summary>
public class PlayerConnectionServerMessageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    private const string AllPlayersConnectedMessage = "All players connected";

    public PlayerConnectionServerMessageHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
    }

    internal void Handle_LoadingPlayersChanged(MessagePayload<LoadingPlayersChanged> obj)
    {
        var loadingPlayerCount = obj.What.LoadingPlayerCount;
        BroadcastNotification(loadingPlayerCount > 0 ? LoadingMessage(loadingPlayerCount) : AllPlayersConnectedMessage);
    }

    private void BroadcastNotification(string text)
    {
        var message = new SendInformationMessage(text);
        messageBroker.Publish(this, message);
        network.SendAll(message);
    }

    private static string LoadingMessage(int loadingPlayers) =>
        loadingPlayers + " player(s) are currently joining the game";
}
