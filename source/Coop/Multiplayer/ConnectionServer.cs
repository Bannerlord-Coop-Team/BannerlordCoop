using Coop.Common;
using Coop.Multiplayer.Network;
using Coop.Network;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer
{
    public class ConnectionServer : ConnectionBase
    {
        private enum ETrigger
        {
            WaitForClient,
            JoinRequestAccepted,
            ConnectionEstablished,
            Disconnect,
            Disconnected
        }
        private readonly StateMachine<EConnectionState, ETrigger> m_StateMachine;
        public override EConnectionState State => m_StateMachine.State;

        public ConnectionServer(INetworkConnection network)
            : base(network)
        {
            m_StateMachine = new StateMachine<EConnectionState, ETrigger>(EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.Disconnected)
                .Permit(ETrigger.WaitForClient, EConnectionState.ServerAwaitingClient);

            var disconnectTrigger = m_StateMachine.SetTriggerParameters<EDisconnectReason>(ETrigger.Disconnect);
            m_StateMachine.Configure(EConnectionState.Disconnecting)
                .OnEntryFrom(disconnectTrigger, eReason => closeConnection(eReason))
                .Permit(ETrigger.Disconnected, EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.ServerAwaitingClient)
                .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                .Permit(ETrigger.JoinRequestAccepted, EConnectionState.ServerJoining);

            m_StateMachine.Configure(EConnectionState.ServerJoining)
                .OnEntry(() => SendJoinRequestAccepted())
                .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                .Permit(ETrigger.ConnectionEstablished, EConnectionState.ServerConnected);

            m_StateMachine.Configure(EConnectionState.ServerConnected)
                .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting);

            Dispatcher.RegisterPacketHandlers(this);
        }

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
                m_StateMachine.Fire(new StateMachine<EConnectionState, ETrigger>.TriggerWithParameters<EDisconnectReason>(ETrigger.Disconnect), eReason);
            }
        }
        private void closeConnection(EDisconnectReason eReason)
        {
            m_Network.Close(eReason);
            m_StateMachine.Fire(ETrigger.Disconnected);
        }
        #region ServerAwaitingClient
        [PacketHandler(EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Hello)]
        private void ReceiveClientHello(Packet packet)
        {
            Protocol.Client_Hello payload = Protocol.Client_Hello.Deserialize(new ByteReader(packet.Payload));
            if (payload.m_Version == Protocol.Version)
            {
                SendRequestClientInfo();
            }
            else
            {
                Log.Error($"Join request denied - version mismatch. {packet.Type}: {payload}. server version: {Protocol.Version}.");
                Disconnect(EDisconnectReason.WrongProtocolVersion);
            }
        }
        private void SendRequestClientInfo()
        {
            Send(new Packet(Protocol.EPacket.Server_RequestClientInfo, new Protocol.Server_RequestClientInfo().Serialize()));
        }
        [PacketHandler(EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Info)]
        private void ReceiveClientInfo(Packet packet)
        {
            Protocol.Client_Info info = Protocol.Client_Info.Deserialize(new ByteReader(packet.Payload));
            Log.Info($"Received client info: {info}.");
            m_StateMachine.Fire(ETrigger.JoinRequestAccepted);
        }
        #endregion
        #region ServerJoining
        private void SendJoinRequestAccepted()
        {
            Send(new Packet(Protocol.EPacket.Server_JoinRequestAccepted, new Protocol.Server_JoinRequestAccepted().Serialize()));
        }
        #endregion
        [PacketHandler(EConnectionState.ServerJoining, Protocol.EPacket.Client_KeepAlive)]
        [PacketHandler(EConnectionState.ServerConnected, Protocol.EPacket.Client_KeepAlive)]
        private void receiveClientKeepAlive(Packet packet)
        {
            Protocol.Client_KeepAlive payload = Protocol.Client_KeepAlive.Deserialize(new ByteReader(packet.Payload));
        }
    }
}
