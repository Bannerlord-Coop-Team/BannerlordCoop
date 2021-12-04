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
        SendingGameData,

        /// <summary>
        /// A client is playing.
        /// </summary>
        Playing,
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
    }

    /// <summary>
    /// Defines state machine used by CoopServer
    /// </summary>
    class CoopServerSM : CoopStateMachine<ECoopServerState, ECoopServerTrigger>
    {
        public readonly StateConfiguration PreparingState;
        public readonly StateConfiguration SendingGameDataState;
        public readonly StateConfiguration PlayingState;
        public readonly StateMachine<ECoopServerState, ECoopServerTrigger>.TriggerWithParameters<ConnectionServer> 
            SendGameDataTrigger;

        public CoopServerSM() : base(ECoopServerState.Preparing)
        {

            // Server wait for client connect
            PreparingState = StateMachine.Configure(ECoopServerState.Preparing);

            PreparingState.Permit(ECoopServerTrigger.RequiresWorldData, ECoopServerState.SendingGameData);

            // Send world data
            SendGameDataTrigger = StateMachine.SetTriggerParameters<ConnectionServer>(ECoopServerTrigger.RequiresWorldData);

            SendingGameDataState = StateMachine.Configure(ECoopServerState.SendingGameData);

            SendingGameDataState.Permit(ECoopServerTrigger.ClientLoaded, ECoopServerState.Playing);

            // Server playing
            PlayingState = StateMachine.Configure(ECoopServerState.Playing);
        }
    }
}
