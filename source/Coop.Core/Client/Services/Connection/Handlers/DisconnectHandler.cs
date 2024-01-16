using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.Services.Connection.Handlers;

internal class DisconnectHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopFinalizer coopFinalizer;

    public DisconnectHandler(IMessageBroker messageBroker, ICoopFinalizer coopFinalizer) 
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<NetworkDisconnected>(Handle);
        messageBroker.Subscribe<EnterMainMenuResponse>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
        messageBroker.Unsubscribe<EnterMainMenuResponse>(Handle);
    }

    private void Handle(MessagePayload<NetworkDisconnected> obj)
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    private void Handle(MessagePayload<EnterMainMenuResponse> obj)
    {
        coopFinalizer.Finalize("You have been Disconnected");
    }
}
