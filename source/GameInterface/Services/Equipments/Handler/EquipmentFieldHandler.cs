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
using HarmonyLib;
using System.Reflection;


namespace GameInterface.Services.Equipments.Handlers
{
    /// <summary>
    /// Handles all changes to Equipments on client.
    /// </summary>
    public class EquipmentFieldHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EquipmentHandler>();
        private static readonly ConstructorInfo Equipment_ctor = AccessTools.Constructor(typeof(Equipment));


        public EquipmentFieldHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ItemSlotsCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateItemSlots>(Handle);

        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemSlotsCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateItemSlots>(Handle);
        }

        private void Handle(MessagePayload<ItemSlotsCreated> payload)
        {
            if (objectManager.TryGetId(payload.What, out string EquipmentId) == false)
            {
                Logger.Error("Equipment for ItemSlot sync not found in object manager");
                objectManager.AddNewObject(payload.What, out EquipmentId);
            }

            NetworkCreateItemSlots message = new(new ItemSlotsCreatedData(EquipmentId, payload.What.instance._itemSlots));
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateItemSlots> obj)
        {
            var payload = obj.What.Data;
            if (objectManager.TryGetObject(payload.EquipmentId, out Equipment newEquipment) == false)
            {
                Logger.Error("Equipment for ItemSlot sync not found in object manager");
                return;
            }
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    //newEquipment._itemSlots = new EquipmentElement[12];
                    objectManager.AddExisting(payload.EquipmentId, newEquipment);
                }
            });
        }
    }
}