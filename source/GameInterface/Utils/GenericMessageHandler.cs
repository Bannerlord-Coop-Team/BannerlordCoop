using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using GameInterface.Utils.NetworkEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace GameInterface.Utils
{
    public class GenericMessageHandler<THandler> : IHandler where THandler: IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        private readonly List<object> registeredHandlers = new List<object>();

        public GenericMessageHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
        }

        protected void RegisterEvent<TEvent>()
            where TEvent : IGenericEvent
        {
            Action<MessagePayload<TEvent>> handler = payload =>
            {
                payload.What.HandleEvent(objectManager, network);
            };
            registeredHandlers.Add(handler);
            messageBroker.Subscribe(handler);
        }

        protected void RegisterNetworkEvent<TEvent>()
            where TEvent : IGenericBaseNetworkEvent
        {
            Action<MessagePayload<TEvent>> handler = payload =>
            {
                payload.What.HandleEvent(objectManager);
            };
            registeredHandlers.Add(handler);
            messageBroker.Subscribe(handler);
        }

        public void Dispose()
        {
            var method = AccessTools.Method(messageBroker.GetType(), "unsubscribe");
            registeredHandlers.ForEach(handler => method.Invoke(messageBroker, new object[] { handler }));
        }
    }
}
