using Coop.Common;
using Coop.Network;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer.Network
{
    public class LiteNetManagerClient : IUpdateable
    {
        private readonly GameSession m_Session;
        private readonly LiteNetListenerClient m_Listener;
        private readonly NetManager m_NetManager;
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
        public void Connect(string sAddress, int iPort)
        {
            if(!Connected)
            {
                if (m_NetManager.Start())
                {
                    m_Peer = m_NetManager.Connect(sAddress, iPort, "");
                }
                else
                {
                    throw new NetworkConnectionFailedException($"Could not connect to {sAddress}:{iPort}.");
                }
            }
        }
        public void Disconnect(EDisconnectReason eReason)
        {
            if(Connected)
            {
                m_NetManager.DisconnectPeer(m_Peer, new byte[] { Convert.ToByte(eReason) });
                m_NetManager.Stop();
                m_Session.Disconnect(eReason);
            }
        }
        public bool Connected
        {
            get
            {
                return m_Peer != null ? m_Peer.ConnectionState.HasFlag(ConnectionState.Connected) : false;
            }
        }
        public void Update(TimeSpan frameTime)
        {
            if(Connected)
            {
                m_NetManager.PollEvents();
            }
        }
    }
}
