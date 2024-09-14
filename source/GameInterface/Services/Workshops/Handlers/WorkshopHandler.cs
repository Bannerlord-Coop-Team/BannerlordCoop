using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;


namespace GameInterface.Services.Workshops.Handlers
{
    /// <summary>
    /// Handles all changes to Workshops on client.
    /// </summary>
    public class WorkshopHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<WorkshopHandler>();

        public WorkshopHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<WorkshopCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateWorkshop>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WorkshopCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateWorkshop>(Handle);
        }

        private void Handle(MessagePayload<WorkshopCreated> payload)
        {
            NetworkCreateWorkshop message = new(payload.What.Data);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateWorkshop> obj)
        {
            var payload = obj.What.Data;

            if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false) {
                return;
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    var Workshop = new Workshop(settlement, payload.Tag);
                    objectManager.AddExisting(payload.WorkshopId, Workshop);
                }
            });
        }
    }
}