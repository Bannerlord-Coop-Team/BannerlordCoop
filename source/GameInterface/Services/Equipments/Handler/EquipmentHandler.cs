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
using System;
using GameInterface.Services.Equipments.Data;


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
            Equipment newEquipment = payload.What.Data;
            if (objectManager.TryGetId(newEquipment, out string newEquipmentId) )
            {
                if (payload.What.IsCivilian != null)
                {
                    NetworkCreateEquipment CivilMessage = new(new EquipmentCreatedData(newEquipmentId, null, payload.What.IsCivilian));
                    network.SendAll(CivilMessage);
                }
                else
                {
                    Logger.Error("Server already has {name} in object manager\n"
                    + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
                    
                }
                return;
            }
            string parameterId = null;
            if (payload.What.Param != null)
            {
                if (objectManager.TryGetId(payload.What.Param, out parameterId) == false)
                {
                    // Maybe need to add parameterEquipment in that case to object manager
                    // or check in default Object Manager
                    Logger.Error("Failed to find object in objectManager {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
                }
            }
            if (objectManager.AddNewObject(newEquipment, out newEquipmentId) == false) {
                Logger.Error("Object manager already has equipment object");
                return;
            }
            NetworkCreateEquipment message = new(new EquipmentCreatedData(newEquipmentId, parameterId, payload.What.IsCivilian));
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateEquipment> obj)
        {
            var payload = obj.What.Data;

            objectManager.TryGetObject<Equipment>(payload.EquipmentId, out var testEquipment);
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