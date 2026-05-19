using Common.Messaging;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages.Control;
using GameInterface.Services.Smithing;

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
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        ICoopServer coopServer,
        IControllerIdProvider controllerIdProvider,
        ICoopSessionProvider coopSessionProvider,
        IControlledEntityRegistry controlledEntityRegistry) 
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.coopServer = coopServer;
        this.controllerIdProvider = controllerIdProvider;
        this.coopSessionProvider = coopSessionProvider;
        this.controlledEntityRegistry = controlledEntityRegistry;
        messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);

        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    private void Handle_GameSaved(MessagePayload<GameSaved> obj)
    {
        var saveName = obj.What.SaveName;
        messageBroker.Publish(this, new PackageObjectGuids());

        var controlledEntities = controlledEntityRegistry.PackageControlledEntities();

        CraftingPlayerData craftingPlayerData = coopSessionProvider.CoopSession.CraftingPlayerData;
        craftingPlayerData ??= new(new(), new(), new());

        CoopSession session = new CoopSession(saveName, controlledEntities, craftingPlayerData);
        coopSessionProvider.CoopSession = session;

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        savedSession = saveManager.LoadCoopSession(obj.What.SaveName);
        coopSessionProvider.CoopSession = savedSession;
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
    }
}
