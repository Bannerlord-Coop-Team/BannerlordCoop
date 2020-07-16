using Common;
using NLog;
using Stateless;
using System;
using System.Collections.Generic;

namespace Network.Infrastructure
{
    using StateConfiguration = StateMachine<EClientConnectionState, EClientConnectionTrigger>.StateConfiguration;
    public enum EClientConnectionTrigger
    {
        TryJoinServer,
        ServerAcceptedJoinRequest,
        Disconnect,
    }

    public enum EClientConnectionState
    {
        /** [client side] Client is trying to establish a connection to a server.
         *
         * Possible transitions to:
         * - ClientJoining: The server accepted the request.
         * - Disconnecting: Request timeout or the server rejected the request.
         */
        ///<summary>
        /// [client side] Client is trying to establish a connection to a server.
        /// Possible transitions to:
        /// </summary>
        JoinRequesting,

        /** [client side] Client is trying to establish a connection to a server.
         *
         * Possible transitions to:
         * - ClientJoining: The server accepted the request.
         * - Disconnecting: Request timeout or the server rejected the request.
         */
        Connected,

        /** Connection is being closed.
         *
         * Possible transitions to:
         * - Disconnected:  Connection was closed.
         */
        Disconnected
    }

    

    public class ConnectionClientSM : CoopStateMachine<EClientConnectionState, EClientConnectionTrigger>
    {
        public readonly StateConfiguration DisconnectState;
        public readonly StateConfiguration JoinRequestingState;
        public readonly StateConfiguration ConnectedState;
        public readonly StateMachine<EClientConnectionState, EClientConnectionTrigger>
            .TriggerWithParameters<EDisconnectReason> DisconnectTrigger;
        public ConnectionClientSM() : base(EClientConnectionState.Disconnected)
        {
            DisconnectTrigger = StateMachine.SetTriggerParameters<EDisconnectReason>(EClientConnectionTrigger.Disconnect);

            DisconnectState = StateMachine.Configure(EClientConnectionState.Disconnected);
            DisconnectState.Permit(EClientConnectionTrigger.TryJoinServer, EClientConnectionState.JoinRequesting);


            // Client join request
            JoinRequestingState = StateMachine.Configure(EClientConnectionState.JoinRequesting);
            JoinRequestingState.Permit(EClientConnectionTrigger.ServerAcceptedJoinRequest, EClientConnectionState.Connected);
            JoinRequestingState.Permit(EClientConnectionTrigger.Disconnect, EClientConnectionState.Disconnected);

            // Client connected
            ConnectedState = StateMachine.Configure(EClientConnectionState.Connected);
            ConnectedState.Permit(EClientConnectionTrigger.Disconnect, EClientConnectionState.Disconnected);
        }
    }
}
