using Common.Messaging;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using GameInterface.Services.GameDebug.Messages;
using System;
using System.IO;

namespace Coop.Core.Server.Services.Save.Handlers;

/// <summary>
/// Handles Coop specific saving and loading
/// </summary>
internal class SaveGameHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopSaveManager saveManager;
    private readonly ICoopServer coopServer;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        ICoopServer coopServer,
        IControllerIdProvider controllerIdProvider,
        IControlledEntityRegistry controlledEntityRegistry) 
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.coopServer = coopServer;
        this.controllerIdProvider = controllerIdProvider;
        this.controlledEntityRegistry = controlledEntityRegistry;
        messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<LoadGame>(Handle_LoadGameByName);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);

        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<LoadGame>(Handle_LoadGameByName);
    }

    private void Handle_GameSaved(MessagePayload<GameSaved> obj)
    {
        var saveName = obj.What.SaveName;
        messageBroker.Publish(this, new PackageObjectGuids());

        var controlledEntities = controlledEntityRegistry.PackageControlledEntities();

        CoopSession session = new CoopSession(saveName, controlledEntities);

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        savedSession = saveManager.LoadCoopSession(obj.What.SaveName);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        if (savedSession == null)
        {
            messageBroker.Publish(this, new RegisterAllPartiesAsControlled(controllerIdProvider.ControllerId));
        }
        else
        {
            controlledEntityRegistry.LoadControlledEntities(savedSession.ControlledEntityMap);
        }

        // Auto-enable time after world registration completes
        messageBroker.Publish(this, new SetTimeControlMode(GameInterface.Services.Heroes.Enum.TimeControlEnum.Play_1x));
    }

    private void Handle_LoadGameByName(MessagePayload<LoadGame> obj)
    {
        var provided = obj.What.SaveName;
        var nameOnly = Path.GetFileNameWithoutExtension(provided);
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        var dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "Game Saves");
        var pathCandidate = Path.Combine(dir, nameOnly + ".sav");
        var path = File.Exists(pathCandidate) ? pathCandidate : provided;

        if (!File.Exists(path))
        {
            messageBroker.Publish(this, new SendInformationMessage($"Sauvegarde introuvable: {provided}"));
            return;
        }

        var bytes = File.ReadAllBytes(path);
        messageBroker.Publish(this, new LoadGameSave(bytes));
    }
}
