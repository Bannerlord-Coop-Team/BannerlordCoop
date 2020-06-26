using System;
using System.Linq;
using Coop.Mod.Persistence.World;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;

namespace Coop.Mod
{
    public class CoopServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopServer> m_Instance =
            new Lazy<CoopServer>(() => new CoopServer());

        private LiteNetManagerServer m_NetManager;

        private GameEnvironmentServer m_GameEnvironmentServer;

        private CoopServer()
        {
        }

        [CanBeNull] public CoopServerRail Persistence { get; private set; }

        public static CoopServer Instance => m_Instance.Value;

        public Server Current { get; private set; }

        public void StartServer()
        {
            if (Current == null)
            {
                Server.EType eServerType = Server.EType.Threaded;
                Current = new Server(eServerType);

                m_GameEnvironmentServer = new GameEnvironmentServer();
                Persistence = new CoopServerRail(Current, m_GameEnvironmentServer);
                Current.Updateables.Add(Persistence);
                Current.OnClientConnected += OnClientConnected;

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
            m_NetManager?.Stop();
            m_NetManager = null;
            m_GameEnvironmentServer = null;
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
            connection.OnClientJoined += Persistence.ClientJoined;
            connection.OnDisconnected += Persistence.Disconnected;
            connection.OnServerSendingWorldData += m_GameEnvironmentServer.LockTimeControlStopped;
            connection.OnServerSendedWorldData += m_GameEnvironmentServer.UnlockTimeControl;
        }
    }
}
