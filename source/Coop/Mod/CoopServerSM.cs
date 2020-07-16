using Common;
using Network.Infrastructure;
using Stateless;

namespace Coop.Mod
{
    using StateConfiguration = StateMachine<ECoopServerState, ECoopServerTrigger>.StateConfiguration;
    enum ECoopServerState {
        Preparing,
        SendingWorldData,
        Playing,
    }

    enum ECoopServerTrigger {
        RequiresWorldData,
        DeclineWorldData,
        WorldDataRecieved,
    }

    class CoopServerSM : CoopStateMachine<ECoopServerState, ECoopServerTrigger>
    {
        public readonly StateConfiguration PreparingState;
        public readonly StateConfiguration SendingWorldDataState;
        public readonly StateConfiguration PlayingState;
        public readonly StateMachine<ECoopServerState, ECoopServerTrigger>.TriggerWithParameters<ConnectionServer> 
            SendWorldDataTrigger;

        public CoopServerSM() : base(ECoopServerState.Preparing)
        {

            // Server wait for client connect
            PreparingState = StateMachine.Configure(ECoopServerState.Preparing);
            PreparingState.Permit(ECoopServerTrigger.RequiresWorldData, ECoopServerState.SendingWorldData);
            PreparingState.Permit(ECoopServerTrigger.DeclineWorldData, ECoopServerState.Playing);


            // Send world data
            SendWorldDataTrigger = StateMachine.SetTriggerParameters<ConnectionServer>(ECoopServerTrigger.RequiresWorldData);

            SendingWorldDataState = StateMachine.Configure(ECoopServerState.SendingWorldData);
            SendingWorldDataState.Permit(ECoopServerTrigger.WorldDataRecieved, ECoopServerState.Playing);

            // Server playing
            PlayingState = StateMachine.Configure(ECoopServerState.Playing);
        }
    }
}
