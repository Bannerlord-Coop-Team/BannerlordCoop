using Common.Logging;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Common.Util;
using GameInterface.Services.WorkshopTypes.Messages;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.WorkshopTypes.Handlers
{
    internal class WorkshopTypeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyFieldsHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public WorkshopTypeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ProductionsChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeProductions>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ProductionsChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeProductions>(Handle);
        }

        private void Handle(MessagePayload<ProductionsChanged> payload)
        {
            var data = payload.What;

            objectManager.TryGetId(data.workshopType, out string workshopTypeId);

            NetworkChangeProductions message = new(workshopTypeId, data.isAdd, data.production._conversionSpeed);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkChangeProductions> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<WorkshopType>(data.workshopTypeId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(WorkshopType), data.workshopTypeId);
                return;
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    HandleDataChanged(instance, data);
                }
            });
        }

        private void HandleDataChanged(WorkshopType instance, NetworkChangeProductions data)
        {
            if (data.isAdd)
            {
                instance._productions.Add(new WorkshopType.Production(data.conversionSpeed));
            }
            else
            {
                instance._productions.Remove(new WorkshopType.Production(data.conversionSpeed));
            }
        }
    }
}
