using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using GameInterface.Services.VillageTypes.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageTypes.Handlers
{
    internal class VillageTypeLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<VillageTypeLifetimeHandler>();

        public VillageTypeLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<VillageTypeCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateVillageType>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<VillageTypeCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateVillageType>(Handle);
        }

        private void Handle(MessagePayload<VillageTypeCreated> payload)
        {
            if (objectManager.AddNewObject(payload.What.VillageType, out string newId) == false)
            {
                Logger.Error("Failed to add {type} to manager", typeof(VillageType));
                return;
            }

            network.SendAll(new NetworkCreateVillageType(newId));
        }

        private void Handle(MessagePayload<NetworkCreateVillageType> payload)
        {
            var data = payload.What;

            var newVillageType = DefaultVillageTypes.Instance.Create(data.VillageTypeId);

            if (objectManager.AddExisting(data.VillageTypeId, newVillageType) == false)
            {
                Logger.Error("Failed to add {type} to manager with id {id}", typeof(VillageType), data.VillageTypeId);
                return;
            }
        }
    }
}
