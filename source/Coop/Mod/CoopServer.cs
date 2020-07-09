using System;
using System.IO;
using System.Linq;
using Coop.Mod.Managers;
using Coop.Mod.Persistence.World;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;

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

        /// <summary>
        ///     Object store shared with all connected clients. Set to an instance when the server
        ///     is started, otherwise null.
        /// </summary>
        [CanBeNull]
        public SharedRemoteStore SyncedObjectStore { get; private set; }

        [CanBeNull] public CoopServerRail Persistence { get; private set; }

        public static CoopServer Instance => m_Instance.Value;

        public Server Current { get; private set; }
        public ServerGameManager gameManager { get; private set; }

        public string StartServer()
        {
            if (Campaign.Current == null)
            {
                string msg = "Campaign is not loaded. Could not start server.";
                Logger.Debug(msg);
                return msg;
            }

            if (Current == null)
            {
                Server.EType eServerType = Server.EType.Threaded;
                Current = new Server(eServerType);

                SyncedObjectStore = new SharedRemoteStore();
                m_GameEnvironmentServer = new GameEnvironmentServer();
                Persistence = new CoopServerRail(Current, m_GameEnvironmentServer);

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

            return null;
        }

        public void ShutDownServer()
        {
            Current?.Stop();
            Persistence = null;
            SyncedObjectStore = null;
            m_NetManager?.Stop();
            m_NetManager = null;
            m_GameEnvironmentServer = null;
            Current = null;
        }

        public void StartGame(string saveName)
        {
            if(Main.DEBUG)
            {
                try
                {
                    LoadGameResult saveGameData = MBSaveLoad.LoadSaveGameData(saveName, Utilities.GetModulesNames());
                    MBGameManager.StartNewGame(CreateGameManager(saveGameData));
                }
                catch(IOException ex)
                {
                    Logger.Error("Save file not found: " + ex.Message);
                }
                
            }
        }

        public ServerGameManager CreateGameManager(LoadGameResult saveGameData = null)
        {
            if (saveGameData != null)
            {
                gameManager = CreateGameManager(saveGameData.LoadResult);
            }
            else
            {
                gameManager = new ServerGameManager();
            }
            return gameManager;
        }

        public ServerGameManager CreateGameManager(LoadResult loadResult = null)
        {
            if (loadResult != null)
            {
                gameManager = new ServerGameManager(loadResult);
            }
            else
            {
                gameManager = new ServerGameManager();
            }
            return gameManager;
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
            connection.OnServerSendingWorldData += m_GameEnvironmentServer.LockTimeControlStopped;
            connection.OnServerSendedWorldData += m_GameEnvironmentServer.UnlockTimeControl;
        }

        private void OnClientDisconnected(ConnectionServer connection, EDisconnectReason eReason)
        {
            connection.OnClientJoined -= Persistence.ClientJoined;
            connection.OnDisconnected -= Persistence.Disconnected;
            SyncedObjectStore?.RemoveConnection(connection);
        }
    }
}