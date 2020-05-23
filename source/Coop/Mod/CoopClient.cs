using System;
using System.Net;
using Coop.Common;
using Coop.Mod.Persistence;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using JetBrains.Annotations;
using NLog;

namespace Coop.Mod
{
    public class CoopClient : IUpdateable
    {
        private const int MaxReconnectAttempts = 2;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient());

        [NotNull] private readonly LiteNetManagerClient m_NetManager;
        [CanBeNull] private PersistenceClient m_Persistence;
        private int m_ReconnectAttempts = MaxReconnectAttempts;

        public CoopClient()
        {
            Session = new GameSession(new GameData());
            Session.OnConnectionDestroyed += ConnectionDestroyed;
            m_NetManager = new LiteNetManagerClient(Session);
            GameState = new CoopGameState();
            Events = new CoopEvents();
            Events.OnGameLoaded.AddNonSerializedListener(this, Init);
        }

        [NotNull] public GameSession Session { get; }

        public static CoopClient Instance => m_Instance.Value;

        public CoopGameState GameState { get; }
        public CoopEvents Events { get; }

        public bool Connected
        {
            get
            {
                if (Session.Connection == null)
                {
                    return false;
                }

                return Session.Connection.State == EConnectionState.ClientPlaying;
            }
        }

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
            Session.OnConnectionCreated += ConnectionCreated;
            if (Session.Connection != null)
            {
                ConnectionCreated(Session.Connection);
            }
        }

        private void TryInitPersistence(ConnectionClient con)
        {
            if (con == null || con.State != EConnectionState.ClientPlaying) return;

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

            m_ReconnectAttempts = MaxReconnectAttempts;
            TryInitPersistence(con);
            con.OnClientJoined += TryInitPersistence;
            con.OnDisconnected += ConnectionClosed;
        }

        private void ConnectionClosed(EDisconnectReason eReason)
        {
            m_Persistence?.SetConnection(null);
        }

        private void ConnectionDestroyed(EDisconnectReason eReason)
        {
            switch (eReason)
            {
                case EDisconnectReason.Timeout:
                case EDisconnectReason.Unknown:
                    TryReconnect();
                    break;
            }
        }

        private void TryReconnect()
        {
            if (m_ReconnectAttempts > 0)
            {
                Logger.Info(
                    "Reconnect attempt [{currentAttempt}/{max}].",
                    m_ReconnectAttempts,
                    MaxReconnectAttempts);
                --m_ReconnectAttempts;
                m_NetManager.Reconnect();
            }
        }

        public override string ToString()
        {
            if (Session.Connection == null)
            {
                return "Client not connected.";
            }

            return $"{Session.Connection}";
        }
    }
}
