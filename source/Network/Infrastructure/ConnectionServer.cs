using System;
using Network.Protocol;
using NLog;
using Stateless;
using Version = Network.Protocol.Version;

namespace Network.Infrastructure
{
    public class ConnectionServerPacketHandlerAttribute : PacketHandlerAttribute
    {
        public ConnectionServerPacketHandlerAttribute(EServerConnectionState state, EPacket eType)
        {
            State = state;
            Type = eType;
        }
    }
    public class ConnectionServer : ConnectionBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConnectionServerSM m_ServerSM;
        private readonly ISaveData m_WorldData;

        public ConnectionServer(
            INetworkConnection network,
            IGameStatePersistence persistence,
            ISaveData worldData) : base(network, persistence)
        {
            m_WorldData = worldData;
            m_ServerSM = new ConnectionServerSM();

            #region State Machine Callbacks
            m_ServerSM.TerminatedState.OnEntryFrom(m_ServerSM.CloseTrigger, closeConnection);

            m_ServerSM.ClientJoiningState.OnEntry(SendJoinRequestAccepted);

            m_ServerSM.ReadyState.OnEntry(onConnected);
            #endregion

            Dispatcher.RegisterPacketHandler(ReceiveClientHello);
            Dispatcher.RegisterPacketHandler(ReceiveClientInfo);
            Dispatcher.RegisterPacketHandler(ReceiveClientJoined);
            Dispatcher.RegisterPacketHandler(ReceiveSyncPacket);
            Dispatcher.RegisterPacketHandler(ReceiveClientKeepAlive);

            Dispatcher.RegisterStateMachine(this, m_ServerSM);
        }

        public override Enum State => m_ServerSM.StateMachine.State;
        public event Action<ConnectionServer> OnClientJoined;
        public event Action<ConnectionServer> OnDisconnected;

        ~ConnectionServer()
        {
            Dispatcher.UnregisterPacketHandlers(this);
        }
        public void SendWorldData()
        {
            Send(new Packet(EPacket.Server_WorldData, m_WorldData.SerializeInitialWorldState()));
        }

        public override void Disconnect(EDisconnectReason eReason)
        {
            if (!m_ServerSM.StateMachine.IsInState(EServerConnectionState.Terminated))
            {
                m_ServerSM.StateMachine.Fire(
                    m_ServerSM.CloseTrigger,
                    eReason);
            }
        }

        private void closeConnection(EDisconnectReason eReason)
        {
            OnDisconnected?.Invoke(this);
            Network.Close(eReason);
        }

        private void onConnected()
        {
            OnClientJoined?.Invoke(this);
        }

        #region ServerAwaitingClient
        [ConnectionServerPacketHandler(EServerConnectionState.AwaitingClient, EPacket.Client_Hello)]
        private void ReceiveClientHello(ConnectionBase connection, Packet packet)
        {
            Client_Hello payload = Client_Hello.Deserialize(new ByteReader(packet.Payload));
            if (payload.m_Version == Version.Number)
            {
                SendRequestClientInfo();
            }
            else
            {
                Logger.Error(
                    "Join request denied - version mismatch. {packetType}: {payload}. server version: {protocolVersion}.",
                    packet.Type,
                    payload,
                    Version.Number);
                Disconnect(EDisconnectReason.WrongProtocolVersion);
            }
        }

        private void SendRequestClientInfo()
        {
            Send(
                new Packet(
                    EPacket.Server_RequestClientInfo,
                    new Server_RequestClientInfo().Serialize()));
        }

        [ConnectionServerPacketHandler(EServerConnectionState.AwaitingClient, EPacket.Client_Info)]
        private void ReceiveClientInfo(ConnectionBase connection, Packet packet)
        {
            Client_Info info = Client_Info.Deserialize(new ByteReader(packet.Payload));
            Logger.Info("Received client join request from {playerName}.", info.m_Player.Name);
            m_ServerSM.StateMachine.Fire(EServerConnectionTrigger.ClientInfoVerified);
        }
        #endregion

        #region ServerJoining, ServerSendingWorldData & ServerPlaying
        private void SendJoinRequestAccepted()
        {
            Send(
                new Packet(
                    EPacket.Server_JoinRequestAccepted,
                    new Server_JoinRequestAccepted().Serialize()));
        }

        [ConnectionServerPacketHandler(EServerConnectionState.ClientJoining, EPacket.Client_Loaded)]
        private void ReceiveClientJoined(ConnectionBase connection, Packet packet)
        {
            Client_Joined payload = Client_Joined.Deserialize(new ByteReader(packet.Payload));
            m_ServerSM.StateMachine.Fire(EServerConnectionTrigger.ClientReady);
        }

        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.Sync)]
        private void ReceiveSyncPacket(ConnectionBase connection, Packet packet)
        {
            try
            {
                m_WorldData.Receive(packet.Payload);
            }
            catch (Exception e)
            {
                Logger.Error(
                    e,
                    "Sync data received from {client} could not be parsed. Ignored.",
                    this);
            }
        }

        [ConnectionServerPacketHandler(EServerConnectionState.Ready, EPacket.KeepAlive)]
        private void ReceiveClientKeepAlive(ConnectionBase connection, Packet packet)
        {
            KeepAlive payload = KeepAlive.Deserialize(new ByteReader(packet.Payload));
        }
        #endregion
    }
}
