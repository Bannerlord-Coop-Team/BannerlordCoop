using System;
using System.Net;
using Coop.Common;
using Coop.Network;
using LiteNetLib;
using NLog;

namespace Coop.Multiplayer.Network
{
    public class LiteNetManagerClient : IUpdateable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly GameSession m_Session;
        private NetManager m_NetManager;
        private NetPeer m_Peer;

        public LiteNetManagerClient(GameSession session)
        {
            m_Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool Connected =>
            m_Peer != null && m_Peer.ConnectionState.HasFlag(ConnectionState.Connected);

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
                    Logger.Debug("Client is already connected to the endpoint. Ignoring request.");
                    return;
                }

                Logger.Debug("Switching servers.");
                Disconnect(EDisconnectReason.ClientJoinedAnotherServer);
            }

            m_NetManager = new NetManager(new LiteNetListenerClient(m_Session))
            {
                ReconnectDelay = 500,
                MaxConnectAttempts = 10
            };

            if (m_NetManager.Start())
            {
                m_Peer = m_NetManager.Connect(address.ToString(), iPort, "");
            }
            else
            {
                throw new NetworkConnectionFailedException(
                    $"Could not connect to {address}:{iPort}.");
            }
        }

        public void Disconnect(EDisconnectReason eReason)
        {
            if (Connected)
            {
                m_NetManager.DisconnectPeer(m_Peer, new[] {Convert.ToByte(eReason)});
                m_NetManager.Stop();
                m_Session.Disconnect(eReason);
                m_NetManager = null;
            }
        }
    }
}
