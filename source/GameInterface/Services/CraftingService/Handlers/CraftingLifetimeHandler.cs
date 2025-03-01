using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Handlers;

/// <summary>
/// Handles all changes to clans on client.
/// </summary>
public class CraftingLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILogger Logger = LogManager.GetLogger<CraftingLifetimeHandler>();

    public CraftingLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<CraftingCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateCrafting>(Handle);
        messageBroker.Subscribe<CraftingRemoved>(Handle);
        messageBroker.Subscribe<NetworkRemoveCrafting>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CraftingCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateCrafting>(Handle);
        messageBroker.Unsubscribe<CraftingRemoved>(Handle);
        messageBroker.Unsubscribe<NetworkRemoveCrafting>(Handle);
    }

    private void Handle(MessagePayload<CraftingCreated> payload)
    {
        if (objectManager.AddNewObject(payload.What.Crafting, out var newId) == false) return;

        NetworkCreateCrafting message = new(newId);
        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkCreateCrafting> obj)
    {
        var newObj = ObjectHelper.SkipConstructor<Crafting>();

        if (objectManager.AddExisting(obj.What.Id, newObj) == false)
        {
            Logger.Error("Failed to register type {type} with id {id}", typeof(Crafting), obj.What.Id);
            return;
        }
    }

    private void Handle(MessagePayload<CraftingRemoved> payload)
    {
        var crafting = payload.What.Crafting;

        if(objectManager.TryGetId(crafting, out string craftingId) == false)
        {
            Logger.Error("Failed to get ID for {type}", typeof(Crafting));
            return;
        }
        if(objectManager.Remove(crafting) == false)
        {
            Logger.Error("Failed to remove {type}", typeof(Crafting));
            return;
        }
        NetworkRemoveCrafting message = new(craftingId);
        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkRemoveCrafting> obj)
    {
        var payload = obj.What;

        if (objectManager.TryGetObject(payload.CraftingId, out Crafting crafting) == false)
        {
            Logger.Error("Failed to get object for {type} with id {id}", typeof(Crafting), payload.CraftingId);
            return;
        }

        if (objectManager.Remove(crafting) == false)
        {
            Logger.Error("Failed to remove {type} with id {id}", crafting, payload.CraftingId);
            return;
        }
    }
}
