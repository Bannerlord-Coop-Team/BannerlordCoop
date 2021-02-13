using JetBrains.Annotations;
using Sync;

namespace CoopFramework
{
    /// <summary>
    ///     Interface to synchronize method calls and field changes to other clients.
    /// </summary>
    public interface ISynchronization
    {
        /// <summary>
        ///     Broadcast a method call to all clients.
        /// </summary>
        /// <param name="id">Id of the method to call.</param>
        /// <param name="instance">Instance to call the method on. null for a static call.</param>
        /// <param name="args">Method call arguments.</param>
        void Broadcast(MethodId id, [CanBeNull] object instance, [NotNull] object[] args);
        /// <summary>
        ///     Broadcast a field change buffer to all clients.
        /// </summary>
        /// <param name="buffer">The buffer whose content should be broadcast.</param>
        void Broadcast([NotNull] FieldChangeBuffer buffer);
    }
}