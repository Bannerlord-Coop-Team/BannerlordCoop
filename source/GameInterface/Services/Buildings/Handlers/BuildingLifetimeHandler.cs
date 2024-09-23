using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Handlers;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Handlers
{
    internal class BuildingLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();
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

            if (objectManager.TryGetId(payload.Town, out var townId) == false) return;

            objectManager.AddNewObject(payload.Building, out string buildingId);

            var message = new NetworkCreateBuilding(buildingId, payload.BuildingType.StringId, townId, payload.BuildingProgress, payload.CurrentLevel);
            network.SendAll(message);
        }

        private void Handle_CreateBuilding(MessagePayload<NetworkCreateBuilding> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.TownId, out Town town) == false) return;

            BuildingType buildingType = BuildingType.All.Find(x => x.StringId == payload.BuildingTypeId);

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    var building = new Building(buildingType, town, payload.BuildingProgress, payload.CurrentLevel);
                    objectManager.AddExisting(payload.BuildingId, building);
                }
            });
        }
    }
}
