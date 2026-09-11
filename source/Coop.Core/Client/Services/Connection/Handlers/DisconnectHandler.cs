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
        gameStateInterface.GoToMainMenu();
        coopFinalizer.Finalize(GetDisconnectMessage(obj.What.DisconnectInfo.Reason, obj.What.ServerReason));
    }

    private static string GetDisconnectMessage(DisconnectReason reason, string serverReason)
    {
        switch (serverReason)
        {
            case "JoinReplayAppliedTimeout":
                return "Joining the campaign timed out while synchronizing.\n" +
                       "The server stopped this join to keep the campaign responsive. Please try again.";
            case "JoinReplayQueueLimit":
                return "Joining the campaign stopped because its synchronization queue exceeded the safety limit.\n" +
                       "Please try again.";
            case "JoinCampaignEntryTimeout":
                return "Joining the campaign timed out while loading the transferred save.\n" +
                       "The server stopped this join to keep the campaign responsive. Please try again.";
        }

        return reason == DisconnectReason.Timeout
            ? "Connection to the co-op server timed out.\nCheck your internet connection and try joining again."
            : "You have been Disconnected";
    }
}
