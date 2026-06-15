using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ItemObjects.Messages;
using GameInterface.Services.ItemObjects.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
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
            NetworkSetCraftedWeaponNameClients nameChange = new(obj.What);

            // Applying the rename runs vanilla game code, so it must run on the main thread,
            // not the network thread that delivered the message. The server relays to all
            // clients only after it has applied the change itself.
            GameLoopRunner.RunOnMainThread(() =>
            {
                try
                {
                    SetCraftedWeaponName(nameChange);

                    // Send from server to all clients
                    network.SendAll(nameChange);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkSetCraftedWeaponNameServer");
                }
            });
        }

        private void Handle(MessagePayload<NetworkSetCraftedWeaponNameClients> obj)
        {
            var data = obj.What;

            // Applying the rename runs vanilla game code, so it must run on the main thread,
            // not the network thread that delivered the message.
            GameLoopRunner.RunOnMainThread(() =>
            {
                try
                {
                    SetCraftedWeaponName(data);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkSetCraftedWeaponNameClients");
                }
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
}
