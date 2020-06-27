using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common;
using Coop.Mod.Persistence;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;
using RailgunNet.Logic;
using Sync.Store;

namespace Coop.Mod
{
    public class CoopClient : IUpdateable
    {
        private const int MaxReconnectAttempts = 2;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient());

        [NotNull] private readonly LiteNetManagerClient m_NetManager;

        /// <summary>
        ///     Internal data storage for <see cref="SyncedObjectStore" />.
        /// </summary>
        private readonly Dictionary<ObjectId, object> m_SyncedObjects =
            new Dictionary<ObjectId, object>();

        private int m_ReconnectAttempts = MaxReconnectAttempts;

        public Action<PersistenceClient> OnPersistenceInitialized;

        public CoopClient()
        {
            Session = new GameSession(new GameData());
            Session.OnConnectionDestroyed += ConnectionDestroyed;
            m_NetManager = new LiteNetManagerClient(Session);
            GameState = new CoopGameState();
            Events = new CoopEvents();
            Events.OnGameLoaded.AddNonSerializedListener(this, Init);
        }

        /// <summary>
        ///     Object store shared with the server if connected. Otherwise null.
        /// </summary>
        [CanBeNull]
        public RemoteStore SyncedObjectStore { get; private set; }

        [CanBeNull] public PersistenceClient Persistence { get; private set; }

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
            Persistence?.Update(frameTime);
        }

        public string Connect(IPAddress ip, int iPort)
        {
            return m_NetManager.Connect(ip, iPort);
        }

        public void Disconnect()
        {
            m_NetManager.Disconnect(EDisconnectReason.ClientLeft);
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

            if (Persistence == null)
            {
                Persistence = new PersistenceClient(new GameEnvironmentClient());
                OnPersistenceInitialized?.Invoke(Persistence);
            }

            Persistence.SetConnection(con);
        }

        private void ConnectionCreated(ConnectionClient con)
        {
            if (con == null)
            {
                throw new ArgumentNullException(nameof(con));
            }

            m_ReconnectAttempts = MaxReconnectAttempts;
            TryInitPersistence(con);
            SyncedObjectStore = new RemoteStore(m_SyncedObjects, con);
            con.OnClientJoined += TryInitPersistence;
            con.OnDisconnected += ConnectionClosed;
        }

        private void ConnectionClosed(EDisconnectReason eReason)
        {
            Persistence?.SetConnection(null);
            SyncedObjectStore = null;
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

            string sLeadingWhitespace = "       ";
            string sRet =
                $"{Session.Connection.Latency,-5}{Session.Connection.State,-30}{Session.Connection.Network}";
            sRet += Environment.NewLine + sLeadingWhitespace;
            if (Persistence != null)
            {
                IEnumerable<RailEntityBase> controlledEntity = Persistence.Room.LocalEntities;
                sRet += $"Controlling {controlledEntity.Count()} entities.";
                foreach (RailEntityBase entity in controlledEntity)
                {
                    sRet += Environment.NewLine + sLeadingWhitespace + entity;
                }
            }

            return sRet;
        }
    }
}
