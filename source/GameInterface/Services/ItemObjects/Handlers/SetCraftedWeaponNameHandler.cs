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

namespace GameInterface.Services.ItemObjects.Handlers
{
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
            messageBroker.Subscribe<CraftedWeaponNameSet>(Handle);
            messageBroker.Subscribe<NetworkSetCraftedWeaponNameServer>(Handle);
            messageBroker.Subscribe<NetworkSetCraftedWeaponNameClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CraftedWeaponNameSet>(Handle);
            messageBroker.Unsubscribe<NetworkSetCraftedWeaponNameServer>(Handle);
            messageBroker.Unsubscribe<NetworkSetCraftedWeaponNameClients>(Handle);
        }

        private void Handle(MessagePayload<CraftedWeaponNameSet> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Weapon, out var weaponId)) return;

            // Send to server from client
            NetworkSetCraftedWeaponNameServer message = new(
                weaponId,
                obj.What.Name.ToString() ?? ""
            );
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkSetCraftedWeaponNameServer> obj)
        {
            // Send from server to all clients
            NetworkSetCraftedWeaponNameClients nameChange = new(obj.What);
            network.SendAll(nameChange);
            SetCraftedWeaponName(nameChange);
        }

        private void Handle(MessagePayload<NetworkSetCraftedWeaponNameClients> obj)
        {
            SetCraftedWeaponName(obj.What);
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
}
