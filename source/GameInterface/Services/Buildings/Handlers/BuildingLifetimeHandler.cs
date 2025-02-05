using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Handlers
{
    internal class BuildingLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BuildingLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public BuildingLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<BuildingCreated>(Handle_BuildingCreated);
            messageBroker.Subscribe<NetworkCreateBuilding>(Handle_CreateBuilding);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BuildingCreated>(Handle_BuildingCreated);
            messageBroker.Unsubscribe<NetworkCreateBuilding>(Handle_CreateBuilding);
        }

        private void Handle_BuildingCreated(MessagePayload<BuildingCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.Building, out string buildingId) == false) return;

            var message = new NetworkCreateBuilding(buildingId);
            network.SendAll(message);
        }

        private void Handle_CreateBuilding(MessagePayload<NetworkCreateBuilding> obj)
        {
            var payload = obj.What;

            var building = ObjectHelper.SkipConstructor<Building>();
            if (objectManager.AddExisting(payload.BuildingId, building) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", payload.BuildingId);
                return;
            }
        }
    }
}
