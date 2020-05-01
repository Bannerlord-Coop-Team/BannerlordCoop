using System;
using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using RailgunNet;
using RailgunNet.Connection.Client;

namespace Coop.Game.Persistence
{
    public class PersistenceClient : IUpdateable
    {
        private readonly RailClient m_RailClient;

        public PersistenceClient(IEnvironment environment)
        {
            m_RailClient = new RailClient(Registry.Get(Component.Client, environment));
        }

        public void Update(TimeSpan frameTime)
        {
            m_RailClient.Update();
        }

        public void OnConnectionCreated(ConnectionClient connection)
        {
            connection.OnClientJoined += OnClientJoined;
            connection.OnDisconnected += OnDisconnected;
        }

        private void OnClientJoined(ConnectionClient connection)
        {
            m_RailClient.SetPeer((RailNetPeerWrapper) connection.GameStatePersistence);
        }

        private void OnDisconnected()
        {
            m_RailClient.SetPeer(null);
        }
    }
}
