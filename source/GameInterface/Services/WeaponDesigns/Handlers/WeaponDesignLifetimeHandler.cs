using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemObjects.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.WeaponDesigns.Messages;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.WeaponDesigns.Handlers
{
    internal class WeaponDesignLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<WeaponDesignLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public WeaponDesignLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<WeaponDesignCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateWeaponDesign>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WeaponDesignCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateWeaponDesign>(Handle);
        }

        private void Handle(MessagePayload<WeaponDesignCreated> payload)
        {
            var data = payload.What;

            if (objectManager.AddNewObject(data.WeaponDesign, out string weaponDesignId) == false) return;

            var message = new NetworkCreateWeaponDesign(weaponDesignId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateWeaponDesign> payload)
        {
            var data = payload.What;

            var weaponDesign = ObjectHelper.SkipConstructor<WeaponDesign>();
            if (objectManager.AddExisting(data.WeaponDesignId, weaponDesign) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", data.WeaponDesignId);
                return;
            }
        }
    }
}
