using System;
using System.Net;
using Coop.Common;
using Coop.Network;
using LiteNetLib;

namespace Coop.Multiplayer.Network
{
    public class LiteNetManagerClient : IUpdateable
    {
        private readonly LiteNetListenerClient m_Listener;
        private readonly NetManager m_NetManager;
        private readonly GameSession m_Session;
        private NetPeer m_Peer;

        public LiteNetManagerClient(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session may not be null.");
            }

            m_Session = session;
            m_Listener = new LiteNetListenerClient(session);
            m_NetManager = new NetManager(m_Listener);
            m_NetManager.ReconnectDelay = 100;
            m_NetManager.MaxConnectAttempts = 5;
        }

        public bool Connected =>
            m_Peer != null ? m_Peer.ConnectionState.HasFlag(ConnectionState.Connected) : false;

        public void Update(TimeSpan frameTime)
        {
            if (Connected)
            {
                m_NetManager.PollEvents();
            }
        }

        public void Connect(IPAddress address, int iPort)
        {
            IPEndPoint toConnectTo = new IPEndPoint(address, iPort);
            if (Connected)
            {
                if (m_Peer.EndPoint.Equals(toConnectTo))
                {
                    Log.Debug($"Client is already connected to the endpoint. Ignoring request.");
                    return;
                }
                Log.Debug("Switching servers.");
                Disconnect(EDisconnectReason.ClientJoinedAnotherServer);
            }

            if (m_NetManager.Start())
            {
                m_Peer = m_NetManager.Connect(address.ToString(), iPort, "");
            }
            else
            {
                throw new NetworkConnectionFailedException(
                    $"Could not connect to {address.ToString()}:{iPort}.");
            }
        }

        public void Disconnect(EDisconnectReason eReason)
        {
            if (Connected)
            {
                m_NetManager.DisconnectPeer(m_Peer, new[] {Convert.ToByte(eReason)});
                m_NetManager.Stop();
                m_Session.Disconnect(eReason);
            }
        }
    }
}
