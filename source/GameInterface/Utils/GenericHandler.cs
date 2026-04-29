using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using GameInterface.Utils.NetworkEvents;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Utils
{
    public class GenericHandler<THandler, TInstance> : IHandler
        where TInstance : class
        where THandler : IHandler
    {
        protected readonly IMessageBroker messageBroker;
        protected readonly IObjectManager objectManager;
        protected readonly INetwork network;
        protected readonly ILogger Logger = LogManager.GetLogger<THandler>();

        private readonly List<Action> disposeFunctions = new List<Action>();

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

                if (!objectManager.TryGetIdWithLogging(data.Instance, out string instanceId)) return;

                messageHandler(instanceId, data);
            };
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }
        protected void SubscribeGenericReference<TValue, TMessage, TNetworkMessage>()
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceEvent<TInstance, TValue>
        {
            // Get ctor with most parameters to invoke
            var ctor = typeof(TNetworkMessage).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();

            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!objectManager.TryGetIdWithLogging(data.Instance, out string instanceId)) return;

                string valueId = null;
                if (data.Value != null && !objectManager.TryGetIdWithLogging(data.Value, out valueId)) return;

                network.SendAll((TNetworkMessage)ctor.Invoke(new object[] { instanceId, valueId }));
            };
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }

        protected void SubscribeNetwork<TValue, TMessage>(Action<TInstance, TMessage> messageHandler)
            where TMessage : GenericNetworkEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!objectManager.TryGetObjectWithLogging(data.InstanceId, out TInstance instance)) return;
                AllowedThread.AllowThisThread();
                messageHandler(instance, data);
                AllowedThread.RevokeThisThread();
            };
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }
        protected void SubscribeNetworkReference<TValue, TMessage>(Action<TInstance, TValue, TMessage> messageHandler)
            where TValue : class
            where TMessage : GenericNetworkReferenceEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!objectManager.TryGetObjectWithLogging(data.InstanceId, out TInstance instance)) return;

                TValue value = null;
                if (data.ValueId != null && !objectManager.TryGetObjectWithLogging(data.ValueId, out value)) return;

                using (new AllowedThread())
                {
                    messageHandler(instance, value, data);
                }
            };
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }

        public void Dispose()
        {
            foreach (var disposeFn in disposeFunctions)
                disposeFn();
        }
    }
}
