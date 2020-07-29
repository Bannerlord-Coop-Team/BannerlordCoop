using System;
using System.Net;
using Common;
using LiteNetLib;
using Network;
using Network.Infrastructure;
using NLog;

namespace Coop.NetImpl.LiteNet
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
            m_NetManager?.PollEvents();
        }

        public void Reconnect()
        {
            if (m_Peer == null)
            {
                throw new Exception("Client was never connected. Cannot reconnect.");
            }

            if (Connected)
            {
                throw new Exception("Client is still connected. Cannot reconnect.");
            }

            Logger.Info("Client reconnecting...");
            Connect(m_Peer.EndPoint.Address, m_Peer.EndPoint.Port);
        }

        public string Connect(IPAddress address, int iPort)
        {
            string result = null;

            IPEndPoint toConnectTo = new IPEndPoint(address, iPort);
            if (Connected)
            {
                if (m_Peer.EndPoint.Equals(toConnectTo))
                {
                    string msg = "Client is already connected to the endpoint. Ignoring request.";
                    Logger.Debug(msg);
                    return msg;
                }

                Logger.Debug(result = "Switching servers.");
                Disconnect(EDisconnectReason.ClientJoinedAnotherServer);
            }

            m_NetManager = new NetManager(new LiteNetListenerClient(m_Session))
            {
                ReconnectDelay = 2000,
                MaxConnectAttempts = 20,
                DisconnectTimeout = (int) TimeSpan.FromSeconds(60).TotalMilliseconds
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

            return result;
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
