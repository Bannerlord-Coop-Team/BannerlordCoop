using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Handlers
{
    internal class VillageLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<VillageLifetimeHandler>();

        public VillageLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<VillageCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateVillage>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<VillageCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateVillage>(Handle);
        }

        private void Handle(MessagePayload<VillageCreated> payload)
        {
            if (objectManager.AddNewObject(payload.What.Village, out string newId) == false)
            {
                Logger.Error("Failed to add {type} to manager", typeof(Village));
                return;
            }

            network.SendAll(new NetworkCreateVillage(newId));
        }

        private void Handle(MessagePayload<NetworkCreateVillage> payload)
        {
            var newVillage = ObjectHelper.SkipConstructor<Village>();

            var data = payload.What;

            if (objectManager.AddExisting(data.VillageId, newVillage) == false)
            {
                Logger.Error("Failed to add {type} to manager with id {id}", typeof(Village), data.VillageId);
                return;
            }
        }
    }
}
