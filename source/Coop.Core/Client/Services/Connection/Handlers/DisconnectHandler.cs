using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.Services.Connection.Handlers;

internal class DisconnectHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopFinalizer coopFinalizer;
    private readonly IGameStateInterface gameStateInterface;

    // MainMenuEntered fires both on a real disconnect AND as an intermediate step of normal flows —
    // e.g. ReceivingSavedDataState calls GoToMainMenu() to clear the character-creation game before
    // loading the host save. Finalizing on the latter would dispose the coop container mid-load, so
    // the save's sync patches resolve ISyncPolicy from a disposed container (ObjectDisposedException).
    // Only tear coop down when MainMenuEntered actually follows a disconnect.
    private bool pendingDisconnect;

    public DisconnectHandler(IMessageBroker messageBroker, ICoopFinalizer coopFinalizer, IGameStateInterface gameStateInterface)
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        this.gameStateInterface = gameStateInterface;
        messageBroker.Subscribe<NetworkDisconnected>(Handle);
        messageBroker.Subscribe<MainMenuEntered>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle);
    }

    private void Handle(MessagePayload<NetworkDisconnected> obj)
    {
        pendingDisconnect = true;
        gameStateInterface.GoToMainMenu();
    }

    private void Handle(MessagePayload<MainMenuEntered> obj)
    {
        if (!pendingDisconnect) return;

        pendingDisconnect = false;
        coopFinalizer.Finalize("You have been Disconnected");
    }
}
