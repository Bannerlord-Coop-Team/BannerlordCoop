﻿using Common;
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
    public class EquipmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EquipmentHandler>();
        private static readonly ConstructorInfo Equipment_ctor = AccessTools.Constructor(typeof(Equipment));
        private static readonly ConstructorInfo EquipmentParam_ctor = AccessTools.Constructor(typeof(Equipment), new Type[] { typeof(Equipment) });
        private static readonly ConstructorInfo EquipmentCivil_ctor = AccessTools.Constructor(typeof(Equipment), new Type[] { typeof(bool) });

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

            if (objectManager.TryGetId(payload.What.Data, out string newEquipmentId) )
            {   //  Check if we want to construct via chained constructor
                
                Logger.Error("Server already has {name} in object manager\n"
                    + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
                    
                return;
            }
            string ParamId = null;
            if (payload.What.Param != null)
            {
                if (objectManager.TryGetId(payload.What.Param, out ParamId) == false)
                {
                    Logger.Error("Equipment param not found in object manager");
                    return;
                }
            }
            if (objectManager.AddNewObject(payload.What.Data, out newEquipmentId) == false) {
                Logger.Error("Create new equipment object failed");
                return;
            }
            NetworkCreateEquipment message = new(new EquipmentCreatedData(newEquipmentId, ParamId, payload.What.IsCivilian));
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

            Equipment newEquipment = ObjectHelper.SkipConstructor<Equipment>();
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {

                    if (propertyEquipment != null)
                    {
                        using (new AllowedThread())
                        {
                            EquipmentParam_ctor.Invoke(newEquipment, new object[] { propertyEquipment });
                        }
                    }
                    else if (payload.IsCivil != null)
                    {
                        using (new AllowedThread())
                        {
                            EquipmentCivil_ctor.Invoke(newEquipment, new object[] { payload.IsCivil });
                        }
                    }
                    else
                    {
                        using (new AllowedThread())
                        {
                            Equipment_ctor.Invoke(newEquipment, Array.Empty<object>());
                        }
                    }
                    objectManager.AddExisting(payload.EquipmentId, newEquipment);
                 }
            });
        }
    }
}