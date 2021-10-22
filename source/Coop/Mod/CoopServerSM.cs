using Common;
using Network.Infrastructure;
using Stateless;

namespace Coop.Mod
{
    using StateConfiguration = StateMachine<ECoopServerState, ECoopServerTrigger>.StateConfiguration;
    enum ECoopServerState {
        /// <summary>
        /// A client is awaiting world data.
        /// </summary>
        Preparing,

        /// <summary>
        /// A client is receiving world data.
        /// </summary>
        SendingWorldData,

        /// <summary>
        /// A client is playing.
        /// </summary>
        Playing,

        /// <summary>
        /// A client requires validation
        /// </summary>
        ClientValidation,
    }

    enum ECoopServerTrigger {
        /// <summary>
        /// A client has requested world data.
        /// </summary>
        RequiresWorldData,

        /// <summary>
        /// A client does not need world data.
        /// </summary>
        DeclineWorldData,

        /// <summary>
        /// A client has recieved world data.
        /// </summary>
        ClientLoaded,
        ClientValidated,
    }

    /// <summary>
    /// Defines state machine used by CoopServer
    /// </summary>
    class CoopServerSM : CoopStateMachine<ECoopServerState, ECoopServerTrigger>
    {
        public readonly StateConfiguration PreparingState;
        public readonly StateConfiguration SendingWorldDataState;
        public readonly StateConfiguration ClientValidationState;
        public readonly StateConfiguration PlayingState;
        public readonly StateMachine<ECoopServerState, ECoopServerTrigger>.TriggerWithParameters<ConnectionServer> 
            SendWorldDataTrigger;
        public readonly StateMachine<ECoopServerState, ECoopServerTrigger>.TriggerWithParameters<ConnectionServer>
            SendPartyValidationTrigger;

        public CoopServerSM() : base(ECoopServerState.Preparing)
        {

            // Server wait for client connect
            PreparingState = StateMachine.Configure(ECoopServerState.Preparing);
            PreparingState.Permit(ECoopServerTrigger.RequiresWorldData, ECoopServerState.SendingWorldData);
            PreparingState.Permit(ECoopServerTrigger.DeclineWorldData, ECoopServerState.Playing);


            // Send world data
            SendWorldDataTrigger = StateMachine.SetTriggerParameters<ConnectionServer>(ECoopServerTrigger.RequiresWorldData);

            SendingWorldDataState = StateMachine.Configure(ECoopServerState.SendingWorldData);

            SendingWorldDataState.Permit(ECoopServerTrigger.ClientLoaded, ECoopServerState.ClientValidation);

            ClientValidationState = StateMachine.Configure(ECoopServerState.ClientValidation);

            SendPartyValidationTrigger = StateMachine.SetTriggerParameters<ConnectionServer>(ECoopServerTrigger.ClientLoaded);

            ClientValidationState.Permit(ECoopServerTrigger.ClientValidated, ECoopServerState.Playing);

            // Server playing
            PlayingState = StateMachine.Configure(ECoopServerState.Playing);
        }
    }
}
