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
using Coop.Mod.Patch.World;
using System.Diagnostics;
using Coop.Mod.Data;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using TaleWorlds.TwoDimension;
using System.Reflection;
using Coop.Mod.Serializers.Custom;
using TaleWorlds.SaveSystem;
using TaleWorlds.Localization;
using System.Text;
using Coop.Mod.Persistence.RemoteAction;
using Coop.Mod.GameSync;
using Coop.Mod.GameSync.Party;
using RailgunNet.Connection.Server;
using TaleWorlds.CampaignSystem.Party;
using Common.Serialization;

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

    public class CoopServer : IDisposable, IServerAccess
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
        public RemoteStoreServer SyncedObjectStore { get; private set; }

        [CanBeNull] public CoopServerRail Persistence { get; private set; }

        [NotNull] public CoopSyncServer Synchronization { get; private set; }

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

                SyncedObjectStore = new RemoteStoreServer(new SerializableFactory());
                m_GameEnvironmentServer = new GameEnvironmentServer();
                Persistence = new CoopServerRail(
                    Current,
                    SyncedObjectStore,
                    Registry.Server(m_GameEnvironmentServer),
                    config.EventBroadcastTimeout);
                Synchronization = new CoopSyncServer(this);
                Initializer.SetupSyncAfterLoad();

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
                m_NetManager = new LiteNetManagerServer(Current);
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
                LoadResult saveGameData = MBSaveLoad.LoadSaveGameData(
                    saveName);
                MBGameManager.StartNewGame(CreateGameManager(saveGameData));
            }
            catch (IOException ex)
            {
                Logger.Error("Save file not found: " + ex.Message);
            }
#endif
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

        public void Dispose()
        {
            ShutDownServer();
        }

        private void OnClientConnected(ConnectionServer connection)
        {
            CoopServerSM coopServerSM = new CoopServerSM();
            m_CoopServerSMs.Add(connection, coopServerSM);

            #region State Machine Callbacks
            #endregion

            SyncedObjectStore.AddConnection(connection);

            // Event Registration
            connection.OnClientJoined += Persistence.ClientJoined;
            connection.OnDisconnected += Persistence.Disconnected;
            OnServerSendingWorldData += m_GameEnvironmentServer.LockTimeControlStopped;
            OnServerSentWorldData += m_GameEnvironmentServer.UnlockTimeControl;

            // Packet Handler Registration
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientRequestWorldData);
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientLoaded);
            connection.Dispatcher.RegisterPacketHandler(SendGameData);
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientPlayerPartyChanged);

#if DEBUG
            connection.Dispatcher.RegisterPacketHandler(ReceiveClientBadId);
#endif

            // State Machine Registration
            connection.Dispatcher.RegisterStateMachine(connection, coopServerSM);
            connection.OnPlayerPartyRequest += Connection_OnPlayerPartyRequest;
        }

        private void Connection_OnPlayerPartyRequest(object sender, RequestPlayerParty e)
        {
            string clientId = e.ClientId;

            ConnectionServer connection = (ConnectionServer)sender;

            // if saved party exists
            if (CoopSaveManager.PlayerParties.ContainsKey(clientId))
            {
                // skip character creation on client
                Guid guid = CoopSaveManager.PlayerParties[clientId];
                connection.Send(new Packet(EPacket.Server_NotifyCharacterExists, CommonSerializer.Serialize(guid)));
            }
            else
            {
                // else do character creation
                connection.Send(new Packet(EPacket.Server_RequireCharacterCreation, new byte[0]));
            }
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
            OnServerSendingWorldData?.Invoke();

            ConnectionServer connectionServer = (ConnectionServer)connection;
            Client_RequestWorldData info =
                Client_RequestWorldData.Deserialize(new ByteReader(packet.Payload));
            Logger.Info("Client requested world data.");

            m_CoopServerSMs[connectionServer].StateMachine.Fire(
                    new StateMachine<ECoopServerState, ECoopServerTrigger>.TriggerWithParameters<
                        ConnectionServer>(ECoopServerTrigger.RequiresWorldData),
                    connectionServer);
        }

        [GameServerPacketHandler(ECoopServerState.Preparing, EPacket.Client_RequestGameData)]
        private void SendGameData(ConnectionBase connection, Packet packet)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                // Recieve player hero
                PlayerHeroSerializer serializer = (PlayerHeroSerializer)CommonSerializer.Deserialize(packet.Payload);
                Hero newHero = (Hero)serializer.Deserialize();

                // Remove main hero
                CoopObjectManager.RemoveObject(Hero.MainHero);
                CoopObjectManager.RemoveObject(Hero.MainHero.Father);

                // Create save data, this contains player id and MBGUID <> Guid id assosiations
                CoopObjectManager.AddObject(newHero.PartyBelongedTo);
                CoopObjectManager.AddObject(newHero.CharacterObject);
                SaveData saveData = new SaveData(CoopObjectManager.AddObject(newHero));

                connection.Send(new Packet(EPacket.Server_GameData, saveData));

                ConnectionServer connectionServer = (ConnectionServer)connection;
                m_CoopServerSMs[connectionServer].StateMachine.Fire(ECoopServerTrigger.RequiresWorldData);
            });
        }

        [GameServerPacketHandler(ECoopServerState.SendingGameData, EPacket.Client_Loaded)]
        private void ReceiveClientLoaded(ConnectionBase connection, Packet packet)
        {
            ConnectionServer connectionServer = (ConnectionServer)connection;
            m_CoopServerSMs[connectionServer].StateMachine.Fire(ECoopServerTrigger.ClientLoaded);

            OnServerSentWorldData?.Invoke();
        }

#if DEBUG
        [GameServerPacketHandler(ECoopServerState.SendingGameData, EPacket.BadID)]
        private void ReceiveClientBadId(ConnectionBase connection, Packet packet)
        {
            List<Guid> list = (List<Guid>) CommonSerializer.Deserialize(packet.Payload);
            foreach(Guid guid in list)
            {
                StringBuilder s = new StringBuilder("### Client replied BadID:\n");
                s.Append($"Guid: {guid}\n");
                object obj = CoopObjectManager.GetObject(guid);
                Type t = obj.GetType();
                s.Append($"Type: {t}\n");
                PropertyInfo[] props = t.GetProperties();
                foreach (PropertyInfo p in props)
                {
                    s.Append(p.Name + " : " + p.GetValue(obj) + "\n");
                }
                Logger.Error(s);
            }
            
        }
#endif

        [GameServerPacketHandler(ECoopServerState.Playing, EPacket.Client_PartyChanged)]
        private void ReceiveClientPlayerPartyChanged(ConnectionBase connection, Packet packet)
        {
            Guid guid = CommonSerializer.Deserialize<Guid>(packet.Payload);
            Debug.WriteLine($"Requested GUID {guid}");
            Hero clientHero = CoopObjectManager.GetObject<Hero>(guid);

            MobileParty party = clientHero.PartyBelongedTo;

            // TODO: quite hacky, wouldn't really work outside of the initial loading.
            MobilePartyManaged.MakeManaged(party, false);
            Persistence.MobilePartyEntityManager.GrantPartyControl(party, Persistence.ConnectedClients.Last());
        }

        #region IServerAccess
        public RemoteStoreServer GetStore()
        {
            return SyncedObjectStore;
        }

        public RailServerRoom GetRoom()
        {
            return Persistence.Room;
        }

        public EventBroadcastingQueue GetQueue()
        {
            return Persistence.EventQueue;
        }
        #endregion
    }
}
