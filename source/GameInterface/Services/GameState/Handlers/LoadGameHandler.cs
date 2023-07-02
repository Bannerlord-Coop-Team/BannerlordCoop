﻿using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameState.Handlers;

internal class LoadGameHandler : IHandler
{
    private readonly IGameStateInterface gameStateInterface;
    private readonly IMessageBroker messageBroker;

    public LoadGameHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
    {
        this.gameStateInterface = gameStateInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<LoadGameSave>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadGameSave>(Handle);
    }

    private void Handle(MessagePayload<LoadGameSave> obj)
    {
        gameStateInterface.LoadSaveGame(obj.What.SaveData);

        messageBroker.Publish(this, new GameSaveLoaded());
    }
}
