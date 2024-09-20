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


namespace GameInterface.Services.Equipments.Handlers
{
    /// <summary>
    /// Handles all changes to Equipments on client.
    /// </summary>
    public class EquipmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EquipmentHandler>();

        public EquipmentHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<EquipmentCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateEquipment>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<EquipmentCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateEquipment>(Handle);
        }

        private void Handle(MessagePayload<EquipmentCreated> payload)
        {
            NetworkCreateEquipment message = new(payload.What.Data);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateEquipment> obj)
        {
            var payload = obj.What.Data;
            Equipment propertyEquipment = null;
            if (payload.EquipmentPropertyId != null)
            {
                if (objectManager.TryGetObject(payload.EquipmentPropertyId, out propertyEquipment) == false)
                {
                    return;
                }
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    object Equipment = null;
                    if (propertyEquipment != null)
                    {
                        Equipment = new Equipment(propertyEquipment);
                    }
                    else if(payload.IsCivil != null)
                    {
                        Equipment = new Equipment((bool)payload.IsCivil);
                    }
                    else
                    {
                        Equipment = new Equipment();
                    }
                    objectManager.AddExisting(payload.EquipmentId, Equipment);
                }
            });
        }
    }
}