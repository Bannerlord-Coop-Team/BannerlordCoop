using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ItemObjects.Messages;
using GameInterface.Services.ItemObjects.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemObjects.Handlers;

internal class SetCraftedWeaponNameHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SetCraftedWeaponNameHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public SetCraftedWeaponNameHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<SetCraftedWeaponName>(Handle_SetCraftedWeaponName);
        messageBroker.Subscribe<NetworkSetCraftedWeaponNameServer>(Handle_NetworkSetCraftedWeaponNameServer);
        messageBroker.Subscribe<NetworkSetCraftedWeaponNameClients>(Handle_NetworkSetCraftedWeaponNameClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetCraftedWeaponName>(Handle_SetCraftedWeaponName);
        messageBroker.Unsubscribe<NetworkSetCraftedWeaponNameServer>(Handle_NetworkSetCraftedWeaponNameServer);
        messageBroker.Unsubscribe<NetworkSetCraftedWeaponNameClients>(Handle_NetworkSetCraftedWeaponNameClients);
    }

    private void Handle_SetCraftedWeaponName(MessagePayload<SetCraftedWeaponName> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Weapon, out var weaponId)) return;

        // Send to server from client
        NetworkSetCraftedWeaponNameServer message = new(
            weaponId,
            obj.What.Name.ToString() ?? ""
        );
        network.SendAll(message);
    }

    private void Handle_NetworkSetCraftedWeaponNameServer(MessagePayload<NetworkSetCraftedWeaponNameServer> obj)
    {
        NetworkSetCraftedWeaponNameClients nameChange = new(obj.What);

        GameThread.RunSafe(() =>
        {
            SetCraftedWeaponName(nameChange);

            // Send from server to all clients
            network.SendAll(nameChange);
        });
    }

    private void Handle_NetworkSetCraftedWeaponNameClients(MessagePayload<NetworkSetCraftedWeaponNameClients> obj)
    {
        GameThread.RunSafe(() =>
        {
            SetCraftedWeaponName(obj.What);
        });
    }

    private void SetCraftedWeaponName(NetworkSetCraftedWeaponNameClients obj)
    {
        if (!objectManager.TryGetObjectWithLogging(obj.WeaponId, out ItemObject weapon)) return;
        ItemObject mbCraftedWeapon = MBObjectManager.Instance.GetObject<ItemObject>(weapon.StringId);

        // Change name on custom and MB object managers
        SetCraftedWeaponNamePatch.SetCraftedWeaponNameOverride(ref weapon, obj.StringName);
        SetCraftedWeaponNamePatch.SetCraftedWeaponNameOverride(ref mbCraftedWeapon, obj.StringName);
    }
}
