namespace Sync.Behaviour
{
    /// <summary>
    /// Describes the behaviour of a patch after a prefix function has been executed.
    /// </summary>
    public enum ECallPropagation
    {
        /// <summary>
        /// The original method is called, meaning the call is propagated normally. 
        /// </summary>
        CallOriginal,
        /// <summary>
        /// The call is suppressed, that is the original function is not called. The intended use case for this
        /// behaviour is to intercept a function call locally in order to reroute it via the Coop server. The rerouted
        /// call can then be executed again once it has been sent to all clients.
        /// </summary>
        Suppress
    }
}