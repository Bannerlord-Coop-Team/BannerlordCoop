using NLog;
using Stateless;
using System;

namespace Common
{
    public abstract class CoopStateMachine { 
        public Enum State { get; protected set; }
    }
    public class CoopStateMachine<T, U> : CoopStateMachine where T : Enum where U : Enum
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public readonly StateMachine<T, U> StateMachine;

        public CoopStateMachine(T StartingState)
        {
            StateMachine = new StateMachine<T, U>(StartingState);
            State = StartingState;

            StateMachine.OnTransitioned((stateMachine) =>
            {
                State = stateMachine.Destination;
                Logger.Debug($"{GetType().Name} switched from {stateMachine.Source} " +
                    $"to {stateMachine.Destination} " +
                    $"with trigger {stateMachine.Trigger}.");
            });
        }
    }
}
