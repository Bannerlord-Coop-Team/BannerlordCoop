using System;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.BesiegerCamps.Handlers;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.BesiegerCamps.Messages.Collection;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.WeaponDesigns.Messages.Collection;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService.Handlers;

/// <summary>
/// Handler for  <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
internal class CraftingWeaponDesignHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingWeaponDesignHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public CraftingWeaponDesignHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NetworkAddWeaponDesign>(HandleCommand_AddWeaponDesign);
        messageBroker.Subscribe<NetworkRemoveWeaponDesign>(HandleCommand_RemoveWeaponDesign);
        messageBroker.Subscribe<WeaponDesignAdded>(HandleEvent_WeaponDesignAdded);
        messageBroker.Subscribe<WeaponDesignRemoved>(HandleEvent_WeaponDesignRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddWeaponDesign>(HandleCommand_AddWeaponDesign);
        messageBroker.Unsubscribe<NetworkRemoveWeaponDesign>(HandleCommand_RemoveWeaponDesign);
        messageBroker.Unsubscribe<WeaponDesignAdded>(HandleEvent_WeaponDesignAdded);
        messageBroker.Unsubscribe<WeaponDesignRemoved>(HandleEvent_WeaponDesignRemoved);
    }

    private void HandleEvent_WeaponDesignAdded(MessagePayload<WeaponDesignAdded> payload)
    {
        var data = payload.What;

        var networkData = CreateNetworkMessageData(data.Crafting, data.WeaponDesign);
        if (networkData == null) return;

        network.SendAll(new NetworkAddWeaponDesign(networkData));
    }

    private void HandleEvent_WeaponDesignRemoved(MessagePayload<WeaponDesignRemoved> payload)
    {
        var data = payload.What;

        var networkData = CreateNetworkMessageData(data.Crafting, data.WeaponDesign);
        if (networkData == null) return;

        network.SendAll(new NetworkRemoveWeaponDesign(networkData));
    }

    private void HandleCommand_RemoveWeaponDesign(MessagePayload<NetworkRemoveWeaponDesign> payload)
    {
        var data = payload.What;
        var instanceId = data.CraftingId;
        var removedWeaponDesignId = data.WeaponDesignId;

        if (objectManager.TryGetObject<Crafting>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(Crafting), instanceId);
            return;
        }

        if (objectManager.TryGetObject<WeaponDesign>(removedWeaponDesignId, out var removedWeaponDesign) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(WeaponDesign), removedWeaponDesignId);
            return;
        }

        instance._history.Remove(removedWeaponDesign);
    }

    private void HandleCommand_AddWeaponDesign(MessagePayload<NetworkAddWeaponDesign> payload)
    {
        var data = payload.What;
        var instanceId = data.CraftingId;
        var addedWeaponDesignId = data.WeaponDesignId;

        if (objectManager.TryGetObject<Crafting>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(Crafting), instanceId);
            return;
        }

        if (objectManager.TryGetObject<WeaponDesign>(addedWeaponDesignId, out var addedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(WeaponDesign), addedWeaponDesignId);
            return;
        }

        instance._history.Add(addedParty);
    }

    private WeaponDesignData CreateNetworkMessageData(Crafting crafting, WeaponDesign weaponDesign)
    {
        if (!TryGetId(crafting, out string craftingId)) return null;
        if (!TryGetId(weaponDesign, out string weaponDesignId)) return null;

        return new WeaponDesignData(craftingId, weaponDesignId);
    }

    private bool TryGetId(object value, out string id)
    {
        id = null;
        if (value == null) return false;

        if (!objectManager.TryGetId(value, out id))
        {
            Logger.Error("Unable to get ID for instance of type {type}", value.GetType().Name);
            return false;
        }

        return true;
    }
}