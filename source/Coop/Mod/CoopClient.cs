using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common;
using Coop.Mod.Managers;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RemoteAction;
using Coop.Mod.Serializers;
using Coop.NetImpl.LiteNet;
using CoopFramework;
using JetBrains.Annotations;
using Network.Infrastructure;
using Network.Protocol;
using NLog;
using RailgunNet.Connection.Client;
using RailgunNet.Logic;
using StoryMode;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Logger = NLog.Logger;

namespace Coop.Mod
{
    class GameClientPacketHandlerAttribute : PacketHandlerAttribute
    {
        public GameClientPacketHandlerAttribute(ECoopClientState state, EPacket eType)
        {
            State = state;
            Type = eType;
        }
    }

    public class CoopClient : IUpdateable, IClientAccess
    {
        #region Private
        private const int MaxReconnectAttempts = 2;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient(new ClientConfiguration()));
        private readonly CoopClientSM m_CoopClientSM;

        [NotNull] private readonly LiteNetManagerClient m_NetManager;

        /// <summary>
        ///     Internal data storage for <see cref="SyncedObjectStore" />.
        /// </summary>
        private readonly Dictionary<ObjectId, object> m_SyncedObjects =
            new Dictionary<ObjectId, object>();

        private MBGameManager gameManager;

        private int m_ReconnectAttempts = MaxReconnectAttempts;
        private Hero m_Hero;
        private ObjectId m_HeroId;
        #endregion
        public Action<PersistenceClient> OnPersistenceInitialized;

        public Action<RemoteStore> RemoteStoreCreated;

        public CoopClient(ClientConfiguration config)
        {
            Session = new GameSession(new GameData());
            Session.OnConnectionDestroyed += ConnectionDestroyed;
            m_NetManager = new LiteNetManagerClient(Session, config);
            GameState = new CoopGameState();
            Events = new CoopEvents();
            m_CoopClientSM = new CoopClientSM();
            Synchronization = new Synchronization(this);
            
            #region State Machine Callbacks
            m_CoopClientSM.CharacterCreationState.OnEntry(CreateCharacter);
            m_CoopClientSM.ReceivingWorldDataState.OnEntry(SendClientRequestInitialWorldData);
            m_CoopClientSM.LoadingState.OnEntry(SendGameLoading);
            m_CoopClientSM.PlayingState.OnEntry(SendGameLoaded);
            #endregion


            Init();
        }

        /// <summary>
        ///     Object store shared with the server if connected. Otherwise null.
        /// </summary>
        [CanBeNull]
        public RemoteStore SyncedObjectStore { get; private set; }

        [CanBeNull] public PersistenceClient Persistence { get; private set; }
        
        [NotNull] public ISynchronization Synchronization { get; }

        [NotNull] public GameSession Session { get; }

        public static CoopClient Instance => m_Instance.Value;

        public CoopGameState GameState { get; }
        public CoopEvents Events { get; }

        public bool ClientConnected
        {
            get
            {
                if (Session.Connection == null)
                {
                    return false;
                }

                return Session.Connection.State.Equals(EClientConnectionState.Connected);
            }
        }

        public bool ClientPlaying
        {
            get
            {
                return m_CoopClientSM.State.Equals(ECoopClientState.Playing);
            }
        }

        public RemoteStore GetStore()
        {
            return SyncedObjectStore;
        }

