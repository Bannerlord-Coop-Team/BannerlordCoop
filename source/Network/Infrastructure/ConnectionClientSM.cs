using Common;
using Stateless;

namespace Network.Infrastructure
{
    using StateConfiguration = StateMachine<EClientConnectionState, EClientConnectionTrigger>.StateConfiguration;
    public enum EClientConnectionState
    {
        /// <summary>
        /// Client is trying to establish a connection to a server.
        /// </summary>
        JoinRequesting,

        /// <summary>
        /// A connection with the server has been established.
        /// </summary>
        Connected,

        /// <summary>
        /// Connection has been closed.
        /// </summary>
        Disconnected
    }

    public enum EClientConnectionTrigger
    {
        /// <summary>
        /// A request to join server has been sent.
        /// </summary>
        TryJoinServer,

        /// <summary>
        /// Server has accepted join request
        /// </summary>
        ServerAcceptedJoinRequest,

        /// <summary>
        /// Connection has been closed.
        /// </summary>
        Disconnect,
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
