using System;
using Network.Protocol;
using NLog;
using Stateless;
using StoryMode;
using Version = Network.Protocol.Version;

namespace Network.Infrastructure
{
    public class ConnectionClient : ConnectionBase
    {
        public static StateMachine<EConnectionState, ETrigger> m_StateMachine;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ISaveData m_WorldData;

        public ConnectionClient(
            INetworkConnection network,
            IGameStatePersistence persistence,
            ISaveData worldData) : base(network, persistence)
        {
            m_WorldData = worldData;

            m_StateMachine =
                new StateMachine<EConnectionState, ETrigger>(EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.Disconnected)
                          .Permit(ETrigger.TryJoinServer, EConnectionState.ClientJoinRequesting);

            // Disconnect trigger
            StateMachine<EConnectionState, ETrigger>.TriggerWithParameters<EDisconnectReason>
                disconnectTrigger =
                    m_StateMachine.SetTriggerParameters<EDisconnectReason>(ETrigger.Disconnect);
            m_StateMachine.Configure(EConnectionState.Disconnecting)
                          .OnEntryFrom(disconnectTrigger, closeConnection)
                          .Permit(ETrigger.Disconnected, EConnectionState.Disconnected);

            // Client join request
            m_StateMachine.Configure(EConnectionState.ClientJoinRequesting)
                          .OnEntry(sendClientHello)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .PermitIf(
                              ETrigger.RequireClientCreation,
                              EConnectionState.ClientCharacterCreation,
                              () => m_WorldData.RequiresCharacterCreation)
                          .PermitIf(
                              ETrigger.ServerAcceptedJoinRequest,
                              EConnectionState.ClientAwaitingWorldData,
                              () => m_WorldData.RequiresInitialWorldData)
                          .PermitIf(
                              ETrigger.ServerAcceptedJoinRequest,
                              EConnectionState.ClientPlaying,
                              () => !m_WorldData.RequiresInitialWorldData);

            m_StateMachine.Configure(EConnectionState.ClientCharacterCreation)
                          .OnEntry(characterCreation)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .PermitIf(
                              ETrigger.CharacterCreated,
                              EConnectionState.ClientAwaitingWorldData,
                              () => m_WorldData.RequiresCharacterCreation)
                          .PermitIf(
                              ETrigger.ServerAcceptedJoinRequest,
                              EConnectionState.ClientPlaying,
                              () => !m_WorldData.RequiresCharacterCreation);

            // Client request world data
            m_StateMachine.Configure(EConnectionState.ClientAwaitingWorldData)
                          .OnEntry(sendClientRequestInitialWorldData)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .Permit(
                              ETrigger.InitialWorldDataReceived,
                              EConnectionState.ClientLoading);

            // Client loading game
            m_StateMachine.Configure(EConnectionState.ClientLoading)
                          .OnEntry(sendGameLoading)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .Permit(
                              ETrigger.GameLoaded,
                              EConnectionState.ClientPlaying);

            // Client playing (game loaded)
            m_StateMachine.Configure(EConnectionState.ClientPlaying)
                          .OnEntry(onConnected)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting);

            Dispatcher.RegisterPacketHandlers(this);
        }

        public override EConnectionState State => m_StateMachine.State;

        public Action<ConnectionClient> OnClientJoined { get; set; }

        public event Action<ConnectionClient> OnClientLoaded;
        public event Action<EDisconnectReason> OnDisconnected;
        public event Action<ConnectionClient> OnCharacterCreated;
        public event Action<ConnectionClient> RequireCharacterCreation;

        ~ConnectionClient()
        {
            Dispatcher.UnregisterPacketHandlers(this);
        }

        public void Connect()
        {
            m_StateMachine.Fire(ETrigger.TryJoinServer);
        }

        public override void Disconnect(EDisconnectReason eReason)
        {
            if (!m_StateMachine.IsInState(EConnectionState.Disconnected))
            {
                m_StateMachine.Fire(
                    new StateMachine<EConnectionState, ETrigger>.TriggerWithParameters<
                        EDisconnectReason>(ETrigger.Disconnect),
                    eReason);
            }
        }

