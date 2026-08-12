using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Transactions;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorWeaponNameHandler : IHandler
{
    private static readonly ILogger Logger =
        LogManager.GetLogger<CraftingCampaignBehaviorWeaponNameHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface;

    private WeaponDesignResultPopupVM currentWeaponDesignResultPopupVM;

    public CraftingCampaignBehaviorWeaponNameHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.craftingCampaignBehaviorInterface = craftingCampaignBehaviorInterface;

        messageBroker.Subscribe<WeaponDesignResultPopupVMCreated>(
            Handle_WeaponDesignResultPopupVMCreated);
        messageBroker.Subscribe<SetBehaviorCraftedWeaponName>(
            Handle_SetBehaviorCraftedWeaponName);
        messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameServer>(
            Handle_NetworkBehaviorSetCraftedWeaponNameServer);
        messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameClients>(
            Handle_NetworkBehaviorSetCraftedWeaponNameClients);
        messageBroker.Subscribe<UpdateCraftedItem>(Handle_UpdateCraftedItem);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WeaponDesignResultPopupVMCreated>(
            Handle_WeaponDesignResultPopupVMCreated);
        messageBroker.Unsubscribe<SetBehaviorCraftedWeaponName>(
            Handle_SetBehaviorCraftedWeaponName);
        messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameServer>(
            Handle_NetworkBehaviorSetCraftedWeaponNameServer);
        messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameClients>(
            Handle_NetworkBehaviorSetCraftedWeaponNameClients);
        messageBroker.Unsubscribe<UpdateCraftedItem>(Handle_UpdateCraftedItem);
    }

    private void Handle_WeaponDesignResultPopupVMCreated(
        MessagePayload<WeaponDesignResultPopupVMCreated> obj)
    {
        GameThread.RunSafe(() =>
            currentWeaponDesignResultPopupVM =
                obj.What.WeaponDesignResultPopupVM);
    }

    private void Handle_SetBehaviorCraftedWeaponName(
        MessagePayload<SetBehaviorCraftedWeaponName> obj)
    {
        if (ModInformation.IsServer) return;
        network.SendAll(new NetworkBehaviorSetCraftedWeaponNameServer(
            obj.What.CraftedWeaponId,
            obj.What.Name));
    }

    private void Handle_NetworkBehaviorSetCraftedWeaponNameServer(
        MessagePayload<NetworkBehaviorSetCraftedWeaponNameServer> obj)
    {
        if (!ModInformation.IsServer || obj.Who is not NetPeer peer)
            return;

        GameThread.RunSafe(() =>
        {
            try
            {
                string safeName = new string((obj.What.Name?.ToString() ?? "")
                    .Where(character => !char.IsControl(character))
                    .Take(64)
                    .ToArray()).Trim();
                if (safeName.Length == 0 ||
                    !ServerTransactionOutcome.TryConsumeCraftRename(
                        peer, obj.What.CraftedWeaponId))
                    return;

                var sanitized =
                    new NetworkBehaviorSetCraftedWeaponNameServer(
                        obj.What.CraftedWeaponId,
                        new TextObject("{=!}" + safeName));
                var nameChange =
                    new NetworkBehaviorSetCraftedWeaponNameClients(sanitized);
                if (!TrySetCraftedWeaponName(nameChange))
                    return;
                network.SendAll(nameChange);
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Failed to apply crafted weapon rename");
            }
        });
    }

    private void Handle_NetworkBehaviorSetCraftedWeaponNameClients(
        MessagePayload<NetworkBehaviorSetCraftedWeaponNameClients> obj)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => TrySetCraftedWeaponName(obj.What));
    }

    private bool TrySetCraftedWeaponName(
        NetworkBehaviorSetCraftedWeaponNameClients obj)
    {
        if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(
                out var craftingBehavior))
            return false;

        ItemObject craftedWeapon =
            MBObjectManager.Instance.GetObject<ItemObject>(
                obj.CraftedWeaponId);
        if (craftedWeapon == null)
            return false;

        using (new AllowedThread())
        {
            craftingBehavior.SetCraftedWeaponName(
                craftedWeapon, obj.Name);
        }
        return true;
    }

    private void Handle_UpdateCraftedItem(
        MessagePayload<UpdateCraftedItem> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (currentWeaponDesignResultPopupVM == null) return;

            MBObjectManager.Instance.UnregisterObject(
                currentWeaponDesignResultPopupVM._craftedItem);
            currentWeaponDesignResultPopupVM._craftedItem.StringId =
                obj.What.CraftedItemObject.StringId;

            TextObject name = new TextObject(
                "{=!}" + currentWeaponDesignResultPopupVM.ItemName);
            currentWeaponDesignResultPopupVM._crafting
                .SetCraftedWeaponName(name);
            currentWeaponDesignResultPopupVM._craftingBehavior
                .SetCraftedWeaponName(
                    obj.What.CraftedItemObject, name);
        });
    }
}
