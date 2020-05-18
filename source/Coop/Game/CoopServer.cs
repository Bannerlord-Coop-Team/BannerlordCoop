using System;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using NLog;
using SandBox;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Game
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
                Current = new Server(Server.EType.Direct);
                m_RailServer = new CoopServerRail(Current, new GameEnvironmentServer());
                Current.Updateables.Add(m_RailServer);
                Current.OnClientConnected += OnClientConnected;
                Main.Instance.Updateables.Add(Current);
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

        public void LoadGame()
        {
            LoadGameResult saveGameData = MBSaveLoad.LoadSaveGameData("MP", Utilities.GetModulesNames());
            MBGameManager.StartNewGame(CreateGameManager(saveGameData));
        }

        public CampaignGameManager CreateGameManager(LoadGameResult saveGameData = null)
        {
            CampaignGameManager gameManager;
            if (saveGameData != null)
            {
                gameManager = CreateGameManager(saveGameData.LoadResult);
            }
            else
            {
                gameManager = new CampaignGameManager();
            }
            return gameManager;
        }

        public CampaignGameManager CreateGameManager(LoadResult loadResult = null)
        {
            CampaignGameManager gameManager;
            if (loadResult != null)
            {
                gameManager = new CampaignGameManager(loadResult);
            }
            else
            {
                gameManager = new CampaignGameManager();
            }
            return gameManager;
        }


        public void ShutDownServer()
        {
            Current?.Stop();
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
            connection.OnClientJoined += m_RailServer.ClientJoined;
            connection.OnDisconnected += m_RailServer.Disconnected;
        }
    }
}
