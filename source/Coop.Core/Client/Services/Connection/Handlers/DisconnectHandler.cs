using Common.Logging;
using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using LiteNetLib;
using Serilog;
using System;

namespace Coop.Core.Client.Services.Connection.Handlers;

internal class DisconnectHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisconnectHandler>();

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
        string message = GetDisconnectMessage(obj.What.DisconnectInfo.Reason, obj.What.ServerReason);

        // The state's MainMenuEntered handler can finalize first (in character creation, on the next tick),
        // so every finalize in this session shows this message.
        coopFinalizer.SetCloseText(message);

        try
        {
            gameStateInterface.GoToMainMenu();
            coopFinalizer.Finalize(message);
        }
        catch (OperationCanceledException)
        {
            // A teardown ended the session first. On the map that is CoopMod.OnGameEnd, which shows no popup.
            coopFinalizer.ShowCloseText();
            Logger.Information("The co-op session ended while returning to the main menu after a disconnect ({Reason})",
                obj.What.ServerReason ?? obj.What.DisconnectInfo.Reason.ToString());
        }
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
            case "ServerRestarting":
                return "The server is restarting. Try again in a few minutes.";
        }

        return reason == DisconnectReason.Timeout
            ? "Connection to the co-op server timed out.\nCheck your internet connection and try joining again."
            : "You have been Disconnected";
    }
}
