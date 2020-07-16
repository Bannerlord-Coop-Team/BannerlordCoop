using Common;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network.Infrastructure
{
    using StateConfiguration = StateMachine<EServerState, EServerTrigger>.StateConfiguration;
    enum EServerState
    {
        Inactive,
        Starting,
        Running,
        Stopping
    }
    enum EServerTrigger
    {
        Start,
        Initialized,
        Stop,
        Stopped
    }
    class ServerSM : CoopStateMachine<EServerState, EServerTrigger>
    {
        public readonly StateConfiguration InactiveState;
        public readonly StateConfiguration StartingState;
        public readonly StateConfiguration RunningState;
        public readonly StateConfiguration StoppingState;
        public readonly StateMachine<EServerState, EServerTrigger>.TriggerWithParameters<ServerConfiguration> StartTrigger;
        public ServerSM() : base(EServerState.Inactive)
        {
            InactiveState = StateMachine.Configure(EServerState.Inactive).Permit(EServerTrigger.Start, EServerState.Starting);

            StartTrigger = StateMachine.SetTriggerParameters<ServerConfiguration>(EServerTrigger.Start);
            StartingState = StateMachine.Configure(EServerState.Starting)
                   .Permit(EServerTrigger.Initialized, EServerState.Running)
                   .Permit(EServerTrigger.Stop, EServerState.Stopping);

            RunningState = StateMachine.Configure(EServerState.Running)
                   .Permit(EServerTrigger.Stop, EServerState.Stopping);

            StoppingState = StateMachine.Configure(EServerState.Stopping)
                   
                   .Permit(EServerTrigger.Stopped, EServerState.Inactive);
        }
    }
}