        private void closeConnection(EDisconnectReason eReason)
        {
            Network.Close(eReason);
            m_StateMachine.Fire(ETrigger.Disconnected);
            OnDisconnected?.Invoke(eReason);
        }

        public enum ETrigger
        {
            TryJoinServer,
            ServerAcceptedJoinRequest,
            RequireClientCreation,
            CharacterCreated,
            InitialWorldDataReceived,
            GameLoaded,
            Disconnect,
            Disconnected
        }

        #region ClientJoinRequesting
        private void sendClientHello()
        {
            Send(new Packet(EPacket.Client_Hello, new Client_Hello(Version.Number).Serialize()));
        }

        [PacketHandler(EConnectionState.ClientJoinRequesting, EPacket.Server_RequestClientInfo)]
        private void receiveClientInfoRequest(Packet packet)  
        {
            Server_RequestClientInfo payload =
                Server_RequestClientInfo.Deserialize(new ByteReader(packet.Payload));
            sendClientInfo();
        }

        private void sendClientInfo()
        {
            Send(
                new Packet(
                    EPacket.Client_Info,
                    new Client_Info(new Player("Unknown")).Serialize()));
        }

        [PacketHandler(EConnectionState.ClientJoinRequesting, EPacket.Server_JoinRequestAccepted)]
        private void receiveJoinRequestAccepted(Packet packet)
        {
            Server_JoinRequestAccepted payload =
                Server_JoinRequestAccepted.Deserialize(new ByteReader(packet.Payload));
            if(m_WorldData.RequiresCharacterCreation)
            {
                m_StateMachine.Fire(ETrigger.RequireClientCreation);
            }
            else
            {
                m_StateMachine.Fire(ETrigger.ServerAcceptedJoinRequest);
            }
        }

        #endregion

        #region ClientCharacterCreation
        private void characterCreation()
        {
            RequireCharacterCreation?.Invoke(this);
        }
        #endregion

        #region ClientAwaitingWorldData
        private void sendClientRequestInitialWorldData()
        {
            Send(
                new Packet(
                    EPacket.Client_RequestWorldData,
                    new Client_RequestWorldData().Serialize()));
        }

        [PacketHandler(EConnectionState.ClientAwaitingWorldData, EPacket.Server_WorldData)]
        private void receiveInitialWorldData(Packet packet)
        {
            bool bSuccess = false;
            try
            {
                bSuccess = m_WorldData.Receive(packet.Payload);
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
                m_StateMachine.Fire(ETrigger.InitialWorldDataReceived);
            }
            else
            {
                Logger.Error(
                    "World data received from server could not be parsed. Disconnect {client}.",
                    this);
                Disconnect(EDisconnectReason.WorldDataTransferIssue);
            }
        }
        #endregion

        #region  ClientLoading
        private void sendGameLoading()
        {
            Send(
                new Packet(
                    EPacket.Client_Joined,
                    new Client_GameLoading().Serialize()));
        }
        public void sendGameLoaded(object source, EventArgs e)
        {
            Send(
                new Packet(
                    EPacket.Client_Joined,
                    new Client_GameLoaded().Serialize()));
            m_StateMachine.Fire(ETrigger.GameLoaded);
        }
        #endregion

        #region ClientPlaying
        private void onConnected()
        {
            Send(new Packet(EPacket.Client_Joined, new Client_Joined().Serialize()));
            OnClientLoaded?.Invoke(this);
        }

        [PacketHandler(EConnectionState.ClientPlaying, EPacket.Sync)]
        private void receiveSyncPacket(Packet packet)
        {
            try
            {
                m_WorldData.Receive(packet.Payload);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Sync data received from server could not be parsed. Ignored.");
            }
        }

        [PacketHandler(EConnectionState.ClientAwaitingWorldData, EPacket.KeepAlive)]
        [PacketHandler(EConnectionState.ClientPlaying, EPacket.KeepAlive)]
        private void receiveServerKeepAlive(Packet packet)
        {
            KeepAlive payload = KeepAlive.Deserialize(new ByteReader(packet.Payload));
            Send(new Packet(EPacket.KeepAlive, new KeepAlive(payload.m_iKeepAliveID).Serialize()));
        }
        #endregion
    }
}
