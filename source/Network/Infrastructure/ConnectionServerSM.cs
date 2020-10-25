using Common;
using Stateless;

namespace Network.Infrastructure
{
    using StateConfiguration = StateMachine<EServerConnectionState, EServerConnectionTrigger>.StateConfiguration;
    public enum EServerConnectionState
    {
        /// <summary>
        /// Server is awaiting connection
        /// </summary>
        AwaitingClient,

        /// <summary>
        /// Client is joining.
        /// </summary>
        ClientJoining,

        /// <summary>
        /// Client is ready for state transfer and synchronization.
        /// </summary>
        Ready,

        /// <summary>
        /// Connection is terminated.
        /// </summary>
        Terminated,
    }

    public enum EServerConnectionTrigger
    {
        /// <summary>
        /// Close connection with client.
        /// </summary>
        Close,

        /// <summary>
        /// Client info is valid.
        /// </summary>
        ClientInfoVerified,

        /// <summary>
        /// Client is ready for state transfer and synchronization.
        /// </summary>
        ClientReady,
    }

    public class ConnectionServerSM : CoopStateMachine<EServerConnectionState, EServerConnectionTrigger>
    {
        public readonly StateConfiguration TerminatedState;
        public readonly StateConfiguration AwaitingClientState;
        public readonly StateConfiguration ClientJoiningState;
        public readonly StateConfiguration ReadyState;

        public readonly StateMachine<EServerConnectionState, EServerConnectionTrigger>.TriggerWithParameters<EDisconnectReason>
                CloseTrigger;
        public ConnectionServerSM() : base(EServerConnectionState.AwaitingClient)
        {
            // Close trigger
            CloseTrigger = StateMachine.SetTriggerParameters<EDisconnectReason>(EServerConnectionTrigger.Close);

            TerminatedState = StateMachine.Configure(EServerConnectionState.Terminated);
            TerminatedState.PermitReentry(EServerConnectionTrigger.Close);

            AwaitingClientState = StateMachine.Configure(EServerConnectionState.AwaitingClient);
            AwaitingClientState.Permit(EServerConnectionTrigger.Close, EServerConnectionState.Terminated);
            AwaitingClientState.Permit(EServerConnectionTrigger.ClientInfoVerified, EServerConnectionState.ClientJoining);

            ClientJoiningState = StateMachine.Configure(EServerConnectionState.ClientJoining);
            ClientJoiningState.Permit(EServerConnectionTrigger.Close, EServerConnectionState.Terminated);
            ClientJoiningState.Permit(EServerConnectionTrigger.ClientReady, EServerConnectionState.Ready);

            ReadyState = StateMachine.Configure(EServerConnectionState.Ready);
            ReadyState.Permit(EServerConnectionTrigger.Close, EServerConnectionState.Terminated);
        }
    }
}
