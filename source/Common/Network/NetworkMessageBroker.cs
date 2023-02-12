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
        /// Publish an <see cref="INetworkEvent"/> to all connected <see cref="NetPeer"/>s
        /// </summary>
        /// <param name="networkEvent">Event to send over the network</param>
        /// <remarks>
        /// This does not publish internally.
        /// </remarks>
        void PublishNetworkEvent(INetworkEvent networkEvent);

        /// <summary>
        /// Publish an <see cref="INetworkEvent"/> to the given <see cref="NetPeer"/>
        /// </summary>
        /// <param name="peer">Peer to send event</param>
        /// <param name="networkEvent">Event to send over the network</param>
        /// <remarks>
        /// This does not publish internally.
        /// </remarks>
        void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent);
    }

    /// <inheritdoc cref="INetworkMessageBroker"/>
    public class NetworkMessageBroker : MessageBroker, INetworkMessageBroker
    {
        /// <summary>
        /// Auto-wired dependency
        /// </summary>
        public INetwork Network { get; set; }
        public static new NetworkMessageBroker Instance => _instance as NetworkMessageBroker;
        public NetworkMessageBroker()
        {
            _instance = this;
        }

        /// <inheritdoc/>
        public void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            Network.Send(peer, eventPacket);
        }

        /// <inheritdoc/>
        public void PublishNetworkEvent(INetworkEvent networkEvent)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            Network.SendAll(eventPacket);
        }

        /// <inheritdoc/>
        public override void Publish<T>(object source, T message)
        {
            base.Publish(source, message);
        }

        /// <inheritdoc/>
        public override void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            base.Subscribe(subscription);
        }

        /// <inheritdoc/>
        public override void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            base.Unsubscribe(subscription);
        }
    }
}
