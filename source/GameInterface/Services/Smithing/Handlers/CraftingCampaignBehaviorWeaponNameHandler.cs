using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorWeaponNameHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorWeaponNameHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface;

    public CraftingCampaignBehaviorWeaponNameHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.craftingCampaignBehaviorInterface = craftingCampaignBehaviorInterface;

        messageBroker.Subscribe<SetBehaviorCraftedWeaponName>(Handle_SetBehaviorCraftedWeaponName);
        messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameServer>(Handle_NetworkBehaviorSetCraftedWeaponNameServer);
        messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameClients>(Handle_NetworkBehaviorSetCraftedWeaponNameClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetBehaviorCraftedWeaponName>(Handle_SetBehaviorCraftedWeaponName);
        messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameServer>(Handle_NetworkBehaviorSetCraftedWeaponNameServer);
        messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameClients>(Handle_NetworkBehaviorSetCraftedWeaponNameClients);
    }

    private void Handle_SetBehaviorCraftedWeaponName(MessagePayload<SetBehaviorCraftedWeaponName> obj)
    {
        // Send to server from client
        NetworkBehaviorSetCraftedWeaponNameServer message = new(
            obj.What.CraftedWeaponId,
            obj.What.Name
        );
        network.SendAll(message);
    }

    private void Handle_NetworkBehaviorSetCraftedWeaponNameServer(MessagePayload<NetworkBehaviorSetCraftedWeaponNameServer> obj)
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

    private void Handle_NetworkBehaviorSetCraftedWeaponNameClients(MessagePayload<NetworkBehaviorSetCraftedWeaponNameClients> obj)
    {
        SetCraftedWeaponName(obj.What);
    }

    private void SetCraftedWeaponName(NetworkBehaviorSetCraftedWeaponNameClients obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;

            ItemObject mbCraftedWeapon = MBObjectManager.Instance.GetObject<ItemObject>(obj.CraftedWeaponId);
            if (mbCraftedWeapon == null) return;

            using (new AllowedThread())
            {
                craftingBehavior.SetCraftedWeaponName(mbCraftedWeapon, obj.Name);
            }
        });
    }
}
