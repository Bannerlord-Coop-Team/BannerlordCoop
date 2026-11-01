using Common;
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
        protected readonly ILogger Logger;

        private readonly List<Action> disposeFunctions = new List<Action>();

        // Only touched inside MarshalApply, so always on the game thread
        private readonly HashSet<uint> failedInstanceIds = new HashSet<uint>();

        public GenericHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
            : this(messageBroker, objectManager, network, LogManager.GetLogger<THandler>())
        {
        }

        protected GenericHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network, ILogger logger)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            Logger = logger;
        }

        /// <summary>
        /// Subscribes directly to a message type while still unsubscribing when the handler is disposed.
        /// Use this instead of calling <see cref="IMessageBroker.Subscribe{T}"/> directly, otherwise the
        /// subscription leaks past the handler's lifetime.
        /// </summary>
        protected void SubscribeTracked<TMessage>(Action<MessagePayload<TMessage>> payloadHandler)
            where TMessage : IMessage
        {
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }

        protected void Subscribe<TValue, TMessage>(Action<uint, TMessage> messageHandler)
            where TMessage : GenericEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;

                if (!objectManager.TryGetHandleWithLogging(data.Instance, out var instanceId)) return;

                messageHandler(instanceId, data);
            };
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }
        protected void SubscribeGenericReference<TValue, TMessage, TNetworkMessage>(Predicate<TValue> skipUnregisteredValue = null)
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceEvent<TInstance, TValue>
        {
            // Get ctor with most parameters to invoke
            var ctor = typeof(TNetworkMessage).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();

            Action<MessagePayload<TMessage>> payloadHandler = (payload) =>
            {
                var data = payload.What;
                if (!objectManager.TryGetHandleWithLogging(data.Instance, out var instanceId)) return;

                uint valueId = 0;
                if (data.Value != null && skipUnregisteredValue?.Invoke(data.Value) == true
                    && !objectManager.TryGetHandle(data.Value, out _)) return;
                if (data.Value != null && !objectManager.TryGetHandleWithLogging(data.Value, out valueId)) return;

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

                MarshalApply(() =>
                {
                    if (!objectManager.TryGetObjectWithLogging(data.InstanceId, out TInstance instance)) return;

                    try
                    {
                        messageHandler(instance, data);
                    }
                    catch
                    {
                        LogFirstFailure(data.InstanceId, instance, data);
                        throw;
                    }
                });
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

                MarshalApply(() =>
                {
                    if (!objectManager.TryGetObjectWithLogging(data.InstanceId, out TInstance instance)) return;

                    TValue value = null;
                    if (data.ValueId != 0 && !objectManager.TryGetObjectWithLogging(data.ValueId, out value)) return;

                    try
                    {
                        messageHandler(instance, value, data);
                    }
                    catch
                    {
                        LogFirstFailure(data.InstanceId, instance, data);
                        throw;
                    }
                });
            };
            messageBroker.Subscribe(payloadHandler);
            disposeFunctions.Add(() => messageBroker.Unsubscribe(payloadHandler));
        }

        /// <summary>
        /// Marshals a received apply onto the game-loop thread inside an <see cref="AllowedThread"/>
        /// scope, so the re-run vanilla setter does not race the game loop or re-trigger the patches.
        /// Resolve object ids INSIDE the supplied action so the lookups run in queue order with the
        /// marshaled AutoRegistry destroy — an apply for an already-destroyed object resolves nothing
        /// and is dropped.
        /// </summary>
        protected void MarshalApply(Action apply)
        {
            GameThread.RunSafe(() =>
            {
                using (new AllowedThread())
                {
                    apply();
                }
            }, context: typeof(THandler).Name);
        }

        // The game thread error only names the handler, so name the object once, the first time one of its applies fails
        private void LogFirstFailure(uint instanceId, TInstance instance, object message)
        {
            if (!failedInstanceIds.Add(instanceId)) return;

            objectManager.TryGetId(instance, out var stringId);
            Logger.Error("Failed to apply {Message} to {Type} {StringId} with handle {Handle}",
                message.GetType().Name, typeof(TInstance).Name, stringId, instanceId);
        }

        public void Dispose()
        {
            foreach (var disposeFn in disposeFunctions)
                disposeFn();
        }
    }
}
