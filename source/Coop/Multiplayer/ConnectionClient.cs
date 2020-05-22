using System;
using Coop.Network;
using NLog;
using Stateless;

namespace Coop.Multiplayer
{
    public class ConnectionClient : ConnectionBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly StateMachine<EConnectionState, ETrigger> m_StateMachine;
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

            StateMachine<EConnectionState, ETrigger>.TriggerWithParameters<EDisconnectReason>
                disconnectTrigger =
                    m_StateMachine.SetTriggerParameters<EDisconnectReason>(ETrigger.Disconnect);
            m_StateMachine.Configure(EConnectionState.Disconnecting)
                          .OnEntryFrom(disconnectTrigger, closeConnection)
                          .Permit(ETrigger.Disconnected, EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.ClientJoinRequesting)
                          .OnEntry(sendClientHello)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .PermitIf(
                              ETrigger.ServerAcceptedJoinRequest,
                              EConnectionState.ClientAwaitingWorldData,
                              () => m_WorldData.RequiresInitialWorldData)
                          .PermitIf(
                              ETrigger.ServerAcceptedJoinRequest,
                              EConnectionState.ClientConnected,
                              () => !m_WorldData.RequiresInitialWorldData);

            m_StateMachine.Configure(EConnectionState.ClientAwaitingWorldData)
                          .OnEntry(sendClientRequestInitialWorldData)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .Permit(
                              ETrigger.InitialWorldDataReceived,
                              EConnectionState.ClientConnected);

            m_StateMachine.Configure(EConnectionState.ClientConnected)
                          .OnEntry(onConnected)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting);

            Dispatcher.RegisterPacketHandlers(this);
        }

        public override EConnectionState State => m_StateMachine.State;
        public event Action<ConnectionClient> OnClientJoined;
        public event Action<EDisconnectReason> OnDisconnected;

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

        private enum ETrigger
        {
            TryJoinServer,
            ServerAcceptedJoinRequest,
            InitialWorldDataReceived,
            Disconnect,
            Disconnected
        }

        #region ClientJoinRequesting
        private void sendClientHello()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Client_Hello,
                    new Protocol.Client_Hello(Protocol.Version).Serialize()));
        }

        [PacketHandler(
            EConnectionState.ClientJoinRequesting,
            Protocol.EPacket.Server_RequestClientInfo)]
        private void receiveClientInfoRequest(Packet packet)
        {
            Protocol.Server_RequestClientInfo payload =
                Protocol.Server_RequestClientInfo.Deserialize(new ByteReader(packet.Payload));
            sendClientInfo();
        }

        private void sendClientInfo()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Client_Info,
                    new Protocol.Client_Info(new Player("Unknown")).Serialize()));
        }

        [PacketHandler(
            EConnectionState.ClientJoinRequesting,
            Protocol.EPacket.Server_JoinRequestAccepted)]
        private void receiveJoinRequestAccepted(Packet packet)
        {
            Protocol.Server_JoinRequestAccepted payload =
                Protocol.Server_JoinRequestAccepted.Deserialize(new ByteReader(packet.Payload));
            m_StateMachine.Fire(ETrigger.ServerAcceptedJoinRequest);
        }
        #endregion

        #region ClientAwaitingWorldData & ClientConnected
        private void sendClientRequestInitialWorldData()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Client_RequestWorldData,
                    new Protocol.Client_RequestWorldData().Serialize()));
        }

        [PacketHandler(EConnectionState.ClientAwaitingWorldData, Protocol.EPacket.Server_WorldData)]
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

        private void onConnected()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Client_Joined,
                    new Protocol.Client_Joined().Serialize()));
            OnClientJoined?.Invoke(this);
        }

        [PacketHandler(EConnectionState.ClientConnected, Protocol.EPacket.Sync)]
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

        [PacketHandler(EConnectionState.ClientAwaitingWorldData, Protocol.EPacket.KeepAlive)]
        [PacketHandler(EConnectionState.ClientConnected, Protocol.EPacket.KeepAlive)]
        private void receiveServerKeepAlive(Packet packet)
        {
            Protocol.KeepAlive payload =
                Protocol.KeepAlive.Deserialize(new ByteReader(packet.Payload));
            Send(
                new Packet(
                    Protocol.EPacket.KeepAlive,
                    new Protocol.KeepAlive(payload.m_iKeepAliveID).Serialize()));
        }
        #endregion
    }
}
