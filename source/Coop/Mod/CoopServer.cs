using System;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;
using Sync.Store;

namespace Coop.Mod
{
    public class CoopServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopServer> m_Instance =
            new Lazy<CoopServer>(() => new CoopServer());

        private LiteNetManagerServer m_NetManager;

        private CoopServer()
        {
        }

        /// <summary>
        ///     Object store shared with all connected clients. Set to an instance when the server
        ///     is started, otherwise null.
        /// </summary>
        [CanBeNull]
        public SharedRemoteStore SyncedObjectStore { get; private set; }

        [CanBeNull] public CoopServerRail Persistence { get; private set; }

        public static CoopServer Instance => m_Instance.Value;

        public Server Current { get; private set; }

        public void StartServer()
        {
            if (Current == null)
            {
                Server.EType eServerType = Server.EType.Threaded;
                Current = new Server(eServerType);

                SyncedObjectStore = new SharedRemoteStore();
                Persistence = new CoopServerRail(Current, new GameEnvironmentServer());
                Current.Updateables.Add(Persistence);
                Current.OnClientConnected += OnClientConnected;
                Current.OnClientDisconnected += OnClientDisconnected;

                if (eServerType == Server.EType.Direct)
                {
                    Main.Instance.Updateables.Add(Current);
                }

                Current.Start(new ServerConfiguration());
                Logger.Debug("Created server.");
            }

            if (m_NetManager == null)
            {
                m_NetManager = new LiteNetManagerServer(Current, new GameData());
                m_NetManager.StartListening();
                Logger.Debug("Setup network connection for server.");
            }
        }

        public void ShutDownServer()
        {
            Current?.Stop();
            Persistence = null;
            SyncedObjectStore = null;
            m_NetManager?.Stop();
            m_NetManager = null;
            Current = null;
        }

        public override string ToString()
        {
            if (Current == null)
            {
                return "Server not running.";
            }

            return Current.ToString();
        }

        private void OnClientConnected(ConnectionServer connection)
        {
            SyncedObjectStore.AddConnection(connection);
            connection.OnClientJoined += Persistence.ClientJoined;
            connection.OnDisconnected += Persistence.Disconnected;
        }

        private void OnClientDisconnected(ConnectionServer connection, EDisconnectReason eReason)
        {
            connection.OnClientJoined -= Persistence.ClientJoined;
            connection.OnDisconnected -= Persistence.Disconnected;
            SyncedObjectStore?.RemoveConnection(connection);
        }
    }
}
