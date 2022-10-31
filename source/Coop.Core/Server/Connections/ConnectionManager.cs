using Common.Messaging;
using LiteNetLib;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections
{

    public readonly struct ClientConnected
    {
        public NetPeer Peer { get; }

        public ClientConnected(NetPeer peer)
        {
            Peer = peer;
        }
    }

    public readonly struct ClientDisconnected
    {
        public NetPeer Peer { get; }
        public DisconnectInfo DisconnectInfo { get; }

        public ClientDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {

            Peer = peer;
            DisconnectInfo = disconnectInfo;
        }
    }

    internal interface IConnectionManager
    {
    }

    internal class ConnectionManager : IConnectionManager
    {
        private readonly IMessageBroker _messageBroker;

        private readonly List<IConnection> _connections = new List<IConnection>();

        public ConnectionManager(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;

            _messageBroker.Subscribe<ClientConnected>(Handle_Connect);
            _messageBroker.Subscribe<ClientDisconnected>(Handle_Disconnect);
        }

        private void Handle_Connect(MessagePayload<ClientConnected> payload)
        {
            NetPeer peer = payload.What.Peer;
            _connections.Add(new Connection(peer));
        }

        private void Handle_Disconnect(MessagePayload<ClientDisconnected> payload)
        {
            NetPeer peer = payload.What.Peer;
            IConnection connection = _connections.Find(con => con.Peer == peer);

            if (connection != null)
            {
                _connections.Remove(connection);
                connection.Dispose();
            }
        }
    }
}