        public RailClientRoom GetRoom()
        {
            return Persistence?.Room;
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

        private void TryInitPersistence()
        {
            ConnectionClient con = Session.Connection;
            if (con == null || !m_CoopClientSM.State.Equals(ECoopClientState.Playing)) return;

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

            Session.Connection.OnConnected += ConnectionEstablished;
        }

        private void ConnectionEstablished(ConnectionClient con)
        {
            if (m_CoopClientSM.State.Equals(ECoopClientState.MainManu))
            {
                if (Coop.IsServer)
                {
                    m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.CharacterExists);
                }
                else
                {
                    // TODO get if character exists on server
                    m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.RequiresCharacterCreation);
                }

                SyncedObjectStore = new RemoteStore(m_SyncedObjects, con, new SerializableFactory());
                RemoteStoreCreated?.Invoke(SyncedObjectStore);

                #region events
                Session.Connection.OnDisconnected += ConnectionClosed;
                #endregion

                // Handler Registration
                Session.Connection.Dispatcher.RegisterPacketHandler(ReceiveInitialWorldData);
                Session.Connection.Dispatcher.RegisterPacketHandler(ReceiveSyncPacket);

                Session.Connection.Dispatcher.RegisterStateMachine(this, m_CoopClientSM);
            }
        }

        private void CreateCharacter()
        {
            if (gameManager == null)
            {
                gameManager = new ClientCharacterCreatorManager();
                MBGameManager.StartNewGame(gameManager);

                ClientCharacterCreatorManager.OnGameLoadFinishedEvent += (object source, EventArgs e) =>
                {
                    if(e is HeroEventArgs args)
                    {
                        m_HeroId = args.HeroId;

                        CharacterCreationOver();
                    }
                    else
                    {
                        throw new Exception("EventArgs not of type HeroEventArgs");
                    }
                    
                };
            }
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

        #region ClientCharacterCreation

        public void CharacterCreationOver()
        {
            GetStore().OnObjectAcknowledged += (id, obj) =>
            {
                if (id == m_HeroId)
                {
                    m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.CharacterCreated);
                    if (obj is Hero hero)
                    {
                        m_Hero = hero;
                    }
                }
            };
        }
        #endregion

        #region ClientAwaitingWorldData
        private void SendClientRequestInitialWorldData()
        {
            if(Coop.IsServer)
            {
                Session.Connection.Send(
                new Packet(
                    EPacket.Client_DeclineWorldData,
                    new Client_DeclineWorldData().Serialize()));
                m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.WorldDataReceived);
                m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.GameLoaded);
            }
            else
            {
                Session.Connection.Send(
                new Packet(
                    EPacket.Client_RequestWorldData,
                    new Client_RequestWorldData().Serialize()));
            }
            
        }

        [GameClientPacketHandler(ECoopClientState.ReceivingWorldData, EPacket.Server_WorldData)]
        private void ReceiveInitialWorldData(ConnectionBase connection, Packet packet)
        {
            bool bSuccess = false;
            try
            {
                bSuccess = Session.World.Receive(packet.Payload);
            }
            catch (Exception e)
            {
                Logger.Error(
                    e,
                    "World data received from server could not be parsed . Disconnect {client}.",
                    this);
            }

            if (bSuccess)
            {
                m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.WorldDataReceived);
                gameManager = new ClientManager(((GameData)Session.World).LoadResult, m_Hero);
                MBGameManager.StartNewGame(gameManager);
                ClientManager.OnPreLoadFinishedEvent += (source, e) => {
                    CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, SendPlayerPartyChanged);
                };
                ClientManager.OnPostLoadFinishedEvent += (source, e) => {
                    m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.GameLoaded); 
                };
            }
            else
            {
                Logger.Error(
                    "World data received from server could not be parsed. Disconnect {client}.",
                    this);
                Session.Connection.Disconnect(EDisconnectReason.WorldDataTransferIssue);
            }
        }
        #endregion

        #region  ClientLoading
        private void SendGameLoading()
        {
            // TODO add loading and loaded messages
            //Session.Connection.Send(
            //    new Packet(
            //        EPacket.Client_Joined,
            //        new Client_GameLoading().Serialize()));
        }
        #endregion

        #region ClientPlaying
        public void SendGameLoaded()
        {
            Session.Connection.Send(
                new Packet(
                    EPacket.Client_Loaded,
                    new Client_Joined().Serialize()));
            TryInitPersistence();
        }

        private void SendPlayerPartyChanged(Hero hero, MobileParty party)
        {
            Session.Connection.Send(
                new Packet(
                    EPacket.Client_PartyChanged,
                    new MBGUIDSerializer(party.Id).Serialize()));
        }


        [GameClientPacketHandler(ECoopClientState.Playing, EPacket.Sync)]
        private void ReceiveSyncPacket(ConnectionBase connection, Packet packet)
        {
            try
            {
                Session.World.Receive(packet.Payload);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Sync data received from server could not be parsed. Ignored.");
            }
        }
        #endregion

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