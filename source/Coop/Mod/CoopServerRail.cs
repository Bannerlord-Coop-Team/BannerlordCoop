using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.Persistence;
using Coop.Multiplayer.Network;
using JetBrains.Annotations;
using Network.Infrastructure;
using RailgunNet.Connection.Server;

namespace Coop.Mod
{
    public class CoopServerRail : IUpdateable
    {
        private readonly RailServer m_Instance;

        private readonly Dictionary<ConnectionServer, RailNetPeerWrapper> m_RailConnections =
            new Dictionary<ConnectionServer, RailNetPeerWrapper>();

        private readonly Server m_Server;

        public CoopServerRail(Server server, IEnvironmentServer environment)
        {
            m_Server = server;
            m_Instance = new RailServer(Registry.Server(environment));
            EntityManager = new EntityManager(m_Instance);
        }

        [NotNull] public EntityManager EntityManager { get; }

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
