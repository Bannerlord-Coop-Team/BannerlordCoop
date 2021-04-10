using System;
using System.IO;
using Coop.Mod.DebugUtil;
using Coop.Mod.Managers;
using Coop.Mod.Persistence;
using Coop.Mod.Serializers;
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
using Network.Protocol;
using Network;
using System.Collections.Generic;
using Stateless;
using Common;
using System.Linq;
using TaleWorlds.ObjectSystem;
using Coop.Mod.Persistence.Party;
using RailgunNet.Logic;

namespace Coop.Mod
{
    class GameServerPacketHandlerAttribute : PacketHandlerAttribute
    {
        public GameServerPacketHandlerAttribute(ECoopServerState state, EPacket eType)
        {
            State = state;
            Type = eType;
        }
    }

    public class CoopServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopServer> m_Instance =
            new Lazy<CoopServer>(() => new CoopServer());

        private GameEnvironmentServer m_GameEnvironmentServer;
        public bool AreAllClientsPlaying =>
            m_CoopServerSMs.All(clientSM => clientSM.Key.State.Equals(EServerConnectionState.Ready));
        private readonly Dictionary<ConnectionServer, CoopServerSM> m_CoopServerSMs = new Dictionary<ConnectionServer, CoopServerSM>();
        public IEnvironmentServer Environment => m_GameEnvironmentServer;

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
        public ServerGameManager gameManager { get; private set; }

        #region Events
        public event Action OnServerSendingWorldData;
        public event Action OnServerSentWorldData;
        #endregion

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
                ServerConfiguration config = new ServerConfiguration();

                Server.EType eServerType = Server.EType.Threaded;
                Current = new Server(eServerType);

                SyncedObjectStore = new SharedRemoteStore(new SerializableFactory());
                m_GameEnvironmentServer = new GameEnvironmentServer();
                Persistence = new CoopServerRail(
                    Current,
                    SyncedObjectStore,
                    Registry.Server(m_GameEnvironmentServer),
                    config.EventBroadcastTimeout);

                Current.Updateables.Add(Persistence);
                Current.OnClientConnected += OnClientConnected;
                Current.OnClientDisconnected += OnClientDisconnected;

                if (eServerType == Server.EType.Direct)
                {
                    Main.Instance.Updateables.Add(Current);
                }

                Current.Start(config);
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
            Replay.Stop();
            Current?.Stop();
            m_NetManager?.Stop();

            Persistence = null;
            SyncedObjectStore = null;
            m_NetManager = null;
            m_GameEnvironmentServer = null;
            Current = null;

            m_CoopServerSMs.Clear();
        }

        public void StartGame(string saveName)
        {
#if DEBUG
            try
                {
                    LoadGameResult saveGameData = MBSaveLoad.LoadSaveGameData(
                        saveName,
                        Utilities.GetModulesNames());
                    MBGameManager.StartNewGame(CreateGameManager(saveGameData));
                }
                catch (IOException ex)
                {
                    Logger.Error("Save file not found: " + ex.Message);
                }
#endif
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
            CoopServerSM coopServerSM = new CoopServerSM();
            m_CoopServerSMs.Add(connection, coopServerSM);

            #region State Machine Callbacks
            coopServerSM.SendingWorldDataState.OnEntryFrom(coopServerSM.SendWorldDataTrigger, SendInitialWorldData);
            #endregion

            SyncedObjectStore.AddConnection(connection);

            // Event Registration
            connection.OnClientJoined += Persistence.ClientJoined;
            connection.OnDisconnected += Persistence.Disconnected;
            OnServerSendingWorldData += m_GameEnvironmentServer.LockTimeControlStopped;
            OnServerSentWorldData += m_GameEnvironmentServer.UnlockTimeControl;

            // Packet Handler Registration
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientRequestWorldData);
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientDeclineWorldData);
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientLoaded);
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientPlayerPartyChanged);

            // State Machine Registration
            connection.Dispatcher.RegisterStateMachine(connection, coopServerSM);
        }

        private void OnClientDisconnected(ConnectionServer connection, EDisconnectReason eReason)
        {
            m_CoopServerSMs.Remove(connection);

            connection.OnClientJoined -= Persistence.ClientJoined;
            connection.OnDisconnected -= Persistence.Disconnected;
            SyncedObjectStore?.RemoveConnection(connection);
        }

        [GameServerPacketHandler(ECoopServerState.Preparing, EPacket.Client_RequestWorldData)]
        private void ReceiveClientRequestWorldData(ConnectionBase connection, Packet packet)
        {
            ConnectionServer connectionServer = (ConnectionServer)connection;
            Client_RequestWorldData info =
                Client_RequestWorldData.Deserialize(new ByteReader(packet.Payload));
            Logger.Info("Client requested world data.");

            m_CoopServerSMs[connectionServer].StateMachine.Fire(
                    new StateMachine<ECoopServerState, ECoopServerTrigger>.TriggerWithParameters<
                        ConnectionServer>(ECoopServerTrigger.RequiresWorldData),
                    connectionServer);
        }

        [GameServerPacketHandler(ECoopServerState.Preparing, EPacket.Client_DeclineWorldData)]
        private void ReceiveClientDeclineWorldData(ConnectionBase connection, Packet packet)
        {
            m_CoopServerSMs[(ConnectionServer)connection].StateMachine.Fire(ECoopServerTrigger.DeclineWorldData);
        }

        [GameServerPacketHandler(ECoopServerState.SendingWorldData, EPacket.Client_Loaded)]
        private void ReceiveClientLoaded(ConnectionBase connection, Packet packet)
        {
            m_CoopServerSMs[(ConnectionServer)connection].StateMachine.Fire(ECoopServerTrigger.ClientLoaded);
        }

        [GameServerPacketHandler(ECoopServerState.Playing, EPacket.Client_PartyChanged)]
        private void ReceiveClientPlayerPartyChanged(ConnectionBase connection, Packet packet)
        {
            MBGUID guid = MBGUIDSerializer.Deserialize(new ByteReader(packet.Payload));
            MobileParty party = (MobileParty)MBObjectManager.Instance.GetObject(guid);

            party.Party.UpdateVisibilityAndInspected(false);

            // Add party to persistance since manual creation of party is not handled
            Persistence.EntityManager.AddParty(party);

            Persistence.EntityManager.GrantPartyControl(party, Persistence.ConnectedClients.Last());
        }

        private void SendInitialWorldData(ConnectionServer connection)
        {
            OnServerSendingWorldData?.Invoke();
            connection.SendWorldData();
            OnServerSentWorldData?.Invoke();
        }
    }
}
