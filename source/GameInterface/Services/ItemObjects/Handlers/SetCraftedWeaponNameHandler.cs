using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ItemObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
            if (!objectManager.TryGetId(obj.What.Weapon, out var weaponId))
            {
                Logger.Error("Unable to get network ID for ItemObject instance of type {type}", obj.What.Weapon);
                return;
            }

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
            if (!objectManager.TryGetObject(obj.WeaponId, out ItemObject weapon))
            {
                Logger.Error("Unable to get Weapon ItemObject for id {id}", obj.WeaponId);
                return;
            }

            if (weapon.StringId == null) {
                Logger.Warning("Tried to set crafted weapon name for weapon with null StringId");
                return;
            }

            // Replace TaleWorlds implementation
            weapon.Name = new TextObject(obj.StringName);
            if (weapon.WeaponDesign != null)
            {
                weapon.WeaponDesign.SetWeaponName(weapon.Name);
            }
        }
    }
}
