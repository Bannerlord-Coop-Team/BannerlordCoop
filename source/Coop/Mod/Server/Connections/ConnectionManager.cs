using Coop.Communication.MessageBroker;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Server.Connections
{
    public readonly struct ClientConnectedMessage
    {
        public NetPeer Peer { get; }

        public ClientConnectedMessage(NetPeer peer)
        {
            Peer = peer;
        }
    }

    public readonly struct ClientDisconnectedMessage
    {
        public NetPeer Peer { get; }
        public DisconnectInfo DisconnectInfo { get; }

        public ClientDisconnectedMessage(NetPeer peer, DisconnectInfo disconnectInfo)
        {

            Peer = peer;
            DisconnectInfo = disconnectInfo;
        }
    }
    internal class ConnectionManager : IConnectionManager
    {
        private readonly IMessageBroker _messageBroker;

        private readonly List<IConnection> _connections = new List<IConnection>();

        public ConnectionManager(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;

            _messageBroker.Subscribe<ClientConnectedMessage>(Handle_Connect);
            _messageBroker.Subscribe<ClientDisconnectedMessage>(Handle_Disconnect);
        }

        private void Handle_Connect(MessagePayload<ClientConnectedMessage> payload)
        {
            NetPeer peer = payload.What.Peer;
            _connections.Add(new Connection(peer));
        }

        private void Handle_Disconnect(MessagePayload<ClientDisconnectedMessage> payload)
        {
            NetPeer peer = payload.What.Peer;
            IConnection connection = _connections.Find(con => con.Peer == peer);

            if(connection != null)
            {
                _connections.Remove(connection);
                connection.Dispose();
            }
        }
    }
}
