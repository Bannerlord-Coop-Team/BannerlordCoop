using Common.Messaging;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.Players;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Handlers;

/// <summary>
/// Handles Coop specific saving and loading
/// </summary>
internal class SaveGameHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopSaveManager saveManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IMobilePartyInterface mobilePartyInterface;
    private readonly IPlayerRegistry playerRegistry;

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        IControllerIdProvider controllerIdProvider,
        IControlledEntityRegistry controlledEntityRegistry,
        IMobilePartyInterface mobilePartyInterface,
        IPlayerRegistry playerRegistry) 
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.controllerIdProvider = controllerIdProvider;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.mobilePartyInterface = mobilePartyInterface;
        this.playerRegistry = playerRegistry;
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

        CoopSession session = new CoopSession(saveName, controlledEntities, playerRegistry.ToArray());

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        savedSession = saveManager.LoadCoopSession(obj.What.SaveName);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        controlledEntityRegistry.LoadControlledEntities(savedSession.ControlledEntityMap);
        mobilePartyInterface.RegisterAllPartiesAsControlled(controllerIdProvider.ControllerId);
        foreach (var player in savedSession.Players)
        {
            playerRegistry.AddPlayer(player);
        }

        PartyExtensions.InvalidateCache();
    }
}
