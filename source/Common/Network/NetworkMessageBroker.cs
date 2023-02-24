using Common.Messaging;
using Common.PacketHandlers;
using LiteNetLib;
using System;

namespace Common.Network
{
    /// <summary>
    /// MessageBroker implementation allowing for network messaging capability
    /// </summary>
    public interface INetworkMessageBroker : IMessageBroker
    {

        /// <summary>
        /// Publishes events to all connected peers
        /// </summary>
        /// <param name="networkEvent">Event to publish</param>
        void PublishNetworkEvent(INetworkEvent networkEvent);

        /// <summary>
        /// Publishes event to specified peer
        /// </summary>
        /// <param name="networkEvent">Event to publish</param>
        /// <param name="peer">Peer to send event</param>
        void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent);
    }

    /// <inheritdoc cref="INetworkMessageBroker"/>
    public class NetworkMessageBroker : MessageBroker, INetworkMessageBroker
    {
        /// <summary>
        /// Auto-wired dependency
        /// </summary>
        public INetwork Network { get; set; }
        public NetworkMessageBroker()
        {
            _instance = this;
        }

        public void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            Network.Send(peer, eventPacket);
        }

        public void PublishNetworkEvent(INetworkEvent networkEvent)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            Network.SendAll(eventPacket);
        }

        public override void Publish<T>(object source, T message)
        {
            base.Publish(source, message);
        }

        public override void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            base.Subscribe(subscription);
        }

        public override void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            base.Unsubscribe(subscription);
        }
    }
}
