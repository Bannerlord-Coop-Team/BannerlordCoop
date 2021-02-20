using JetBrains.Annotations;
using Sync.Invokable;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Interface to process a pending method call.
    /// </summary>
    public interface IPendingMethodCall
    {
        /// <summary>
        ///     The instance the method is called on. null for static method calls.
        /// </summary>
        [CanBeNull]
        object Instance { get; }

        /// <summary>
        ///     Call parameters to the methods.
        /// </summary>
        [NotNull]
        object[] Parameters { get; }

        /// <summary>
        ///     Get the method id of the call.
        /// </summary>
        InvokableId Id { get; }

        /// <summary>
        ///     The local call will be broadcast to all clients as an authoritative call. All clients will receive the
        ///     call on the same campaign tick. The originator of the call will receive the authoritative call as well.
        /// </summary>
        void Broadcast();
    }
}