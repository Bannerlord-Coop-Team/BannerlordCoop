using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Equipments.Messages;
using Serilog;
using TaleWorlds.Core;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Core.Equipment;


namespace GameInterface.Services.Equipments.Handlers
{
    /// <summary>
    /// Handles all changes to Equipments on client.
    /// </summary>
    public class EquipmentTypeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EquipmentHandler>();

        public EquipmentTypeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<EquipmentTypeChanged>(Handle);
            messageBroker.Subscribe<NetworkEquipmentTypeChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<EquipmentTypeChanged>(Handle);
            messageBroker.Unsubscribe<NetworkEquipmentTypeChanged>(Handle);
        }

        private void Handle(MessagePayload<EquipmentTypeChanged> payload)
        {
            NetworkEquipmentTypeChanged message = new(payload.What.EquipmentType, payload.What.EquipmentId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkEquipmentTypeChanged> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Equipment>(payload.EquipmentId, out var instance) == false)
                {
                    Logger.Error("Unable to find {type} with id: {id}", typeof(Equipment), payload.EquipmentId);
                    return;
                }

            // Not sure if running on Main Thread is needed here
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    instance._equipmentType = (EquipmentType) payload.EquipmentType;
                }
            });
        }
    }
}