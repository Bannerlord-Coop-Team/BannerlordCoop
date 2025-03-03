using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using GameInterface.Utils.NetworkEvents;
using HarmonyLib;
using Serilog;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;

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

        #region Handlers
        protected Action<MessagePayload<TMessage>> GetSetHandler<TValue, TMessage, TNetworkMessage>()
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkSetEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> handler = (payload) =>
            {
                var data = payload.What;

                if (!TryGetId(data.Instance, out string instanceId)) return;

                var networkMessage = (TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, data.Value });
                network.SendAll(networkMessage);
            };

            return handler;
        }

        protected Action<MessagePayload<TMessage>> GetReferenceSetHandler<TValue, TMessage, TNetworkMessage>()
            where TValue : class
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceSetEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> handler = (payload) =>
            {
                var data = payload.What;

                if (!TryGetId(data.Instance, out string instanceId)) return;
                if (!TryGetId(data.Value, out string valueId) && data.Value != null) return;

                var networkMessage = (TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, valueId });
                network.SendAll(networkMessage);
            };

            return handler;
        }

        protected Action<MessagePayload<TMessage>> GetArraySetHandler<TValue, TMessage, TNetworkMessage, TNetworkChangeMessage>()
            where TMessage : GenericEvent<TInstance, TValue[]>
            where TNetworkMessage : GenericNetworkArraySetEvent<TInstance, TValue>
            where TNetworkChangeMessage : GenericNetworkArrayChangedEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> handler = (payload) =>
            {
                var data = payload.What;

                if (!TryGetId(data.Instance, out string instanceId)) return;

                network.SendAll((TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, data.Value.Length }));

                for (int i = 0; i < data.Value.Length; i++)
                {
                    network.SendAll((TNetworkChangeMessage)Activator.CreateInstance(typeof(TNetworkChangeMessage), new object[] { instanceId, data.Value[i], i }));
                }
            };

            return handler;
        }

        protected Action<MessagePayload<TMessage>> GetArrayReferenceSetHandler<TValue, TMessage, TNetworkMessage, TNetworkChangeMessage>()
            where TMessage : GenericEvent<TInstance, TValue[]>
            where TNetworkMessage : GenericNetworkArraySetEvent<TInstance, TValue>
            where TNetworkChangeMessage : GenericNetworkReferenceArrayChangedEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> handler = (payload) =>
            {
                var data = payload.What;
                if (!TryGetId(data.Instance, out string instanceId)) return;

                network.SendAll((TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, data.Value.Length }));

                for (int i = 0; i < data.Value.Length; i++)
                {
                    if (!TryGetId(data.Value[i], out string valueId)) return;
                    network.SendAll((TNetworkChangeMessage)Activator.CreateInstance(typeof(TNetworkChangeMessage), new object[] { instanceId, valueId, i }));
                }
            };

            return handler;
        }

        protected Action<MessagePayload<TMessage>> GetArrayChangedHandler<TValue, TMessage, TNetworkMessage>()
            where TMessage : GenericArrayChangedEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkArrayChangedEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> handler = (payload) =>
            {
                var data = payload.What;

                if (!TryGetId(data.Instance, out string instanceId)) return;

                var networkMessage = (TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, data.Value, data.Index });
                network.SendAll(networkMessage);
            };

            return handler;
        }

        protected Action<MessagePayload<TMessage>> GetArrayReferenceChangedHandler<TValue, TMessage, TNetworkMessage>()
            where TMessage : GenericArrayChangedEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceArrayChangedEvent<TInstance, TValue>
        {
            Action<MessagePayload<TMessage>> handler = (payload) =>
            {
                var data = payload.What;

                if (!TryGetId(data.Instance, out string instanceId)) return;
                if (!TryGetId(data.Value, out string valueId) && data.Value != null) return;

                var networkMessage = (TNetworkMessage)Activator.CreateInstance(typeof(TNetworkMessage), new object[] { instanceId, valueId, data.Index });
                network.SendAll(networkMessage);
            };

            return handler;
        }

        #endregion

        #region Subscriptions

        protected void SubscribeSetHandler<TValue, TMessage, TNetworkMessage>(MemberInfo memberInfo)
            where TValue : class
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkSetEvent<TInstance, TValue>
        {
            Subscribe(GetSetHandler<TValue, TMessage, TNetworkMessage>());
            if (memberInfo is PropertyInfo propertyInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    propertyInfo.SetValue(instance, data.Value);
                };
                Subscribe(networkHandler);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    fieldInfo.SetValue(instance, data.Value);
                };
                Subscribe(networkHandler);
            }
            else
                Logger.Error($"Invalid MemberInfo passed for {typeof(TInstance)}; {typeof(TValue)}");
        }

        protected void SubscribeReferenceSetHandler<TValue, TMessage, TNetworkMessage>(MemberInfo memberInfo)
            where TValue : class
            where TMessage : GenericEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceSetEvent<TInstance, TValue>
        {
            Subscribe(GetReferenceSetHandler<TValue, TMessage, TNetworkMessage>());
            if (memberInfo is PropertyInfo propertyInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out TValue value) && string.IsNullOrEmpty(data.ValueId)) return;

                    propertyInfo.SetValue(instance, value);
                };
                Subscribe(networkHandler);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out TValue value) && string.IsNullOrEmpty(data.ValueId)) return;

                    fieldInfo.SetValue(instance, value);
                };
                Subscribe(networkHandler);
            }
            else
                Logger.Error($"Invalid MemberInfo passed for {typeof(TInstance)}; {typeof(TValue)}");
        }

        protected void SubscribeArraySetHandler<TValue, TMessage, TNetworkMessage, TChangedNetworkMessage>(MemberInfo memberInfo)
            where TMessage : GenericEvent<TInstance, TValue[]>
            where TNetworkMessage : GenericNetworkArraySetEvent<TInstance, TValue>
            where TChangedNetworkMessage : GenericNetworkArrayChangedEvent<TInstance, TValue>
        {
            Subscribe(GetArraySetHandler<TValue, TMessage, TNetworkMessage, TChangedNetworkMessage>());
            if (memberInfo is PropertyInfo propertyInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    propertyInfo.SetValue(instance, new TValue[data.Length]);
                };
                Subscribe(networkHandler);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    fieldInfo.SetValue(instance, new TValue[data.Length]);
                };
                Subscribe(networkHandler);
            }
            else
                Logger.Error($"Invalid MemberInfo passed for {typeof(TInstance)}; {typeof(TValue)}");
        }

        protected void SubscribeArrayReferenceSetHandler<TValue, TMessage, TNetworkMessage, TChangedNetworkMessage>(MemberInfo memberInfo)
            where TMessage : GenericEvent<TInstance, TValue[]>
            where TNetworkMessage : GenericNetworkArraySetEvent<TInstance, TValue>
            where TChangedNetworkMessage : GenericNetworkReferenceArrayChangedEvent<TInstance, TValue>
        {
            Subscribe(GetArrayReferenceSetHandler<TValue, TMessage, TNetworkMessage, TChangedNetworkMessage>());
            if (memberInfo is PropertyInfo propertyInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    propertyInfo.SetValue(instance, new TValue[data.Length]);
                };
                Subscribe(networkHandler);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    fieldInfo.SetValue(instance, new TValue[data.Length]);
                };
                Subscribe(networkHandler);
            }
            else
                Logger.Error($"Invalid MemberInfo passed for {typeof(TInstance)}; {typeof(TValue)}");
        }

        protected void SubscribeArrayChangedHandler<TValue, TMessage, TNetworkMessage>(MemberInfo memberInfo)
            where TMessage : GenericArrayChangedEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkArrayChangedEvent<TInstance, TValue>
        {
            Subscribe(GetArrayChangedHandler<TValue, TMessage, TNetworkMessage>());
            if (memberInfo is PropertyInfo propertyInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    (propertyInfo.GetValue(instance) as TValue[])[data.Index] = data.Value;
                };
                Subscribe(networkHandler);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;

                    (fieldInfo.GetValue(instance) as TValue[])[data.Index] = data.Value;
                };
                Subscribe(networkHandler);
            }
            else
                Logger.Error($"Invalid MemberInfo passed for {typeof(TInstance)}; {typeof(TValue)}");
        }

        protected void SubscribeArrayReferenceChangedHandler<TValue, TMessage, TNetworkMessage>(MemberInfo memberInfo)
            where TValue : class
            where TMessage : GenericArrayChangedEvent<TInstance, TValue>
            where TNetworkMessage : GenericNetworkReferenceArrayChangedEvent<TInstance, TValue>
        {
            Subscribe(GetArrayReferenceChangedHandler<TValue, TMessage, TNetworkMessage>());
            if (memberInfo is PropertyInfo propertyInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out TValue value) && string.IsNullOrEmpty(data.ValueId)) return;

                    (propertyInfo.GetValue(instance) as TValue[])[data.Index] = value;
                };
                Subscribe(networkHandler);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                Action<MessagePayload<TNetworkMessage>> networkHandler = (payload) =>
                {
                    var data = payload.What;

                    if (!objectManager.TryGetObject(data.InstanceId, out TInstance instance)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out TValue value) && string.IsNullOrEmpty(data.ValueId)) return;

                    (fieldInfo.GetValue(instance) as TValue[])[data.Index] = value;
                };
                Subscribe(networkHandler);
            }
            else
                Logger.Error($"Invalid MemberInfo passed for {typeof(TInstance)}; {typeof(TValue)}");
        }

        protected void SubsribeArrayHandler<TValue, TSetMessage, TSetNetworkMessage, TChangedMessage, TChangedNetworkMessage>(MemberInfo memberInfo)
            where TSetMessage : GenericEvent<TInstance, TValue[]>
            where TSetNetworkMessage : GenericNetworkArraySetEvent<TInstance, TValue>
            where TChangedMessage : GenericArrayChangedEvent<TInstance, TValue>
            where TChangedNetworkMessage : GenericNetworkArrayChangedEvent<TInstance, TValue>
        {
            SubscribeArraySetHandler<TValue, TSetMessage, TSetNetworkMessage, TChangedNetworkMessage>(memberInfo);
            SubscribeArrayChangedHandler<TValue, TChangedMessage, TChangedNetworkMessage>(memberInfo);
        }

        protected void SubsribeArrayReferenceHandler<TValue, TSetMessage, TSetNetworkMessage, TChangedMessage, TChangedNetworkMessage>(MemberInfo memberInfo)
            where TValue : class
            where TSetMessage : GenericEvent<TInstance, TValue[]>
            where TSetNetworkMessage : GenericNetworkArraySetEvent<TInstance, TValue>
            where TChangedMessage : GenericArrayChangedEvent<TInstance, TValue>
            where TChangedNetworkMessage : GenericNetworkReferenceArrayChangedEvent<TInstance, TValue>
        {
            SubscribeArrayReferenceSetHandler<TValue, TSetMessage, TSetNetworkMessage, TChangedNetworkMessage>(memberInfo);
            SubscribeArrayReferenceChangedHandler<TValue, TChangedMessage, TChangedNetworkMessage>(memberInfo);
        }

        #endregion

        #region Helpers

        protected void Subscribe<TMessage>(Action<MessagePayload<TMessage>> handler)
            where TMessage : IMessage
        {
            messageBroker.Subscribe(handler);
            handlers.Add(handler);
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
        #endregion
    }
}
