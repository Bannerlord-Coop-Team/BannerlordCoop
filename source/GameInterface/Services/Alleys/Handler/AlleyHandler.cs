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
using GameInterface.Services.Alleys.Messages;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;


namespace GameInterface.Services.Alleys.Handlers
{
    /// <summary>
    /// Handles all changes to Equipments on client.
    /// </summary>
    public class AlleyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<AlleyHandler>();
        //private static readonly ConstructorInfo Equipment_ctor = AccessTools.Constructor(typeof(Alley));
        private static readonly ConstructorInfo Alley_ctor = AccessTools.Constructor(typeof(Alley), new Type[] { typeof(Settlement), typeof(string), typeof(TextObject) } );


        public AlleyHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<AlleyCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateAlley>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AlleyCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateAlley>(Handle);
        }

        private void Handle(MessagePayload<AlleyCreated> payload)
        {
            if (objectManager.AddNewObject(payload.What.Instance, out var newAlleyId) == false)
            {
                Logger.Error("Failed to add object for {type}\n"
                + "Callstack: {callstack}", typeof(Alley), Environment.StackTrace);
                return;
            }
            if (objectManager.TryGetId(payload.What.Settlement, out var settlementId) == false)
            {
                Logger.Error("Failed to get object {Param} for {type}\n" + 
                    "Callstack: {callstack}", typeof(Settlement), typeof(Alley), Environment.StackTrace);
                return;
            }
            var message = new NetworkCreateAlley(newAlleyId, settlementId, payload.What.Tag, payload.What.Name);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateAlley> payload)
        { 
            if (objectManager.TryGetObject(payload.What.SettlementId, out Settlement Settlement) == false)
            {
                Logger.Error("Failed to get object {Param} for {type}\n"
                + "Callstack: {callstack}", typeof(Settlement), typeof(Alley), Environment.StackTrace);
                return;
            }

            var Alley = ObjectHelper.SkipConstructor<Alley>();
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    try 
                    { 
                        Alley_ctor.Invoke(Alley, new Object[] {Settlement, payload.What.Tag, new TextObject(payload.What.Name)}); 
                    }
                    catch (Exception e) 
                    {
                        Logger.Error(e, "Failed to invoke constructor for {type}", typeof(Alley)); 
                    }
                }
                objectManager.AddExisting(payload.What.AlleyId, Alley);
            });
        }
    }
}