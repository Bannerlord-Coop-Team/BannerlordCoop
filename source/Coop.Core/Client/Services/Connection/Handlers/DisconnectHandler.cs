using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using System;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.Services.Connection.Handlers;

internal class DisconnectHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopFinalizer coopFinalizer;
    private DateTime? _ignoreDisconnectsUntilUtc;
    private readonly DateTime _initTimeUtc = DateTime.UtcNow;
    private bool _wasConnected;

    public DisconnectHandler(IMessageBroker messageBroker, ICoopFinalizer coopFinalizer) 
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<NetworkDisconnected>(Handle);
        messageBroker.Subscribe<EnterMainMenuResponse>(Handle);
        messageBroker.Subscribe<NetworkConnected>(Handle);
        messageBroker.Subscribe<SendInformationMessage>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
        messageBroker.Unsubscribe<EnterMainMenuResponse>(Handle);
        messageBroker.Unsubscribe<NetworkConnected>(Handle);
        messageBroker.Unsubscribe<SendInformationMessage>(Handle);
    }

    private void Handle(MessagePayload<NetworkDisconnected> obj)
    {
        if (_ignoreDisconnectsUntilUtc.HasValue && DateTime.UtcNow < _ignoreDisconnectsUntilUtc.Value)
        {
            global::Common.Logging.LogManager.GetLogger<DisconnectHandler>().Warning("NetworkDisconnected ignored (grace period)");
            return;
        }
        if (!_wasConnected)
        {
            global::Common.Logging.LogManager.GetLogger<DisconnectHandler>().Warning("NetworkDisconnected ignored (never connected)");
            return;
        }
        global::Common.Logging.LogManager.GetLogger<DisconnectHandler>().Information("NetworkDisconnected → EnterMainMenu");
        messageBroker.Publish(this, new EnterMainMenu());
    }

    private void Handle(MessagePayload<EnterMainMenuResponse> obj)
    {
        if (_wasConnected)
        {
            coopFinalizer.Finalize("You have been Disconnected");
        }
    }

    private void Handle(MessagePayload<NetworkConnected> obj)
    {
        _ignoreDisconnectsUntilUtc = null;
        _wasConnected = true;
    }

    private void Handle(MessagePayload<SendInformationMessage> obj)
    {
        var text = obj.What.Text ?? string.Empty;
        if (text.Contains("Connecting") || text.Contains("Tentative de connexion"))
        {
            _ignoreDisconnectsUntilUtc = DateTime.UtcNow.AddSeconds(30);
            global::Common.Logging.LogManager.GetLogger<DisconnectHandler>().Information("Grace period started for disconnects");
        }
    }
}
