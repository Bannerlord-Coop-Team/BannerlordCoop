using System;
using System.Net;
using Coop.Common;
using Coop.Mod.Persistence;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using JetBrains.Annotations;

namespace Coop.Mod
{
    public class CoopClient : IUpdateable
    {
        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient());

        [NotNull] private readonly LiteNetManagerClient m_NetManager;
        [NotNull] private readonly GameSession m_Session;
        [CanBeNull] private PersistenceClient m_Persistence;

        private CoopClient()
        {
            m_Session = new GameSession(new GameData());
            m_NetManager = new LiteNetManagerClient(m_Session);
            GameState = new CoopGameState();
            Events = new CoopEvents();
            Events.OnGameLoaded.AddNonSerializedListener(this, Init);
        }

        public static CoopClient Instance => m_Instance.Value;

        public CoopGameState GameState { get; }
        public CoopEvents Events { get; }
        public bool Connected => m_NetManager.Connected;

        public void Update(TimeSpan frameTime)
        {
            m_NetManager.Update(frameTime);
            m_Persistence?.Update(frameTime);
        }

        public void Connect(IPAddress ip, int iPort)
        {
            m_NetManager.Connect(ip, iPort);
        }

        private void Init()
        {
            m_Session.OnConnectionCreated += ConnectionCreated;
            if (m_Session.Connection != null)
            {
                ConnectionCreated(m_Session.Connection);
            }
        }

        private void TryInitPersistence(ConnectionClient con)
        {
            if (con == null || con.State != EConnectionState.ClientConnected) return;

            if (m_Persistence == null)
            {
                m_Persistence = new PersistenceClient(new GameEnvironmentClient());
            }

            m_Persistence.SetConnection(con);
        }

        private void ConnectionCreated(ConnectionClient con)
        {
            if (con == null)
            {
                throw new ArgumentNullException(nameof(con));
            }

            TryInitPersistence(con);
            con.OnClientJoined += TryInitPersistence;
            con.OnDisconnect += Disconnect;
        }

        private void Disconnect()
        {
            m_Persistence?.SetConnection(null);
        }

        public override string ToString()
        {
            if (m_Session.Connection == null)
            {
                return "Client not connected.";
            }

            return $"{m_Session.Connection}";
        }
    }
}
