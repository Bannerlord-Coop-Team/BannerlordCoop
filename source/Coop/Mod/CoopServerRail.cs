using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.Persistence;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using RailgunNet.Connection.Server;

namespace Coop.Mod
{
    public class CoopServerRail : IUpdateable
    {
        private readonly EntityManager m_EntityManager;
        private readonly RailServer m_Instance;

        private readonly Dictionary<ConnectionServer, RailNetPeerWrapper> m_RailConnections =
            new Dictionary<ConnectionServer, RailNetPeerWrapper>();

        private readonly Server m_Server;

        public CoopServerRail(Server server, IEnvironmentServer environment)
        {
            m_Server = server;
            m_Instance = new RailServer(Registry.Server(environment));
            m_EntityManager = new EntityManager(m_Instance);
        }

        public void Update(TimeSpan frameTime)
        {
            m_Instance.Update();
        }

        ~CoopServerRail()
        {
            m_Server.Updateables.Remove(this);
        }

        public void ClientJoined(ConnectionServer connection)
        {
            RailNetPeerWrapper peer = connection.GameStatePersistence as RailNetPeerWrapper;
            m_RailConnections.Add(connection, peer);
            m_Instance.AddClient(peer, ""); // TODO: Name
        }

        public void Disconnected(ConnectionServer connection)
        {
            if (m_RailConnections.ContainsKey(connection))
            {
                m_Instance.RemoveClient(m_RailConnections[connection]);
            }
        }
    }
}
