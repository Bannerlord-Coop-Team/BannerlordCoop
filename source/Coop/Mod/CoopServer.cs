using System;
using Coop.Mod.Managers;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using NLog;
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
        private CoopServerRail m_RailServer;

        private CoopServer()
        {
        }

        public static CoopServer Instance => m_Instance.Value;

        public Server Current { get; private set; }

        public void StartServer()
        {
            if (Current == null)
            {
                Server.EType eServerType = Server.EType.Direct;
                Current = new Server(eServerType);

                m_RailServer = new CoopServerRail(Current, new GameEnvironmentServer());
                Current.Updateables.Add(m_RailServer);
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
                m_NetManager = new LiteNetManagerServer(Current, new SaveData());
                m_NetManager.StartListening();
                Logger.Debug("Setup network connection for server.");
            }
        }

        public void ShutDownServer()
        {
            Current?.Stop();
            m_NetManager?.Stop();
            m_NetManager = null;
            Current = null;
        }

        public void StartGame()
        {
            // TODO: Relies on hardcoded save game file being present.
            LoadGameResult saveGameData = MBSaveLoad.LoadSaveGameData("MP", Utilities.GetModulesNames());
            MBGameManager.StartNewGame(CreateGameManager(saveGameData));
        }

        public ServerGameManager CreateGameManager(LoadGameResult saveGameData = null)
        {
            ServerGameManager gameManager;
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
            ServerGameManager gameManager;
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
            connection.OnClientJoined += m_RailServer.ClientJoined;
            connection.OnDisconnected += m_RailServer.Disconnected;
        }
    }
}
