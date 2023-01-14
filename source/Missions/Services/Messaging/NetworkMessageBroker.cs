using Common.Messaging;
using Common.Serialization;
using LiteNetLib.Utils;
using LiteNetLib;
using Missions.Services.Network.PacketHandlers;
using Missions.Services.Network;
using System;
using Common;

namespace Missions.Services.Messaging
{
    internal interface INetworkMessageBroker : IMessageBroker
    {
        void PublishAllEvent(INetworkEvent networkEvent);
        void PublishEvent(INetworkEvent networkEvent, NetPeer peer);
    }

    internal class NetworkMessageBroker : MessageBroker, INetworkMessageBroker
    {
        private readonly INetworkClient _client;

        // TODO resolve in patches using DI framework
        public static new NetworkMessageBroker Instance { get; private set; }

        public NetworkMessageBroker(INetworkClient client)
        {
            _client = client;
            Instance = this;
        }

        public void PublishEvent(INetworkEvent networkEvent, NetPeer peer)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            _client.Send(eventPacket, peer);
        }

        public void PublishAllEvent(INetworkEvent networkEvent)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            _client.SendAll(eventPacket);
        }

        public override void Publish<T>(object source, T message)
        {
            // TODO fix after DI is done
            MessageBroker.Instance.Publish(source, message);
        }

        public override void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            // TODO fix after DI is done
            MessageBroker.Instance.Subscribe(subscription);
        }

        public override void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            // TODO fix after DI is done
            MessageBroker.Instance.Unsubscribe(subscription);
        }
    }
}
