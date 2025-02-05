using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Messages;
using GameInterface.Services.Workshops.Patches;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Handlers
{
    public class WorkshopPropertyHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyFieldsHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public WorkshopPropertyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<WorkshopPropertyChanged>(Handle_PropertyChanged);
            messageBroker.Subscribe<NetworkWorkshopChangeProperty>(Handle_ChangeProperty);
        }

        private void Handle_PropertyChanged(MessagePayload<WorkshopPropertyChanged> payload)
        {
            var data = payload.What;

            objectManager.TryGetId(data.workshop, out string workshopId);

            NetworkWorkshopChangeProperty message = new(data._propertyType, workshopId, data.mainData, data.extraData);
            network.SendAll(message);
        }

        private void Handle_ChangeProperty(MessagePayload<NetworkWorkshopChangeProperty> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Workshop>(data.workshopId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Workshop), data.workshopId);
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

        private void HandleDataChanged(Workshop instance, NetworkWorkshopChangeProperty data)
        {
            switch (data._propertyType)
            {
                case PropertyType.Capital:
                    instance.Capital = int.Parse(data.mainData);
                    break;

                case PropertyType.LastRunCampaignTime:
                    instance.LastRunCampaignTime = new CampaignTime(long.Parse(data.mainData));
                    break;

                case PropertyType.WorkshopType:
                    if (objectManager.TryGetObject(data.mainData, out WorkshopType type) == false)
                    {
                        Logger.Error("Unable to find {type} with id: {id}", typeof(WorkshopType), data.mainData);
                        return;
                    }
                    instance.WorkshopType = type;
                    break;

                case PropertyType.InitialCapital:
                    instance.InitialCapital = int.Parse(data.mainData);
                    break;
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WorkshopPropertyChanged>(Handle_PropertyChanged);
            messageBroker.Unsubscribe<NetworkWorkshopChangeProperty>(Handle_ChangeProperty);
        }
    }
}
