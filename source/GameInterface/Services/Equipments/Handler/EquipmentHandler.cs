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
    public class EquipmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EquipmentHandler>();
        private static readonly ConstructorInfo Equipment_ctor = AccessTools.Constructor(typeof(Equipment));
        private static readonly ConstructorInfo EquipmentParam_ctor = AccessTools.Constructor(typeof(Equipment), new Type[] { typeof(Equipment) });


        public EquipmentHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<EquipmentCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateEquipment>(Handle);
            messageBroker.Subscribe<EquipmentRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveEquipment>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<EquipmentCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateEquipment>(Handle);
            messageBroker.Unsubscribe<EquipmentRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveEquipment>(Handle);
        }

        private void Handle(MessagePayload<EquipmentCreated> payload)
        {

            if (objectManager.TryGetId(payload.What.Data, out string newEquipmentId) )
            {                
                Logger.Error("Server already has {name} in object manager\n"
                    + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
                    
                return;
            }
            if (objectManager.AddNewObject(payload.What.Data, out newEquipmentId) == false)
            {
                Logger.Error("Create new equipment object failed");
                return;
            }
            NetworkCreateEquipment message = new(new EquipmentCreatedData(newEquipmentId));
            network.SendAll(message);


        }

        private void Handle(MessagePayload<NetworkCreateEquipment> obj)
        {
            var payload = obj.What.Data;

            Equipment newEquipment = ObjectHelper.SkipConstructor<Equipment>();
            GameLoopRunner.RunOnMainThread(() =>
            {
                  using (new AllowedThread())
                  {
                      Equipment_ctor.Invoke(newEquipment, Array.Empty<object>());
                  }
                  objectManager.AddExisting(payload.EquipmentId, newEquipment);
               
            });
        }

        private void Handle(MessagePayload<EquipmentRemoved> obj)
        {
            var payload = obj.What;
            if (objectManager.TryGetId(payload.battleEquipment, out string battleEquipmentId) == false)
            {
                Logger.Error("Failed to get ID for server removal of {type}", typeof(Equipment));
                return;
            }
            if (objectManager.TryGetId(payload.civilEquipment, out string civEquipmentId) == false)
            {
                Logger.Error("Failed to get ID for server removal of {type}", typeof(Equipment));
                return;
            }
            if (objectManager.Remove(payload.battleEquipment) == false)
            {
                Logger.Error("Failed to remove {type}", typeof(Equipment));
                return;
            }
            if (objectManager.Remove(payload.civilEquipment) == false)
            {
                Logger.Error("Failed to remove {type}", typeof(Equipment));
                return;
            }
            NetworkRemoveEquipment message = new(battleEquipmentId, civEquipmentId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkRemoveEquipment> payload) { 
            
            if (objectManager.TryGetObject(payload.What.BattleEquipmentId, out Equipment BattleEquipment) == false)
            {
                Logger.Error("Failed to get object for {type} with id {id}", typeof(Equipment), payload.What.BattleEquipmentId);
                return;
            }
            if (objectManager.TryGetObject(payload.What.CivilEquipmentId, out Equipment CivilEquipment) == false)
            {
                Logger.Error("Failed to get object for {type} with id {id}", typeof(Equipment), payload.What.BattleEquipmentId);
                return;
            }
            if (objectManager.Remove(BattleEquipment) == false)
            {
                Logger.Error("Failed to remove {type} with id { id}", typeof(Equipment), payload.What.BattleEquipmentId);
                return;
            }
            if (objectManager.Remove(CivilEquipment) == false)
            {
                Logger.Error("Failed to remove {type} with id { id}", typeof(Equipment), payload.What.CivilEquipmentId);
                return;
            }
        }
    }
    
}