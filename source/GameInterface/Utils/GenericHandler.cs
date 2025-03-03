using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using GameInterface.Utils.NetworkEvents;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;

namespace GameInterface.Utils
{
    public class GenericHandler<TInstance, THandler> : IHandler
        where TInstance : class
        where THandler : IHandler
    {
        protected readonly IMessageBroker messageBroker;
        protected readonly IObjectManager objectManager;
        protected readonly INetwork network;
        protected readonly ILogger Logger = LogManager.GetLogger<THandler>();

        private readonly List<object> handlers = new List<object>();

        public GenericHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
        }

        protected void Subscribe<TValue, TMessage>(Action<string, TMessage> messageHandler)
            where TMessage : GenericEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!TryGetId(data.Instance, out string instanceId)) return;
                messageHandler(instanceId, data);
            };
            messageBroker.Subscribe(payloadHandler);
            handlers.Add(payloadHandler);
        }
        protected void SubscribeGenericReference<TValue, TMessage, TNetworkMessage>()
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!TryGetId(data.Instance, out string instanceId)) return;
                if (!TryGetId(data.Value, out string valueId)) return;
                network.SendAll((TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, valueId }));
            };
            messageBroker.Subscribe(payloadHandler);
            handlers.Add(payloadHandler);
        }

        protected void SubscribeNetwork<TValue, TMessage>(Action<TInstance, TMessage> messageHandler)
            where TMessage : GenericNetworkEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;
                messageHandler(instance, data);
            };
            messageBroker.Subscribe(payloadHandler);
            handlers.Add(payloadHandler);
        }
        protected void SubscribeNetworkReference<TValue, TMessage>(Action<TInstance, TValue, TMessage> messageHandler)
            where TValue : class
            where TMessage : GenericNetworkReferenceEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;
                if (!objectManager.TryGetObject(data.ValueId, out TValue value)) return;
                messageHandler(instance, value, data);
            };
            messageBroker.Subscribe(payloadHandler);
            handlers.Add(payloadHandler);
        }


        protected bool TryGetId(object value, out string id)
        {
            id = null;
            if (value == null) return false;

            if (!objectManager.TryGetId(value, out id))
            {
                Logger.Error("Unable to get ID for instance of type {type}", value.GetType());
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            var method = AccessTools.Method(messageBroker.GetType(), "Unsubscribe");
            foreach (var handler in handlers)
                method.Invoke(messageBroker, new object[] { handler });
        }
    }
}
