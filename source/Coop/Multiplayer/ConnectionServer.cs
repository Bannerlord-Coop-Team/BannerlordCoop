using System;
using Coop.Common;
using Coop.Network;
using Stateless;

namespace Coop.Multiplayer
{
    public class ConnectionServer : ConnectionBase
    {
        private readonly StateMachine<EConnectionState, ETrigger> m_StateMachine;
        private readonly ISaveData m_WorldData;

        public ConnectionServer(
            INetworkConnection network,
            IGameStatePersistence persistence,
            ISaveData worldData) : base(network, persistence)
        {
            m_WorldData = worldData;
            m_StateMachine =
                new StateMachine<EConnectionState, ETrigger>(EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.Disconnected)
                          .Permit(ETrigger.WaitForClient, EConnectionState.ServerAwaitingClient);

            StateMachine<EConnectionState, ETrigger>.TriggerWithParameters<EDisconnectReason>
                disconnectTrigger =
                    m_StateMachine.SetTriggerParameters<EDisconnectReason>(ETrigger.Disconnect);
            m_StateMachine.Configure(EConnectionState.Disconnecting)
                          .OnEntryFrom(disconnectTrigger, closeConnection)
                          .Permit(ETrigger.Disconnected, EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.ServerAwaitingClient)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .Permit(ETrigger.ClientInfoVerified, EConnectionState.ServerJoining);

            m_StateMachine.Configure(EConnectionState.ServerJoining)
                          .OnEntry(SendJoinRequestAccepted)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .Permit(
                              ETrigger.ClientRequestedWorldData,
                              EConnectionState.ServerSendingWorldData)
                          .Permit(ETrigger.ClientJoined, EConnectionState.ServerConnected);

            m_StateMachine.Configure(EConnectionState.ServerSendingWorldData)
                          .OnEntry(SendInitialWorldData)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                          .Permit(ETrigger.ClientJoined, EConnectionState.ServerConnected);

            m_StateMachine.Configure(EConnectionState.ServerConnected)
                          .OnEntry(onConnected)
                          .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting);

            Dispatcher.RegisterPacketHandlers(this);
        }

        public override EConnectionState State => m_StateMachine.State;
        public event Action<ConnectionServer> OnClientJoined;
        public event Action<ConnectionServer> OnDisconnected;

        ~ConnectionServer()
        {
            Dispatcher.UnregisterPacketHandlers(this);
        }

        public void PrepareForClientConnection()
        {
            m_StateMachine.Fire(ETrigger.WaitForClient);
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
            OnDisconnected?.Invoke(this);
            Network.Close(eReason);
            m_StateMachine.Fire(ETrigger.Disconnected);
        }

        private void onConnected()
        {
            OnClientJoined?.Invoke(this);
        }

        private enum ETrigger
        {
            WaitForClient,
            ClientInfoVerified,
            ClientRequestedWorldData,
            ClientJoined,
            Disconnect,
            Disconnected
        }

        #region ServerAwaitingClient
        [PacketHandler(EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Hello)]
        private void ReceiveClientHello(Packet packet)
        {
            Protocol.Client_Hello payload =
                Protocol.Client_Hello.Deserialize(new ByteReader(packet.Payload));
            if (payload.m_Version == Protocol.Version)
            {
                SendRequestClientInfo();
            }
            else
            {
                Log.Error(
                    $"Join request denied - version mismatch. {packet.Type}: {payload}. server version: {Protocol.Version}.");
                Disconnect(EDisconnectReason.WrongProtocolVersion);
            }
        }

        private void SendRequestClientInfo()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Server_RequestClientInfo,
                    new Protocol.Server_RequestClientInfo().Serialize()));
        }

        [PacketHandler(EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Info)]
        private void ReceiveClientInfo(Packet packet)
        {
            Protocol.Client_Info info =
                Protocol.Client_Info.Deserialize(new ByteReader(packet.Payload));
            Log.Info($"Received client join request from {info.m_Player.Name}.");
            m_StateMachine.Fire(ETrigger.ClientInfoVerified);
        }
        #endregion

        #region ServerJoining, ServerSendingWorldData & ServerConnected
        private void SendJoinRequestAccepted()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Server_JoinRequestAccepted,
                    new Protocol.Server_JoinRequestAccepted().Serialize()));
        }

        [PacketHandler(EConnectionState.ServerJoining, Protocol.EPacket.Client_RequestWorldData)]
        private void ReceiveClientRequestWorldData(Packet packet)
        {
            Protocol.Client_RequestWorldData info =
                Protocol.Client_RequestWorldData.Deserialize(new ByteReader(packet.Payload));
            Log.Info("Client requested world data.");
            m_StateMachine.Fire(ETrigger.ClientRequestedWorldData);
        }

        private void SendInitialWorldData()
        {
            Send(
                new Packet(
                    Protocol.EPacket.Server_WorldData,
                    m_WorldData.SerializeInitialWorldState()));
        }

        [PacketHandler(EConnectionState.ServerJoining, Protocol.EPacket.Client_Joined)]
        [PacketHandler(EConnectionState.ServerSendingWorldData, Protocol.EPacket.Client_Joined)]
        private void receiveClientJoined(Packet packet)
        {
            Protocol.Client_Joined payload =
                Protocol.Client_Joined.Deserialize(new ByteReader(packet.Payload));
            m_StateMachine.Fire(ETrigger.ClientJoined);
        }

        [PacketHandler(EConnectionState.ServerConnected, Protocol.EPacket.Sync)]
        private void receiveSyncPacket(Packet packet)
        {
            try
            {
                m_WorldData.Receive(packet.Payload);
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Sync data received from client {this} could not be parsed '{e}'. Ignored.");
            }
        }

        [PacketHandler(EConnectionState.ServerSendingWorldData, Protocol.EPacket.KeepAlive)]
        [PacketHandler(EConnectionState.ServerConnected, Protocol.EPacket.KeepAlive)]
        private void receiveClientKeepAlive(Packet packet)
        {
            Protocol.KeepAlive payload =
                Protocol.KeepAlive.Deserialize(new ByteReader(packet.Payload));
        }
        #endregion
    }
}
