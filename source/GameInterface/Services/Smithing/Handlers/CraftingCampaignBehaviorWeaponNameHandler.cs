using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorWeaponNameHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorWeaponNameHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    private WeaponDesignResultPopupVM currentWeaponDesignResultPopupVM;

    public CraftingCampaignBehaviorWeaponNameHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<WeaponDesignResultPopupVMCreated>(Handle);

        messageBroker.Subscribe<BehaviorCraftedWeaponNameSet>(Handle);
        messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameServer>(Handle);
        messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameClients>(Handle);

        messageBroker.Subscribe<UpdateCraftedItem>(Handle);

        currentWeaponDesignResultPopupVM = null;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WeaponDesignResultPopupVMCreated>(Handle);

        messageBroker.Unsubscribe<BehaviorCraftedWeaponNameSet>(Handle);
        messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameServer>(Handle);
        messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameClients>(Handle);

        messageBroker.Unsubscribe<UpdateCraftedItem>(Handle);
    }

    private void Handle(MessagePayload<WeaponDesignResultPopupVMCreated> obj)
    {
        GameThread.RunSafe(() =>
        {
            currentWeaponDesignResultPopupVM = obj.What.WeaponDesignResultPopupVM;
        });  
    }

    private void Handle(MessagePayload<BehaviorCraftedWeaponNameSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;

        // Send to server from client
        NetworkBehaviorSetCraftedWeaponNameServer message = new(
            craftingCampaignBehaviorId,
            obj.What.CraftedWeaponId,
            obj.What.Name
        );
        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkBehaviorSetCraftedWeaponNameServer> obj)
    {
        // obj.What.CraftedItemId
        NetworkBehaviorSetCraftedWeaponNameClients nameChange = new(obj.What);

        GameThread.RunSafe(() =>
        {
            SetCraftedWeaponName(nameChange);

            // Send from server to all clients
            network.SendAll(nameChange);
        });
    }

    private void Handle(MessagePayload<NetworkBehaviorSetCraftedWeaponNameClients> obj)
    {
        SetCraftedWeaponName(obj.What);
    }

    private void SetCraftedWeaponName(NetworkBehaviorSetCraftedWeaponNameClients obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            ItemObject mbCraftedWeapon = MBObjectManager.Instance.GetObject<ItemObject>(obj.CraftedWeaponId);
            if (mbCraftedWeapon == null) return;

            /*
            if (craftingCampaignBehavior._craftedItemDictionary.TryGetValue(mbCraftedWeapon, out CraftingCampaignBehavior.CraftedItemInitializationData craftedItemInitializationData))
            {
                craftingCampaignBehavior._craftedItemDictionary[mbCraftedWeapon] = new CraftingCampaignBehavior.CraftedItemInitializationData(
                    craftedItemInitializationData.CraftedData,
                    new TextObject(obj.StringName),
                    craftedItemInitializationData.Culture);
            }
            */

            using (new AllowedThread())
            {
                craftingCampaignBehavior.SetCraftedWeaponName(mbCraftedWeapon, obj.Name);
            }
        });
    }

    private void Handle(MessagePayload<UpdateCraftedItem> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (currentWeaponDesignResultPopupVM == null) return;

            currentWeaponDesignResultPopupVM._craftedItem.StringId = obj.What.CraftedItemObject.StringId;
        });
    }
}
