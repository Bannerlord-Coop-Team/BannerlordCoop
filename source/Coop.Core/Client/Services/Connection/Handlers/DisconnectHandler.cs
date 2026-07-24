using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using LiteNetLib;

namespace Coop.Core.Client.Services.Connection.Handlers;

internal class DisconnectHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopFinalizer coopFinalizer;
    private readonly IGameStateInterface gameStateInterface;

    public DisconnectHandler(IMessageBroker messageBroker, ICoopFinalizer coopFinalizer, IGameStateInterface gameStateInterface)
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        this.gameStateInterface = gameStateInterface;
        messageBroker.Subscribe<NetworkDisconnected>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
    }

    private void Handle(MessagePayload<NetworkDisconnected> obj)
    {
        coopFinalizer.Finalize(GetDisconnectMessage(obj.What.DisconnectInfo.Reason));
        gameStateInterface.GoToMainMenu();
    }

    private static string GetDisconnectMessage(DisconnectReason reason)
    {
        return reason == DisconnectReason.Timeout
            ? "Connection to the co-op server timed out.\nCheck your internet connection and try joining again."
            : "You have been Disconnected";
    }
}
