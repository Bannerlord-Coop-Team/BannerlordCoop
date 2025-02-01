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
using System.Diagnostics;


namespace GameInterface.Services.Equipments.Handlers
{
    /// <summary>
    /// Handles all changes to Equipments on client.
    /// </summary>
    public class ItemSlotHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ItemSlotHandler>();



        public ItemSlotHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ItemSlotsArrayUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateItemSlots>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemSlotsArrayUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateItemSlots>(Handle);
        }

        private void Handle(MessagePayload<ItemSlotsArrayUpdated> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string EquipmentId)) return;
            if (!TryGetId(data.Item, out string ItemId)) return;
            if (!TryGetId(data.ItemModifier, out string ItemModifierId)) return;

            network.SendAll(new NetworkUpdateItemSlots(EquipmentId, ItemId, ItemModifierId, data.Index));
        }

        private void Handle(MessagePayload<NetworkUpdateItemSlots> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.EquipmentId, out Equipment equipment)) return;
            if (!objectManager.TryGetObject(data.ItemId, out ItemObject item)) return;
            if (!objectManager.TryGetObject(data.ItemModifierId, out ItemModifier itemModifier)) return;

            
            equipment._itemSlots[data.Index] = new EquipmentElement(item, itemModifier);
        }

        private bool TryGetId(object value, out string id)
        {
            id = null;
            if (value == null) return false;

            if (!objectManager.TryGetId(value, out id))
            {
                Logger.Error("Unable to get ID for instance of type {type}", value.GetType().Name);
                return false;
            }

            return true;
        }
    }
}

