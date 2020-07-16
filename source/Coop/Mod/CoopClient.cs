﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common;
using Coop.Mod.Managers;
using Coop.Mod.Persistence;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using Network.Infrastructure;
using Network.Protocol;
using NLog;
using RailgunNet.Logic;
using StoryMode;
using Sync.Store;
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

    public class CoopClient : IUpdateable
    {
        private const int MaxReconnectAttempts = 2;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient());
        private readonly CoopClientSM m_CoopClientSM;

        [NotNull] private readonly LiteNetManagerClient m_NetManager;

        /// <summary>
        ///     Internal data storage for <see cref="SyncedObjectStore" />.
        /// </summary>
        private readonly Dictionary<ObjectId, object> m_SyncedObjects =
            new Dictionary<ObjectId, object>();

        private int m_ReconnectAttempts = MaxReconnectAttempts;

        public Action<RemoteStore> RemoteStoreCreated;
        public Action<PersistenceClient> OnPersistenceInitialized;
        private MBGameManager gameManager;

        public CoopClient()
        {
            Session = new GameSession(new GameData());
            Session.OnConnectionDestroyed += ConnectionDestroyed;
            m_NetManager = new LiteNetManagerClient(Session);
            GameState = new CoopGameState();
            Events = new CoopEvents();
            m_CoopClientSM = new CoopClientSM();
            
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

        [NotNull] public GameSession Session { get; }

        public static CoopClient Instance => m_Instance.Value;

        public CoopGameState GameState { get; }
        public CoopEvents Events { get; }

        #region Events
        public event Action OnClientLoaded;
        #endregion

        public bool ClientPlaying
        {
            get
            {
                if (Session.Connection == null)
                {
                    return false;
                }

                return Session.Connection.State.Equals(ECoopClientState.Playing);
            }
        }

        public bool ClientRequestingWorldData
        {
            get
            {
                if (Session.Connection == null)
                {
                    return false;
                }

                // TODO change to main menu state
                return Session.Connection.State.Equals(ECoopClientState.ReceivingWorldData);
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

        private void TryInitPersistence()
        {
            ConnectionClient con = Session.Connection;
            if (con == null || !con.State.Equals(ECoopClientState.Playing)) return;

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
            
            if (Coop.IsServer)
            {
                m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.CharacterExists);
            }
            else
            {
                // TODO get if character exists on server
                m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.RequiresCharacterCreation);
            }
            

            SyncedObjectStore = new RemoteStore(m_SyncedObjects, con);
            RemoteStoreCreated?.Invoke(SyncedObjectStore);

            #region events
            OnClientLoaded += TryInitPersistence;
            Session.Connection.OnDisconnected += ConnectionClosed;
            #endregion

            // Handler Registration
            Session.Connection.Dispatcher.RegisterPacketHandler(ReceiveInitialWorldData);
            Session.Connection.Dispatcher.RegisterPacketHandler(ReceiveSyncPacket);
        }

        private void CreateCharacter()
        {
            if (gameManager == null)
            {
                gameManager = new ClientCharacterCreatorManager();
                MBGameManager.StartNewGame(gameManager);
                ClientCharacterCreatorManager.OnLoadFinishedEvent += (object source, EventArgs e) =>
                {
                    StoryModeEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, () =>
                    {
                        if(m_CoopClientSM.StateMachine.State.Equals(ECoopClientState.CharacterCreation))
                        {
                            CharacterCreationOver();
                        }
                    });
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
            m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.CharacterCreated);
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
                gameManager = new ClientManager(((GameData)Session.World).LoadResult);
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
            Session.Connection.Send(
                new Packet(
                    EPacket.Client_Joined,
                    new Client_GameLoading().Serialize()));
        }
        #endregion

        #region ClientPlaying
        public void SendGameLoaded()
        {
            Session.Connection.Send(
                new Packet(
                    EPacket.Client_Joined,
                    new Client_GameLoaded().Serialize()));
            m_CoopClientSM.StateMachine.Fire(ECoopClientTrigger.GameLoaded);
            Session.Connection.Send(new Packet(EPacket.Client_Joined, new Client_Joined().Serialize()));
            if (m_CoopClientSM.StateMachine.State.Equals(ECoopClientState.Playing))
            {
                OnClientLoaded?.Invoke();
            }
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

