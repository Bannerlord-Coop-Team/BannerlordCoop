using Common.LogicStates;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// Top level client-side state machine logic orchestrator
    /// </summary>
    public interface IClientLogic : ILogic, IClientState
    {
        /// <summary>
        /// Client-side state
        /// </summary>
        IClientState State { get; set; }

        /// <summary>
        /// Networking Client for Client-side
        /// </summary>
        ICoopClient NetworkClient { get; }
    }
}