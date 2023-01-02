using Common;
using Common.Serialization;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using Serilog;

namespace Missions.Network
{
    public class NetworkMessageBroker : INetworkMessageBroker, IPacketHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NetworkMessageBroker>();

        private readonly Dictionary<Type, List<Delegate>> m_Subscribers;
        private readonly LiteNetP2PClient m_Client;

        public PacketType PacketType => PacketType.Event;

        public NetworkMessageBroker(LiteNetP2PClient client)
        {
            m_Client = client;
            m_Subscribers = new Dictionary<Type, List<Delegate>>();

            m_Client.AddHandler(this);
        }

        public virtual void Publish<T>(T message, NetPeer peer = null)
        {
            if (message == null)
                return;

            var payload = new MessagePayload<T>(message, string.Empty);

            EventPacket messagePacket = new EventPacket(payload);

            if (peer != null)
            {
                m_Client.Send(messagePacket, peer);
            }
            else
            {
                m_Client.SendAll(messagePacket);
            }
        }

        public void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            var delegates = m_Subscribers.ContainsKey(typeof(T)) ?
                            m_Subscribers[typeof(T)] : new List<Delegate>();
            if (!delegates.Contains(subscription))
            {
                Logger.Verbose("Started listening for {PacketType}", typeof(T).Name);
                delegates.Add(subscription);
            }
            m_Subscribers[typeof(T)] = delegates;
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            if (!m_Subscribers.ContainsKey(typeof(T))) return;
            Logger.Verbose("Stopped listening for {PacketType}", typeof(T).Name);
            var delegates = m_Subscribers[typeof(T)];
            if (delegates.Contains(subscription))
                delegates.Remove(subscription);
            if (delegates.Count == 0)
                m_Subscribers.Remove(typeof(T));
        }

        public void Dispose()
        {
            m_Subscribers?.Clear();
        }

        public virtual void HandlePacket(NetPeer peer, IPacket packet)
        {
            Logger.Verbose("Received message {Packet} from {Peer}", packet, peer.EndPoint);
            object payload = ProtoBufSerializer.Deserialize(packet.Data);

            Type type = payload.GetType();
            if (type.GetGenericTypeDefinition() != typeof(MessagePayload<>))
            {
                throw new InvalidCastException($"{payload.GetType()} is not of type {typeof(MessagePayload<>)}");
            }

            type.GetProperty("Who").SetValue(payload, peer);

            Type T = type.GetProperty("What").PropertyType;

            if (!m_Subscribers.ContainsKey(T))
            {
                return;
            }

            var delegates = m_Subscribers[T];
            if (delegates == null || delegates.Count == 0) return;

            Logger.Debug("Received {PacketType} from {Peer}: {Payload}", type.Name, peer.EndPoint, payload);

            foreach (var handler in delegates)
            {
                Task.Factory.StartNew(() => handler.Method.Invoke(handler.Target, new object[] { payload }));
            }
        }

        public virtual void Publish<T>(T message)
        {
            Logger.Debug("Publishing {PacketType} : {Payload}", typeof(T).Name, message);
            Publish(message, null);
        }

        public virtual void HandlePeerDisconnect(NetPeer peer, DisconnectInfo reason)
        {

        }

        public void Publish<T>(object source, T message)
        {
        }
    }
}
