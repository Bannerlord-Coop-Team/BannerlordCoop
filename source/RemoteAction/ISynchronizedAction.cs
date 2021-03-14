using System.Collections.Generic;

namespace RemoteAction
{
    /// <summary>
    ///     Interface for an action that is synchronized across multiple clients.
    /// </summary>
    public interface ISynchronizedAction
    {
        /// <summary>
        ///     Arguments to the action.
        /// </summary>
        IEnumerable<Argument> Arguments { get; }
        /// <summary>
        ///     Evaluates whether this action is valid.
        /// </summary>
        /// <returns></returns>
        bool IsValid();
    }
}